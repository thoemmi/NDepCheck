// (c) HMMüller 2006...2015

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class ItemSegment {
        [NotNull]
        private readonly ItemType _type;
        [NotNull]
        public readonly string[] Values;

        public static readonly Dictionary<string,string> CACHE = new Dictionary<string, string>(); 

        protected ItemSegment([NotNull]ItemType type, [NotNull]string[] values) {
            _type = type;
            //Values = values.Select(v => v == null ? null : string.Intern(v)).ToArray();
            //Values = values.Select(v => v == null ? null : Intern(v)).ToArray();
            Values = values;
        }

        private string Intern(string s) {
            string result;
            if (!CACHE.TryGetValue(s, out result)) {
                CACHE.Add(s, result = s);
            }
            return result;
        }

        public ItemType Type => _type;
    }

    public class ItemTail : ItemSegment {
        public ItemTail([NotNull]ItemType type, [NotNull]string[] values) : base(type, values) {
        }

        public override string ToString() {
            return "ItemTail(" + Type + ":" + string.Join(":", Values) + ")";
        }
    }

    /// <remarks>
    /// A token representing a complex name. 
    /// </remarks>
    public class Item : ItemSegment, INode {
        private readonly bool _isInner;

        private string _asString;
        private string _asStringWithType;

        public Item([NotNull]ItemType type, [NotNull]string reducedName, bool isInner)
            : this(type, isInner, new[] { reducedName }) {
        }

        private Item(ItemType type, bool isInner, string[] values)
            : base(type, values) {
            if (type.Length != values.Length) {
                throw new ArgumentException("keys.Length != values.Length", nameof(values));
            }
            _isInner = isInner;
        }

        public Item(ItemType type, params string[] values)
            : this(type, false, values) {
        }

        public override bool Equals(object obj) {
            Item other = obj as Item;
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

        public override int GetHashCode() {
            return Values.Aggregate(Type.GetHashCode(), (current, sum) => unchecked(current + sum.GetHashCode()));
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