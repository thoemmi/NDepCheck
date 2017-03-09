using System;
using System.Linq;
using System.Text;
using Gibraltar;
using JetBrains.Annotations;

namespace NDepCheck {
    public class ItemType : IEquatable<ItemType> {
        internal static readonly ItemType SIMPLE = new ItemType("SIMPLE", new[] { "Data " }, new[] { "" });

        [NotNull]
        public readonly string Name;

        [NotNull]
        public readonly string[] Keys;

        [NotNull]
        public readonly string[] SubKeys;

        private ItemType([NotNull]string name, [NotNull]string[] keys, [NotNull]string[] subKeys) {
            if (keys.Length != subKeys.Length) {
                throw new ArgumentException("keys.Length != subKeys.Length", nameof(subKeys));
            }
            Keys = keys;
            if (subKeys.Any(subkey => !string.IsNullOrEmpty(subkey) && subkey.Length < 2 && subkey[0] != '.' && subkey.Substring(1).Contains("."))) {
                throw new ArgumentException("Subkey must either be empty or .name, but not " + string.Join(" ", subKeys), nameof(subKeys));
            }

            SubKeys = subKeys;
            Name = name;
        }

        public static ItemType New([NotNull] string name, [NotNull, ItemNotNull] string[] keys, [NotNull, ItemNotNull] string[] subKeys) {
            return Intern<ItemType>.GetReference(new ItemType(name, keys, subKeys));
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

        public override int GetHashCode() {
            return Name.GetHashCode();
        }

        public override string ToString() {
            var result = new StringBuilder(nameof(ItemType) + " " + Name);
            var sep = ' ';
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
            return result.ToString();
        }

        public static ItemType New(string format) {
            string[] parts = format.Split(':');
            string name = parts[0];
            string[] keys = parts.Skip(1).ToArray();
            string[] subKeys = Enumerable.Repeat("", keys.Length).ToArray();
            return New(name, keys, subKeys);
        }
    }
}