using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gibraltar;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Collections.Generic;

namespace NDepCheck {
    public class DotNetAssemblyDependencyReaderFactory : AbstractReaderFactory {
        public static readonly ItemType DOTNETREF = ItemType.New(
            "DOTNETREF",
            new[] { "ASSEMBLY", "ASSEMBLY", "ASSEMBLY" },
            new[] { ".NAME", ".VERSION", ".CULTURE" });

        public static readonly ItemType DOTNETCALL = ItemType.New(
            "DOTNETCALL",
            new[] { "NAMESPACE", "CLASS", "ASSEMBLY", "ASSEMBLY", "ASSEMBLY", "MEMBER", "MEMBER" },
            new[] { null, null, ".NAME", ".VERSION", ".CULTURE", ".NAME", ".SORT" });

        private readonly List<ItemType> _types = new List<ItemType> { DOTNETCALL, DOTNETREF };

        public override IEnumerable<ItemType> GetDescriptors() {
            return _types;
        }

        public override bool Accepts(string extension) {
            return extension == "dll" || extension == "exe";
        }

        public override AbstractDependencyReader CreateReader(string filename, Options options, bool needsOnlyItemTails) {
            return needsOnlyItemTails
                ? (AbstractDependencyReader) new ItemsOnlyDotNetAssemblyDependencyReader(this, filename, options)
                : new FullDotNetAssemblyDependencyReader(this, filename, options);
        }

        [NotNull]
        public ItemType GetOrCreateDotNetType(string name, IEnumerable<string> keysAndSubKeys) {
            string[] keys = keysAndSubKeys.Select(k => k.Split('.')[0]).ToArray();
            string[] subkeys = keysAndSubKeys.Select(k => k.Split('.').Length > 1 ? "." + k.Split('.')[1] : "").ToArray();
            return GetOrCreateDotNetType(name, keys, subkeys);
        }

        [NotNull]
        public ItemType GetOrCreateDotNetType(string name, string[] keys, string[] subkeys) {
            ItemType result = _types.FirstOrDefault(t => t.Name == name);
            if (result == null) {
                _types.Add(result = ItemType.New(name, keys, subkeys));
            } else {
                // TODO: Check that existing declaration = new one - for the moment, we "believe" that ...
            }
            return result;
        }

        public static void GetTypeAssemblyInfo(TypeReference reference, out string assemblyName, out string assemblyVersion, out string assemblyCulture) {
            switch (reference.Scope.MetadataScopeType) {
                case MetadataScopeType.AssemblyNameReference:
                    AssemblyNameReference r = (AssemblyNameReference) reference.Scope;
                    assemblyName = reference.Scope.Name;
                    assemblyVersion = r.Version.ToString();
                    assemblyCulture = r.Culture;
                    break;
                case MetadataScopeType.ModuleReference:
                    assemblyName = ((ModuleReference) reference.Scope).Name;
                    assemblyVersion = null;
                    assemblyCulture = null;
                    break;
                case MetadataScopeType.ModuleDefinition:
                    assemblyName = ((ModuleDefinition) reference.Scope).Assembly.Name.Name;
                    assemblyVersion = ((ModuleDefinition) reference.Scope).Assembly.Name.Version.ToString();
                    assemblyCulture = ((ModuleDefinition) reference.Scope).Assembly.Name.Culture;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public abstract class AbstractDotNetAssemblyDependencyReader : AbstractDependencyReader {
        protected readonly DotNetAssemblyDependencyReaderFactory _factory;
        protected readonly string _assemblyname;
        protected readonly Options _options;
        private Dictionary<RawUsedItem, Item> _rawItems2Items;

        protected AbstractDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory factory, string filename, Options options)
            : base(filename) {
            _factory = factory;
            _assemblyname = Path.GetFileNameWithoutExtension(filename);
            _options = options;
        }

        public string AssemblyName => _assemblyname;

        internal static void Init() {
#pragma warning disable 168
            // ReSharper disable once UnusedVariable
            // the only purpose of this instruction is to create a reference to Mono.Cecil.Pdb.
            // Otherwise Visual Studio won't copy that assembly to the output path.
            var readerProvider = new PdbReaderProvider();
#pragma warning restore 168
        }


        protected abstract class RawAbstractItem {
            private readonly string _namespaceName;
            public readonly string ClassName;
            public readonly string AssemblyName;
            private readonly string _assemblyVersion;
            private readonly string _assemblyCulture;
            private readonly string _memberName;
            private readonly string _memberSort;

            protected RawAbstractItem(string namespaceName, string className, string assemblyName, string assemblyVersion, string assemblyCulture, string memberName, string memberSort) {
                if (namespaceName == null) {
                    throw new ArgumentNullException(nameof(namespaceName));
                }
                if (className == null) {
                    throw new ArgumentNullException(nameof(className));
                }
                if (assemblyName == null) {
                    throw new ArgumentNullException(nameof(assemblyName));
                }
                _namespaceName = string.Intern(namespaceName);
                ClassName = string.Intern(className);
                AssemblyName = string.Intern(assemblyName);
                _assemblyVersion = string.Intern(assemblyVersion ?? "");
                _assemblyCulture = string.Intern(assemblyCulture ?? "");
                _memberName = string.Intern(memberName ?? "");
                _memberSort = string.Intern(memberSort ?? "");
            }

            public override string ToString() {
                return _namespaceName + ":" + ClassName + ":" + AssemblyName + ";" + _assemblyVersion + ";" + _assemblyCulture + ":" + _memberName + ";" + _memberSort;
            }

            [NotNull]
            public virtual Item ToItem(ItemType type) {
                return Item.New(type, _namespaceName, ClassName, AssemblyName, _assemblyVersion, _assemblyCulture, _memberName, _memberSort);
            }

            [NotNull]
            protected RawUsedItem ToRawUsedItem() {
                return RawUsedItem.New(_namespaceName, ClassName, AssemblyName, _assemblyVersion, _assemblyCulture, _memberName, _memberSort);
            }

            protected bool EqualsRawAbstractItem(RawAbstractItem other) {
                return this == other
                    || other != null
                       && other._namespaceName == _namespaceName
                       && other.ClassName == ClassName
                       && other.AssemblyName == AssemblyName
                       && other._assemblyVersion == _assemblyVersion
                       && other._assemblyCulture == _assemblyCulture
                       && other._memberName == _memberName
                       && other._memberSort == _memberSort;
            }

            protected int GetRawAbstractItemHashCode() {
                return unchecked(_namespaceName.GetHashCode() + ClassName.GetHashCode() + AssemblyName.GetHashCode() + (_memberName ?? "").GetHashCode());
            }
        }

        protected sealed class RawUsingItem : RawAbstractItem {
            private readonly ItemTail _tail;
            private Item _item;
            private RawUsedItem _usedItem;

            private RawUsingItem(string namespaceName, string className, string assemblyName, string assemblyVersion, string assemblyCulture, string memberName, string memberSort, ItemTail tail)
                : base(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, memberSort) {
                _tail = tail;
            }

            public static RawUsingItem New(string namespaceName, string className, string assemblyName,
                string assemblyVersion, string assemblyCulture, string memberName, string memberSort, ItemTail tail) {
                return Intern<RawUsingItem>.GetReference(new RawUsingItem(namespaceName, className, assemblyName,
                        assemblyVersion, assemblyCulture, memberName, memberSort, tail));
            }

            public override string ToString() {
                return "RawUsingItem(" + base.ToString() + ":" + _tail + ")";
            }

            public override bool Equals(object obj) {
                return EqualsRawAbstractItem(obj as RawUsingItem);
            }

            public override int GetHashCode() {
                return GetRawAbstractItemHashCode();
            }

            public override Item ToItem(ItemType type) {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (_item == null) {
                    _item = base.ToItem(type).Append(_tail);
                }
                return _item;
            }

            public new RawUsedItem ToRawUsedItem() {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (_usedItem == null) {
                    _usedItem = base.ToRawUsedItem();
                }
                return _usedItem;
            }
        }

        protected sealed class RawUsedItem : RawAbstractItem {
            private RawUsedItem(string namespaceName, string className, string assemblyName, string assemblyVersion, string assemblyCulture, string memberName, string memberSort)
                : base(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, memberSort) {
            }

            public static RawUsedItem New(string namespaceName, string className, string assemblyName,
                string assemblyVersion, string assemblyCulture, string memberName, string memberSort) {
                return Intern<RawUsedItem>.GetReference(new RawUsedItem(namespaceName, className, assemblyName,
                        assemblyVersion, assemblyCulture, memberName, memberSort));
            }

            public override string ToString() {
                return "RawUsedItem(" + base.ToString() + ")";
            }

            [CanBeNull] // null (I think) if assemblies do not match (different compiles) and hence a used item is not found in target reader.
            public Item ToItemWithTail(ItemType type, AbstractDotNetAssemblyDependencyReader reader, int depth) {
                return reader.GetFullItemFor(this, depth);
            }

            public override bool Equals(object obj) {
                return EqualsRawAbstractItem(obj as RawUsedItem);
            }

            public override int GetHashCode() {
                return GetRawAbstractItemHashCode();
            }
        }

        [NotNull]
        protected abstract IEnumerable<RawUsingItem> ReadUsingItems(int depth);

        [CanBeNull] // null (I think) if assemblies do not match (different compiles) and hence a used item is not found in target reader.
        protected Item GetFullItemFor(RawUsedItem rawUsedItem, int depth) {
            if (_rawItems2Items == null) {
                _rawItems2Items = new Dictionary<RawUsedItem, Item>();
                foreach (var u in ReadUsingItems(depth + 1)) {
                    RawUsedItem usedItem = u.ToRawUsedItem();
                    _rawItems2Items[usedItem] = u.ToItem(DotNetAssemblyDependencyReaderFactory.DOTNETCALL);
                }
            }
            Item result;
            _rawItems2Items.TryGetValue(rawUsedItem, out result);
            return result;
        }

        protected enum Usage {
            DeclareField,
            DeclareEvent,
            DeclareParameter,
            DeclareReturnType,
            Declaration,
            DeclareVariable,
            Use,
            Inherit,
            Implement,
            UseAsGenericArgument,
        }

        protected sealed class RawDependency {
            private readonly ItemType _type;
            public readonly RawUsingItem UsingItem;
            public readonly RawUsedItem UsedItem;
            public readonly Usage Usage;
            private readonly SequencePoint _sequencePoint;
            private readonly AbstractDotNetAssemblyDependencyReader _reader;

            public RawDependency([NotNull] ItemType type, [NotNull] RawUsingItem usingItem, [NotNull] RawUsedItem usedItem, 
                Usage usage, [CanBeNull] SequencePoint sequencePoint, Options options) {
                if (usingItem == null) {
                    throw new ArgumentNullException(nameof(usingItem));
                }
                if (usedItem == null) {
                    throw new ArgumentNullException(nameof(usedItem));
                }
                UsingItem = usingItem;
                UsedItem = usedItem;
                Usage = usage;
                _reader = options.GetDotNetAssemblyReaderFor(UsedItem.AssemblyName);
                _sequencePoint = sequencePoint;
                _type = type;
            }

            public override bool Equals(object obj) {
                var other = obj as RawDependency;
                return this == obj
                    || other != null 
                        && Equals(other.UsedItem, UsedItem)
                        && Equals(other.UsingItem, UsingItem)
                        && Equals(other._type, _type)
                        && Equals(other._sequencePoint, _sequencePoint);
            }

            public override int GetHashCode() {
                return UsedItem.GetHashCode() ^ UsingItem.GetHashCode();
            }

            public override string ToString() {
                return "RawDep " + UsingItem + "=>" + UsedItem;
            }

            [NotNull]
            private Dependency ToDependency(Item usedItem) {
                return _sequencePoint == null
                    ? new Dependency(UsingItem.ToItem(_type), usedItem, null, 0, 0, 0, 0, Usage.ToString(), 1)
                    : new Dependency(UsingItem.ToItem(_type), usedItem,
                        _sequencePoint.Document.Url, _sequencePoint.StartLine, _sequencePoint.StartColumn, 
                        _sequencePoint.EndLine, _sequencePoint.EndColumn, Usage.ToString(), 1);
            }

            [NotNull]
            public Dependency ToDependencyWithTail(int depth) {
                // ?? fires if reader == null (i.e., target assembly is not read in), or if assemblies do not match (different compiles) and hence a used item is not found in target reader.
                Item usedItem = (_reader == null ? null : UsedItem.ToItemWithTail(_type, _reader, depth)) ?? UsedItem.ToItem(_type);
                return ToDependency(usedItem);
            }
        }

        [CanBeNull]
        protected ItemTail GetCustomSections(Collection<CustomAttribute> customAttributes, [CanBeNull] ItemTail customSections) {
            ItemTail result = customSections;
            foreach (var customAttribute in customAttributes) {
                result = ExtractCustomSections(customAttribute, null) ?? result;
            }
            return result;
        }

        private readonly ISet<string> _loggedInfos = new HashSet<string>();

        private static readonly HashSet<string> _unresolvableTypeReferences = new HashSet<string>();

        private ItemTail ExtractCustomSections(CustomAttribute customAttribute, ItemTail parent) {
            TypeDefinition attributeType;
            TypeReference customAttributeTypeReference = customAttribute.AttributeType;
            if (_unresolvableTypeReferences.Contains(customAttributeTypeReference.FullName)) {
                attributeType = null;
            } else {
                try {
                    attributeType = customAttributeTypeReference.Resolve();
                } catch (Exception ex) {
                    _unresolvableTypeReferences.Add(customAttributeTypeReference.FullName);
                    attributeType = null;
                    string msg = "Cannot resolve " + customAttributeTypeReference + " - reason: " + ex.Message;
                    if (_loggedInfos.Add(msg)) {
                        Log.WriteInfo(msg);
                    }
                }
            }
            bool isSectionAttribute = attributeType != null && attributeType.Interfaces.Any(i => i.FullName == "NDepCheck.ISectionAttribute");
            if (isSectionAttribute) {
                string[] keys = attributeType.Properties.Select(property => property.Name).ToArray();
                FieldDefinition itemTypeNameField = attributeType.Fields.FirstOrDefault(f => f.Name == "ITEM_TYPE");
                if (itemTypeNameField == null) {
                    //??? Log.WriteError();
                    throw new Exception("String constant ITEM_TYPE not defined in " + attributeType.FullName);
                } else {
                    string itemTypeName = "" + itemTypeNameField.Constant;
                    ItemType itemType = GetOrDeclareType(itemTypeName, Enumerable.Repeat("CUSTOM", keys.Length), keys.Select(k => "." + k));
                    var args = keys.Select((k, i) => new {
                        Key = k,
                        Index = i,
                        Property = customAttribute.Properties.FirstOrDefault(p => p.Name == k)
                    });
                    string[] values = args.Select(a => a.Property.Name == null
                        ? parent?.Values[a.Index]
                        : "" + a.Property.Argument.Value).ToArray();
                    return ItemTail.New(itemType, values);
                }
            } else {
                return parent;
            }
        }

        private ItemType GetOrDeclareType(string itemTypeName, IEnumerable<string> keys, IEnumerable<string> subkeys) {
            return _factory.GetOrCreateDotNetType(itemTypeName,
                DotNetAssemblyDependencyReaderFactory.DOTNETCALL.Keys.Concat(keys).ToArray(),
                DotNetAssemblyDependencyReaderFactory.DOTNETCALL.SubKeys.Concat(subkeys).ToArray());
        }

        protected static void GetTypeInfo(TypeReference reference, out string namespaceName, out string className,
            out string assemblyName, out string assemblyVersion, out string assemblyCulture) {
            if (reference.DeclaringType != null) {
                string parentClassName, ignore1, ignore2, ignore3;
                GetTypeInfo(reference.DeclaringType, out namespaceName, out parentClassName, out ignore1, out ignore2, out ignore3);
                className = parentClassName + "/" + CleanClassName(reference.Name);
            } else {
                namespaceName = reference.Namespace;
                className = CleanClassName(reference.Name);
            }

            DotNetAssemblyDependencyReaderFactory.GetTypeAssemblyInfo(reference, out assemblyName, out assemblyVersion, out assemblyCulture);
        }

        private static string CleanClassName(string className) {
            if (!string.IsNullOrEmpty(className)) {
                className = className.TrimEnd('[', ']');
                int pos = className.LastIndexOf('`');
                if (pos > 0) {
                    className = className.Substring(0, pos);
                }
            }
            return className;
        }

        public static void Reset()
        {
            Intern<RawUsingItem>.Reset();
            Intern<RawUsedItem>.Reset();            
        }
    }
}