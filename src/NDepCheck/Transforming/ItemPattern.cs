// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public sealed class ItemPattern : Pattern {
        internal static readonly string[] NO_GROUPS = new string[0];

        private static readonly IMatcher _alwaysMatcher = new AlwaysMatcher(alsoMatchDot: true, groupCount: 0);

        [NotNull]
        private readonly ItemType _itemType;

        private readonly IMatcher[] _matchers;

        public IMatcher[] Matchers => _matchers;

        public ItemPattern([CanBeNull] ItemType itemTypeHintOrNull, [NotNull] string itemPattern, int estimatedGroupCount, bool ignoreCase) {
            const string UNCOLLECTED_GROUP = "(?:";
            const string UNCOLLECTED_GROUP_MASK = "(?#@#";
            IEnumerable<string> parts = itemPattern.Replace(UNCOLLECTED_GROUP, UNCOLLECTED_GROUP_MASK)
                .Split(':')
                .Select(p => p.Replace(UNCOLLECTED_GROUP_MASK, UNCOLLECTED_GROUP))
                .ToArray();

            bool allowNamedPattern;
            ItemType type = ItemType.Find(parts.First());
            if (type != null) {
                parts = parts.Skip(1);
                _itemType = type;
                allowNamedPattern = true;
            } else if (itemTypeHintOrNull != null) {
                // Rules may optionally start with the correct type name (when they are copied from e.g. from a violation textfile).
                if (parts.First() == itemTypeHintOrNull.Name) {
                    parts = parts.Skip(1);
                }
                _itemType = itemTypeHintOrNull;
                allowNamedPattern = true;
            } else {
                // No type found form pattern, no itemTypeHint - we guess a generic type.
                _itemType = ItemType.Generic(parts.Count(), ignoreCase);
                allowNamedPattern = false;
            }

            var result = new List<IMatcher>();

            if (parts.Any(p => p.Contains("="))) {
                if (!allowNamedPattern) {
                    throw new ApplicationException(
                        $"No named patterns possible if type of pattern must be guessed; specify item type in pattern in {itemPattern}");
                }
                if (!parts.All(p => p.Contains("="))) {
                    throw new ApplicationException(
                        $"Pattern must either use names for all fields, or no names. Mixing positional and named parts is not allowed in {itemPattern}");
                }

                _matchers = Enumerable.Repeat(_alwaysMatcher, _itemType.Keys.Length).ToArray();
                foreach (var p in parts) {
                    string[] nameAndPattern = p.Split(new [] { '=' }, 2);
                    string keyAndSubkey = nameAndPattern[0].Trim();
                    int i = _itemType.IndexOf(keyAndSubkey);
                    if (i < 0) {
                        throw new ApplicationException($"Key '{keyAndSubkey}' not defined in item type {_itemType.Name}");
                    }
                    _matchers[i] = CreateMatcher(nameAndPattern[1].Trim(), 0, ignoreCase);
                }
            } else {
                int j = 0;
                foreach (var p in parts) {
                    foreach (var s in p.Split(';')) {
                        result.Add(CreateMatcher(s, estimatedGroupCount, ignoreCase));
                        j++;
                    }
                    while (j > 0 && j < _itemType.Keys.Length && _itemType.Keys[j - 1] == _itemType.Keys[j]) {
                        result.Add(_alwaysMatcher);
                        j++;
                    }
                }
                while (j < _itemType.Keys.Length) {
                    result.Add(_alwaysMatcher);
                    j++;
                }
                _matchers = result.Take(_itemType.Keys.Length).ToArray();
            }
        }

        internal ItemPattern(ItemType itemType, IMatcher[] matchers) {
            _itemType = itemType;
            _matchers = matchers;
        }

        public string[] Matches([NotNull] AbstractItem item) {
            if (item.Type.CommonType(_itemType) == null) {
                return null;
            }

            string[] groupsInItem = NO_GROUPS;

            for (int i = 0; i < _matchers.Length; i++) {
                IMatcher matcher = _matchers[i];
                string value = item.Values[i];
                string[] groups = matcher.Matches(value);
                if (groups == null) {
                    return null;
                }
                if (groups.Length > 0) {
                    var newGroupsInItem = new string[groupsInItem.Length + groups.Length];
                    Array.Copy(groupsInItem, newGroupsInItem, groupsInItem.Length);
                    Array.Copy(groups, 0, newGroupsInItem, groupsInItem.Length, groups.Length);
                    groupsInItem = newGroupsInItem;
                }
            }
            return groupsInItem ?? NO_GROUPS;
        }
    }
}
