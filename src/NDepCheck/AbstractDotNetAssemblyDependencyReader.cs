using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Collections.Generic;

namespace NDepCheck {
    public class DotNetAssemblyDependencyReaderFactory : AbstractReaderFactory {
        public static readonly ItemType DOTNETREF = new ItemType(
            "DOTNETREF",
            new[] { "ASSEMBLY", "ASSEMBLY", "ASSEMBLY" },
            new[] { ".NAME", ".VERSION", ".CULTURE" });

        public static readonly ItemType DOTNETCALL = new ItemType(
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
                ? (AbstractDependencyReader)new ItemsOnlyDotNetAssemblyDependencyReader(this, filename, options)
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
                _types.Add(result = new ItemType(name, keys, subkeys));
            } else {
                // TODO: Check that existing declaration = new one - for the moment, we "believe" that ...
            }
            return result;
        }

        public static void GetTypeAssemblyInfo(TypeReference reference, out string assemblyName, out string assemblyVersion, out string assemblyCulture) {
            switch (reference.Scope.MetadataScopeType) {
                case MetadataScopeType.AssemblyNameReference:
                    AssemblyNameReference r = (AssemblyNameReference)reference.Scope;
                    assemblyName = reference.Scope.Name;
                    assemblyVersion = r.Version.ToString();
                    assemblyCulture = r.Culture;
                    break;
                case MetadataScopeType.ModuleReference:
                    assemblyName = ((ModuleReference)reference.Scope).Name;
                    assemblyVersion = null;
                    assemblyCulture = null;
                    break;
                case MetadataScopeType.ModuleDefinition:
                    assemblyName = ((ModuleDefinition)reference.Scope).Assembly.Name.Name;
                    assemblyVersion = ((ModuleDefinition)reference.Scope).Assembly.Name.Version.ToString();
                    assemblyCulture = ((ModuleDefinition)reference.Scope).Assembly.Name.Culture;
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
            private readonly string _className;
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
                _namespaceName = namespaceName;
                _className = className;
                AssemblyName = assemblyName;
                _assemblyVersion = assemblyVersion ?? "";
                _assemblyCulture = assemblyCulture ?? "";
                _memberName = memberName ?? "";
                _memberSort = memberSort ?? "";
            }

            public override string ToString() {
                return _namespaceName + ":" + _className + ":" + AssemblyName + ";" + _assemblyVersion + ";" + _assemblyCulture + ":" + _memberName + ";" + _memberSort;
            }

            [NotNull]
            public virtual Item ToItem(ItemType type) {
                return new Item(type, _namespaceName, _className, AssemblyName, _assemblyVersion, _assemblyCulture, _memberName, _memberSort);
            }

            [NotNull]
            protected RawUsedItem ToRawUsedItem() {
                return new RawUsedItem(_namespaceName, _className, AssemblyName, _assemblyVersion, _assemblyCulture, _memberName, _memberSort);
            }

            protected bool EqualsRawAbstractItem(RawAbstractItem other) {
                return other != null
                       && other._namespaceName == _namespaceName
                       && other._className == _className
                       && other.AssemblyName == AssemblyName
                       && other._assemblyVersion == _assemblyVersion
                       && other._assemblyCulture == _assemblyCulture
                       && other._memberName == _memberName
                       && other._memberSort == _memberSort;
            }

            protected int GetRawAbstractItemHashCode() {
                return unchecked(_namespaceName.GetHashCode() + _className.GetHashCode() + AssemblyName.GetHashCode() + (_memberName ?? "").GetHashCode());
            }
        }

        protected class RawUsingItem : RawAbstractItem {
            private readonly ItemTail _tail;
            private Item _item;
            private RawUsedItem _usedItem;

            public RawUsingItem(string namespaceName, string className, string assemblyName, string assemblyVersion, string assemblyCulture, string memberName, string memberSort, ItemTail tail)
                : base(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, memberSort) {
                _tail = tail;
            }

            public override string ToString() {
                return "RawUsingItem(" + base.ToString() + ":" + _tail + ")";
            }

            public override Item ToItem(ItemType type) {
                if (_item == null) {
                    _item = base.ToItem(type).Append(_tail);
                }
                return _item;
            }

            public new RawUsedItem ToRawUsedItem() {
                if (_usedItem == null) {
                    _usedItem = base.ToRawUsedItem();
                }
                return _usedItem;
            }
        }

        protected class RawUsedItem : RawAbstractItem {
            public RawUsedItem(string namespaceName, string className, string assemblyName, string assemblyVersion, string assemblyCulture, string memberName, string memberSort)
                : base(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, memberSort) {
            }

            public override string ToString() {
                return "RawUsedItem(" + base.ToString() + ")";
            }

            public Item ToItemWithTail(ItemType type, AbstractDotNetAssemblyDependencyReader reader) {
                return reader.GetFullItemFor(this);
            }

            public override bool Equals(object obj) {
                return EqualsRawAbstractItem(obj as RawUsedItem);
            }

            public override int GetHashCode() {
                return GetRawAbstractItemHashCode();
            }
        }

        [NotNull]
        protected abstract IEnumerable<RawUsingItem> ReadUsingItems();

        [NotNull]
        protected Item GetFullItemFor(RawUsedItem rawUsedItem) {
            if (_rawItems2Items == null) {
                _rawItems2Items = new Dictionary<RawUsedItem, Item>();
                foreach (var u in ReadUsingItems()) {
                    _rawItems2Items[u.ToRawUsedItem()] = u.ToItem(DotNetAssemblyDependencyReaderFactory.DOTNETCALL);
                }
            }
            return _rawItems2Items[rawUsedItem];
        }

        protected class RawDependency {
            private readonly ItemType _type;
            public readonly RawUsingItem UsingItem;
            public readonly RawUsedItem UsedItem;
            private readonly SequencePoint _sequencePoint;

            public RawDependency(ItemType type, RawUsingItem usingItem, RawUsedItem usedItem, SequencePoint sequencePoint) {
                UsingItem = usingItem;
                UsedItem = usedItem;
                _sequencePoint = sequencePoint;
                _type = type;
            }

            public override string ToString() {
                return "RawDep " + UsingItem + "=>" + UsedItem;
            }

            [NotNull]
            private Dependency ToDependency(Item usedItem) {
                return _sequencePoint == null
                    ? new Dependency(UsingItem.ToItem(_type), usedItem, null, 0, 0, 0, 0)
                    : new Dependency(UsingItem.ToItem(_type), usedItem,
                        _sequencePoint.Document.Url, _sequencePoint.StartLine, _sequencePoint.StartColumn, _sequencePoint.EndLine, _sequencePoint.EndColumn);
            }

            [NotNull]
            public Dependency ToDependencyWithTail(Options options) {
                AbstractDotNetAssemblyDependencyReader reader = options.GetDotNetAssemblyReaderFor(UsedItem.AssemblyName);
                return ToDependency(reader == null ? UsedItem.ToItem(_type) : UsedItem.ToItemWithTail(_type, reader));
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

        private ItemTail ExtractCustomSections(CustomAttribute customAttribute, ItemTail parent) {
            TypeDefinition attributeType;
            try {
                attributeType = customAttribute.AttributeType.Resolve();
            } catch (Exception ex) {
                attributeType = null;
                string msg = "Cannot resolve " + customAttribute.AttributeType + " - reason: " + ex.Message;
                if (_loggedInfos.Add(msg)) {
                    Log.WriteInfo(msg);
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
                    return new ItemTail(itemType, values);
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

    }
}