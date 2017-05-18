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

namespace NDepCheck.Reading.AssemblyReading {
    public abstract class AbstractDotNetAssemblyDependencyReader : AbstractDependencyReader {
        protected readonly DotNetAssemblyDependencyReaderFactory _factory;
        public readonly string Assemblyname;

        protected readonly string[] GET_MARKER = { "get" };
        protected readonly string[] SET_MARKER = { "set" };

        private Dictionary<RawUsedItem, Item> _rawItems2Items;

        protected AbstractDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory factory, string fileName)
            : base(Path.GetFullPath(fileName), Path.GetFileName(fileName)) {
            _factory = factory;
            Assemblyname = Path.GetFileNameWithoutExtension(fileName);
        }

        public string AssemblyName => Assemblyname;

        internal static void Init() {
#pragma warning disable 168
            // ReSharper disable once UnusedVariable
            // the only purpose of this instruction is to create a reference to Mono.Cecil.Pdb.
            // Otherwise Visual Studio won't copy that assembly to the output path.
            var readerProvider = new PdbReaderProvider();
#pragma warning restore 168
        }


        protected abstract class RawAbstractItem {
            public readonly string NamespaceName;
            public readonly string ClassName;
            public readonly string AssemblyName;
            private readonly string _assemblyVersion;
            private readonly string _assemblyCulture;
            public readonly string MemberName;
            [CanBeNull, ItemNotNull]
            private readonly string[] _markers;

            protected RawAbstractItem(string namespaceName, string className, string assemblyName, string assemblyVersion,
                                      string assemblyCulture, string memberName, [CanBeNull, ItemNotNull] string[] markers) {
                if (namespaceName == null) {
                    throw new ArgumentNullException(nameof(namespaceName));
                }
                if (className == null) {
                    throw new ArgumentNullException(nameof(className));
                }
                if (assemblyName == null) {
                    throw new ArgumentNullException(nameof(assemblyName));
                }
                NamespaceName = string.Intern(namespaceName);
                ClassName = string.Intern(className);
                AssemblyName = string.Intern(assemblyName);
                _assemblyVersion = string.Intern(assemblyVersion ?? "");
                _assemblyCulture = string.Intern(assemblyCulture ?? "");
                MemberName = string.Intern(memberName ?? "");
                _markers = markers;
            }

            public override string ToString() {
                return NamespaceName + ":" + ClassName + ":" + AssemblyName + ";" + _assemblyVersion + ";" +
                       _assemblyCulture + ":" + MemberName + (_markers == null ? "" : "'" + string.Join("+", _markers));
            }

            [NotNull]
            public virtual Item ToItem(ItemType type) {
                return Item.New(type, new[] { NamespaceName, ClassName, AssemblyName, _assemblyVersion, _assemblyCulture, MemberName }, _markers);
            }

            [NotNull]
            protected RawUsedItem ToRawUsedItem() {
                return RawUsedItem.New(NamespaceName, ClassName, AssemblyName, _assemblyVersion, _assemblyCulture, MemberName, _markers);
            }

            protected bool EqualsRawAbstractItem(RawAbstractItem other) {
                return this == other
                    || other != null
                       && other.NamespaceName == NamespaceName
                       && other.ClassName == ClassName
                       && other.AssemblyName == AssemblyName
                       && other._assemblyVersion == _assemblyVersion
                       && other._assemblyCulture == _assemblyCulture
                       && other.MemberName == MemberName;
            }

            protected int GetRawAbstractItemHashCode() {
                return unchecked(NamespaceName.GetHashCode() ^ ClassName.GetHashCode() ^ AssemblyName.GetHashCode() ^ (MemberName ?? "").GetHashCode());
            }
        }

        protected sealed class RawUsingItem : RawAbstractItem {
            private readonly ItemTail _tail;
            private Item _item;
            private RawUsedItem _usedItem;

            private RawUsingItem(string namespaceName, string className, string assemblyName, string assemblyVersion, string assemblyCulture, string memberName, string[] markers, ItemTail tail)
                : base(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers) {
                _tail = tail;
            }

            public static RawUsingItem New(string namespaceName, string className, string assemblyName,
                string assemblyVersion, string assemblyCulture, string memberName, string[] markers, ItemTail tail) {
                return Intern<RawUsingItem>.GetReference(new RawUsingItem(namespaceName, className, assemblyName,
                                                         assemblyVersion, assemblyCulture, memberName, markers, tail));
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
            private RawUsedItem(string namespaceName, string className, string assemblyName, string assemblyVersion, 
                                string assemblyCulture, string memberName, string[] markers)
                : base(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers) {
            }

            public static RawUsedItem New(string namespaceName, string className, string assemblyName,
                string assemblyVersion, string assemblyCulture, string memberName, [CanBeNull, ItemNotNull] string[] markers) {
                //return Intern<RawUsedItem>.GetReference(new RawUsedItem(namespaceName, className, assemblyName,
                //        assemblyVersion, assemblyCulture, memberName, markers));
                // Dont make unique - costs lot of time; and Raw...Items are anyway removed at end of DLL reading.
                return new RawUsedItem(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers);
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
                    _rawItems2Items[usedItem] = u.ToItem(DotNetAssemblyDependencyReaderFactory.DOTNETITEM);
                }
            }
            Item result;
            _rawItems2Items.TryGetValue(rawUsedItem, out result);
            return result;
        }

        protected enum Usage {
            _declaresfield,
            _declaresevent,
            _declaresparameter,
            _declaresreturntype,
            _declaresvariable,
            _usesmember,
            //_usesmemberoftype, // requires declarations of "uses that type" rules, which opens up possibility to use ALL of that type.
                                 // This is not good. Rather, let the user manually add transitive dependencies via the member if this is needed!
            _usestype,
            _inherits,
            _implements,
            _usesasgenericargument,
        }

        protected sealed class RawDependency {
            private readonly ItemType _type;
            private readonly SequencePoint _sequencePoint;
            private readonly AbstractDotNetAssemblyDependencyReader _readerForUsedItem;

            public readonly RawUsingItem UsingItem;
            public readonly RawUsedItem UsedItem;
            public readonly Usage Usage;

            public RawDependency([NotNull] ItemType type, [NotNull] RawUsingItem usingItem, [NotNull] RawUsedItem usedItem,
                Usage usage, [CanBeNull] SequencePoint sequencePoint, AbstractDotNetAssemblyDependencyReader readerForUsedItem) {
                if (usingItem == null) {
                    throw new ArgumentNullException(nameof(usingItem));
                }
                if (usedItem == null) {
                    throw new ArgumentNullException(nameof(usedItem));
                }
                UsingItem = usingItem;
                UsedItem = usedItem;
                Usage = usage;
                _readerForUsedItem = readerForUsedItem;
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
            private Dependency ToDependency(Item usedItem, string containerUri) {
                return new Dependency(UsingItem.ToItem(_type), usedItem, _sequencePoint == null 
                            ? (ISourceLocation) new LocalSourceLocation(containerUri, UsingItem.NamespaceName + "." + UsingItem.ClassName + (string.IsNullOrWhiteSpace(UsingItem.MemberName) ? "" : "." + UsingItem.MemberName))
                            : new ProgramFileSourceLocation(containerUri, _sequencePoint.Document.Url, _sequencePoint.StartLine, _sequencePoint.StartColumn, _sequencePoint.EndLine, _sequencePoint.EndColumn),
                        Usage.ToString(), 1);
            }

            [NotNull]
            public Dependency ToDependencyWithTail(int depth, string containerUri) {
                // ?? fires if reader == null (i.e., target assembly is not read in), or if assemblies do not match (different compiles) and hence a used item is not found in target reader.
                Item usedItem = (_readerForUsedItem == null ? null : UsedItem.ToItemWithTail(_type, _readerForUsedItem, depth)) ?? UsedItem.ToItem(_type);
                return ToDependency(usedItem, containerUri);
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
            TypeReference customAttributeTypeReference = customAttribute.AttributeType;
            TypeDefinition attributeType = Resolve(customAttributeTypeReference);
            bool isSectionAttribute = attributeType != null && attributeType.Interfaces.Any(i => i.FullName == "NDepCheck.ISectionAttribute");
            if (isSectionAttribute) {
                string[] keys = attributeType.Properties.Select(property => property.Name).ToArray();
                FieldDefinition itemTypeNameField = attributeType.Fields.FirstOrDefault(f => f.Name == "ITEM_TYPE");
                if (itemTypeNameField == null) {
                    //??? Log.WriteError();
                    throw new Exception("string constant ITEM_TYPE not defined in " + attributeType.FullName);
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

        [CanBeNull]
        protected TypeDefinition Resolve(TypeReference typeReference) {
            if (_unresolvableTypeReferences.Contains(typeReference.FullName)) {
                return null;
            } else {
                TypeDefinition typeDefinition;
                try {
                    typeDefinition = typeReference.Resolve();
                } catch (Exception ex) {
                    _unresolvableTypeReferences.Add(typeReference.FullName);
                    typeDefinition = null;
                    string msg = "Cannot resolve " + typeReference + " - reason: " + ex.Message;
                    if (_loggedInfos.Add(msg)) {
                        Log.WriteInfo(msg);
                    }
                }
                return typeDefinition;
            }
        }

        private ItemType GetOrDeclareType(string itemTypeName, IEnumerable<string> keys, IEnumerable<string> subkeys) {
            return _factory.GetOrCreateDotNetType(itemTypeName,
                DotNetAssemblyDependencyReaderFactory.DOTNETITEM.Keys.Concat(keys).ToArray(),
                DotNetAssemblyDependencyReaderFactory.DOTNETITEM.SubKeys.Concat(subkeys).ToArray());
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

        public static void Reset() {
            Intern<RawUsingItem>.Reset();
            Intern<RawUsedItem>.Reset();
        }
    }
}