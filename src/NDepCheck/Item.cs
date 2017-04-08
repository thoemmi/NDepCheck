// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Gibraltar;
using JetBrains.Annotations;
using NDepCheck.Rendering;

namespace NDepCheck {
    public abstract class ItemSegment : ObjectWithMarkers {
        [NotNull]
        private readonly ItemType _type;
        [NotNull]
        public readonly string[] Values;

        protected ItemSegment([NotNull] ItemType type, [NotNull] string[] values) : base(markers: null) {
            _type = type;
            Values = values.Select(v => v == null ? null : string.Intern(v)).ToArray();
        }

        public ItemType Type => _type;

        [DebuggerStepThrough]
        protected bool EqualsSegment(ItemSegment other) {
            if (other == null) {
                return false;
            } else {
                if (!Type.Equals(other.Type)) {
                    return false;
                }
                if (Values.Length != other.Values.Length) {
                    return false;
                }
                for (int i = 0; i < Values.Length; i++) {
                    if (Values[i] != other.Values[i]) {
                        return false;
                    }
                }
                return true;
            }
        }

        [DebuggerStepThrough]
        protected int SegmentHashCode() {
            int h = _type.GetHashCode();

            foreach (var t in Values) {
                h ^= (t ?? "").GetHashCode();
            }
            return h;
        }
    }

    public sealed class ItemTail : ItemSegment {
        private ItemTail([NotNull]ItemType type, [NotNull]string[] values) : base(type, values) {
        }

        public static ItemTail New([NotNull] ItemType type, [NotNull] string[] values) {
            return Intern<ItemTail>.GetReference(new ItemTail(type, values));
        }

        public override string ToString() {
            return "ItemTail(" + Type + ":" + string.Join(":", Values) + ")";
        }

        public override bool Equals(object other) {
            return EqualsSegment(other as ItemTail);
        }

        [DebuggerHidden]
        public override int GetHashCode() {
            return SegmentHashCode();
        }
    }

    /// <remarks>
    /// A token representing a complex name. 
    /// </remarks>
    public sealed class Item : ItemSegment, INode {
        private AdditionalDynamicData _additionalDynamicData;

        private string _asString;
        private string _asStringWithType;
        private string _order;

        private Item([NotNull] ItemType type, string[] values)
            : base(type, values) {
            if (type.Length != values.Length) {
                throw new ArgumentException("keys.Length != values.Length", nameof(values));
            }
        }

        public static Item New([NotNull]ItemType type, [ItemNotNull] params string[] values) {
            return Intern<Item>.GetReference(new Item(type, values));
        }

        public static Item New([NotNull]ItemType type, [NotNull]string reducedName) {
            return New(type, reducedName.Split(':'));
        }

        /// <summary>
        /// Container for any additional data useful for other algorithms. This is e.g. helpful
        /// during rendering in a <see cref="GraphicsDependencyRenderer"/> for associating various
        /// <see cref="IBox"/>es with an <see cref="Item"/>.
        /// </summary>
        public dynamic DynamicData => _additionalDynamicData ?? (_additionalDynamicData = new AdditionalDynamicData(this));

        public string Order => _order;

        public Item SetOrder(string order) {
            _order = order;
            _asStringWithType = null;
            return this;
        }

        public string Name => AsString();

        public bool IsEmpty() {
            return Values.All(s => s == "");
        }

        [DebuggerStepThrough]
        public override bool Equals(object obj) {
            var other = obj as Item;
            return other != null && EqualsSegment(other);
        }

        [DebuggerHidden]
        public override int GetHashCode() {
            return SegmentHashCode();
        }

        public override string ToString() {
            return AsStringWithOrderAndType();
        }

        [NotNull]
        public string AsStringWithOrderAndType() {
            return _asStringWithType
                ?? (_asStringWithType = Type.Name + (string.IsNullOrEmpty(Order) ? "" : ";" + Order) + ":" + AsString());
        }

        [NotNull]
        public string AsString() {
            if (_asString == null) {
                var sb = new StringBuilder();
                string sep = "";
                for (int i = 0; i < Type.Length; i++) {
                    sb.Append(sep);
                    sb.Append(Values[i]);
                    sep = i < Type.Length - 1 && Type.Keys[i + 1] == Type.Keys[i] ? ";" : ":";
                }
                _asString = sb.ToString();
            }
            return _asString;
        }

        [NotNull]
        public Item Append([CanBeNull] ItemTail additionalValues) {
            return additionalValues == null ? this : new Item(additionalValues.Type, Values.Concat(additionalValues.Values).ToArray()).SetOrder(Order);
        }

        public static Dictionary<Item, IEnumerable<Dependency>> CollectIncomingDependenciesMap(IEnumerable<Dependency> dependencies) {
            return CollectMap(dependencies, d => d.UsedItem, d => d)
                    .ToDictionary(kvp => kvp.Key, kvp => (IEnumerable<Dependency>)kvp.Value);
        }

        public static Dictionary<Item, IEnumerable<Dependency>> CollectOutgoingDependenciesMap(IEnumerable<Dependency> dependencies) {
            return CollectMap(dependencies, d => d.UsingItem, d => d)
                    .ToDictionary(kvp => kvp.Key, kvp => (IEnumerable<Dependency>)kvp.Value);
        }

        public static Dictionary<Item, List<T>> CollectMap<T>(IEnumerable<Dependency> dependencies, 
                                            Func<Dependency, Item> getItem, Func<Dependency, T> createT) {
            var result = new Dictionary<Item, List<T>>();
            foreach (var d in dependencies) {
                List<T> list;
                Item key = getItem(d);
                if (!result.TryGetValue(key, out list)) {
                    result.Add(key, list = new List<T>());
                }
                list.Add(createT(d));
            }
            return result;
        }
    }
}