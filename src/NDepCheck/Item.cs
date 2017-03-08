// (c) HMMüller 2006...2017

using System;
using System.Linq;
using System.Text;
using Gibraltar;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class ItemSegment {
        [NotNull]
        private readonly ItemType _type;
        [NotNull]
        public readonly string[] Values;

        protected ItemSegment([NotNull]ItemType type, [NotNull]string[] values) {
            _type = type;
            //Values = _debugSingleton;
            //Values = values;
            Values = values.Select(v => v == null ? null : string.Intern(v)).ToArray();
            //Values = values.Select(v => v == null ? null : StringReference.GetReference(v)).ToArray();
            //Values = values.Select(v => v == null ? null : MyIntern(v)).ToArray();
        }

        public ItemType Type => _type;

        protected bool EqualsSegment(ItemSegment other) {
            if (other == this) {
                return true;
            } else if (other == null) {
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

        protected int SegmentHashCode() {
            int h = _type.GetHashCode();

            foreach (string t in Values) {
                h ^= t.GetHashCode();
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

        public override int GetHashCode() {
            return SegmentHashCode();
        }
    }

    /// <remarks>
    /// A token representing a complex name. 
    /// </remarks>
    public sealed class Item : ItemSegment, INode {
        private readonly bool _isInner;

        private string _asString;
        private string _asStringWithType;

        private Item([NotNull]ItemType type, bool isInner, string[] values)
            : base(type, values) {
            if (type.Length != values.Length) {
                throw new ArgumentException("keys.Length != values.Length", nameof(values));
            }
            _isInner = isInner;
        }

        public static Item New([NotNull]ItemType type, bool isInner, string[] values) {
            return Intern<Item>.GetReference(new Item(type, isInner, values));
        }

        public static Item New([NotNull]ItemType type, [NotNull]string reducedName, bool isInner) {
            return New(type, isInner, new[] { reducedName });
        }

        public static Item New([NotNull]ItemType type, params string[] values) {
            return New(type, false, values);
        }

        public override bool Equals(object obj) {
            var other = obj as Item;
            return this == obj
                || other != null 
                    && other._isInner == _isInner 
                    && EqualsSegment(other);
        }

        public override int GetHashCode() {
            return SegmentHashCode();
        }

        public override string ToString() {
            return AsStringWithType();
        }

        [NotNull]
        public string AsStringWithType() {
            if (_asStringWithType == null) {
                _asStringWithType = Type.Name + ":" + AsString();
            }
            return _asStringWithType;
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

        public string Name => AsString();

        public bool IsInner => _isInner;

        [NotNull]
        public Item Append([CanBeNull] ItemTail additionalValues) {
            return additionalValues == null ? this : new Item(additionalValues.Type, _isInner, Values.Concat(additionalValues.Values).ToArray());
        }
    }
}