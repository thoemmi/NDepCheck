using System;
using System.Linq;
using Gibraltar;
using JetBrains.Annotations;

namespace NDepCheck {
    public class ItemType : IEquatable<ItemType> {
        internal static readonly ItemType DEFAULT = new ItemType("DEFAULT", new[] { "DATA " }, new[] { "" }); // ??????

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

        public static ItemType New([NotNull] string name, [NotNull] string[] keys, [NotNull] string[] subKeys) {
            return Intern<ItemType>.GetReference(new ItemType(name, keys, subKeys));
        }

        public int Length => Keys.Length;

        public bool Equals(ItemType other) {
            // ReSharper disable once PossibleUnintendedReferenceComparison
            if (this == other) {
                return true;
            } else if (other == null) {
                return false;
            } else if (Keys.Length != other.Keys.Length) {
                return false;
            } else {
                for (int i = 0; i < Keys.Length; i++) {
                    if (Keys[i] != other.Keys[i] || SubKeys[i] != other.SubKeys[i]) {
                        return false;
                    }
                }
                return true;
            }
        }

        public override bool Equals(object obj) {
            return Equals(obj as ItemType);
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}