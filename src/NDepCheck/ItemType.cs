using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Gibraltar;
using JetBrains.Annotations;

namespace NDepCheck {
    public class ItemType : IEquatable<ItemType> {
        private static readonly Dictionary<string, ItemType> _allTypes = new Dictionary<string, ItemType>();

        [NotNull]
        public static readonly ItemType SIMPLE = New("SIMPLE", new[] { "Name" }, new[] { "" }, 
                                                     ignoreCase: false, matchesOnFieldNr: true, predefined: true);

        public static void ForceLoadingPredefinedSimpleTypes() {
            ForceLoadingPredefinedType(SIMPLE);
        }

        public static void ForceLoadingPredefinedType(ItemType t) {
            _allTypes[t.Name] = t;
        }

        [NotNull]
        public static ItemType Generic(int fieldNr, bool ignoreCase) {
            if (fieldNr < 1 || fieldNr > 40) {
                throw new ArgumentException("fieldNr must be 1...40", nameof(fieldNr));
            }
            return fieldNr == 1 ? SIMPLE :
                New("GENERIC_" + fieldNr,
                        Enumerable.Range(1, fieldNr).Select(i => "Field_" + i).ToArray(),
                        Enumerable.Range(1, fieldNr).Select(i => "").ToArray(), ignoreCase: ignoreCase, matchesOnFieldNr: true);
        }

        [NotNull]
        public readonly string Name;

        /// <summary>
        /// Only for types created with <see cref="Generic"/>, the comparison is done on the number of fields.
        /// Reason: These types are used for item creation if no type is known.
        /// </summary>
        private readonly bool _matchesOnFieldNr;

        private readonly bool _predefined;

        public bool IgnoreCase {
            get;
        }

        [NotNull]
        public readonly string[] Keys;

        [NotNull]
        public readonly string[] SubKeys;

        private ItemType([NotNull] string name, [NotNull] string[] keys, [NotNull] string[] subKeys, bool matchesOnFieldNr, bool ignoreCase, bool predefined) {
            if (keys.Length == 0) {
                throw new ArgumentException($"Item type {name} is defined with zero fields; this is not supported. Please correct type definition.", nameof(keys));
            }
            if (keys.Length != subKeys.Length) {
                throw new ArgumentException($"Item type {name} is defined with a different number of keys and subkeys, namely {keys.Length} vs. {subKeys.Length}; this is not supported. Please correct type definition.", nameof(subKeys));
            }
            if (subKeys.Any(subkey => !string.IsNullOrWhiteSpace(subkey) && subkey.Length < 2 && subkey[0] != '.' && subkey.Substring(1).Contains("."))) {
                throw new ArgumentException($"Subkeys of item type {name} must either be empty or .name; there are unsupported subkeys: " + string.Join(" ", subKeys), nameof(subKeys));
            }

            Keys = keys.Select(s => s?.Trim()).ToArray();
            SubKeys = subKeys.Select(s => s?.Trim()).ToArray();
            Name = name;
            _matchesOnFieldNr = matchesOnFieldNr;
            _predefined = predefined;
            IgnoreCase = ignoreCase;
        }

        public ItemType CommonType(ItemType other) {
            if (_matchesOnFieldNr) {
                return Keys.Length <= other.Keys.Length ? other : null;
            } else if (other._matchesOnFieldNr) {
                return Keys.Length >= other.Keys.Length ? this : null;
            } else if (Equals(this, other)) {
                return this;
            } else {
                return null;
            }
        }

        public static ItemType Find([NotNull] string name) {
            ItemType result;
            _allTypes.TryGetValue(name, out result);
            return result;
        }

        public static ItemType New([NotNull] string name, [NotNull] [ItemNotNull] string[] keys, [NotNull] [ItemNotNull] string[] subKeys, bool ignoreCase, bool matchesOnFieldNr = false, bool predefined = false) {
            ItemType result;
            if (!_allTypes.TryGetValue(name, out result)) {
                _allTypes.Add(name, result = new ItemType(name, keys, subKeys, matchesOnFieldNr, ignoreCase, predefined));
            }
            return result;
        }

        public int Length => Keys.Length;

        public bool Equals(ItemType other) {
            // ReSharper disable once UseNullPropagation - clearer for me
            if (other == null) {
                return false;
            }
            if (Keys.Length != other.Keys.Length) {
                return false;
            }
            for (int i = 0; i < Keys.Length; i++) {
                if (Keys[i] != other.Keys[i] || SubKeys[i] != other.SubKeys[i]) {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj) {
            return Equals(obj as ItemType);
        }

        [DebuggerHidden]
        public override int GetHashCode() {
            return Name.GetHashCode();
        }

        public override string ToString() {
            var result = new StringBuilder(Name);
            if (IgnoreCase) {
                result.Append('+');
            }
            var sep = '(';
            for (int i = 0; i < Keys.Length; i++) {
                result.Append(sep);
                if (SubKeys[i] != "") {
                    result.Append(Keys[i]);
                    result.Append(SubKeys[i]);
                } else {
                    result.Append(Keys[i].TrimEnd('.'));
                }
                sep = ':';
            }
            result.Append(')');
            return result.ToString();
        }

        public static ItemType New(string format) {
            string[] parts = format.Split(':', ';', ' ', '(', ')');
            string name = parts[0];
            return New(name.TrimEnd('+'), parts.Skip(1).Where(p => p != "").ToArray(), name.EndsWith("+"));
        }

        public static ItemType New(string name, IEnumerable<string> keysAndSubKeys, bool ignoreCase) {
            string[] keys = keysAndSubKeys.Select(k => k.Split('.')[0]).ToArray();
            string[] subkeys = keysAndSubKeys.Select(k => k.Split('.').Length > 1 ? "." + k.Split('.')[1] : "").ToArray();
            return New(name, keys, subkeys, ignoreCase);
        }

        public static void Reset() {
            ItemType[] predefinedTypes = _allTypes.Values.Where(kvp => kvp._predefined).ToArray();

            Intern<ItemType>.Reset();
            _allTypes.Clear();

            foreach (var p in predefinedTypes) {
                _allTypes.Add(p.Name, Intern<ItemType>.GetReference(p));
            }
        }

        public string KeysAndSubkeys() {
            var result = new StringBuilder();
            string sep = "";
            for (int i = 0; i < Keys.Length; i++) {
                result.Append(sep);
                result.Append(Keys[i] + SubKeys[i]);
                sep = ", ";
            }
            return result.ToString();
        }

        public int IndexOf(string keyAndSubkey) {
            for (int i = 0; i < Keys.Length; i++) {
                if (keyAndSubkey == Keys[i] + SubKeys[i]) {
                    return i;
                }
            }
            return -1;
        }
    }
}