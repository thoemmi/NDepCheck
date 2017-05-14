using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public sealed class ItemPattern : Pattern {
        private interface IMatcherGroup {
            IMatcher[] Matchers { get; }
            MatchResult Matches(bool invert, string[] itemValues, string[] references);
        }
        private class MatcherVector : IMatcherGroup {
            public MatcherVector(IMatcher[] matchers) {
                Matchers = matchers;
            }

            public IMatcher[] Matchers { get; }
            public MatchResult Matches(bool invert, string[] itemValues, string[] references) {
                string[] groupsInItem = NO_GROUPS;

                for (int i = 0; i < Matchers.Length; i++) {
                    IMatcher matcher = Matchers[i];
                    string value = i < itemValues.Length ? itemValues[i] : "";
                    IEnumerable<string> groups = matcher.Matches(value, references);
                    if (groups == null) {
                        return new MatchResult(invert, null);
                    }
                    int ct = groups.Count();
                    if (ct > 0) {
                        var newGroupsInItem = new string[groupsInItem.Length + ct];
                        Array.Copy(groupsInItem, newGroupsInItem, groupsInItem.Length);
                        int j = groupsInItem.Length;
                        foreach (var g in groups) {
                            newGroupsInItem[j++] = g;
                        }
                        groupsInItem = newGroupsInItem;
                    }
                }
                return new MatchResult(!invert, groupsInItem);
            }
        }
        private class AnyWhereMatcher : IMatcherGroup {
            public AnyWhereMatcher(IMatcher matcher) {
                Matchers = new[] { matcher};
            }

            public IMatcher[] Matchers {
                get;
            }

            public MatchResult Matches(bool invert, string[] itemValues, string[] references) {
                foreach (var value in itemValues) {
                    IEnumerable<string> groups = Matchers[0].Matches(value, references);
                    if (groups != null) {
                        return new MatchResult(!invert, groups.ToArray());
                    }
                }
                return new MatchResult(invert, null);
            }
        }

        internal static readonly string[] NO_GROUPS = new string[0];

        private static readonly IMatcher _alwaysMatcher = new AlwaysMatcher(alsoMatchDot: true, groupCount: 0);

        [NotNull]
        private readonly ItemType _itemType;

        private readonly IMatcherGroup _matchers;

        public IMatcher[] Matchers => _matchers.Matchers;

        public ItemPattern([CanBeNull] ItemType itemTypeHintOrNull, [NotNull] string itemPattern, int upperBoundOfGroupCount, bool ignoreCase) {
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
                // We ignore empty segments which might be included for "clarity"
                parts = parts.Where(p => p != "");
                if (!parts.All(p => p.Contains("="))) {
                    throw new ApplicationException(
                        $"Pattern must either use names for all fields, or no names. Mixing positional and named parts is not allowed in {itemPattern}");
                }

                IMatcher[] matchers = Enumerable.Repeat(_alwaysMatcher, _itemType.Keys.Length).ToArray();
                foreach (var p in parts) {
                    string[] nameAndPattern = p.Split(new[] { '=' }, 2);
                    string keyAndSubkey = nameAndPattern[0].Trim();
                    int i = _itemType.IndexOf(keyAndSubkey);
                    if (i < 0) {
                        throw new ApplicationException($"Key '{keyAndSubkey}' not defined in item type {_itemType.Name}; keys are {_itemType.KeysAndSubkeys()}");
                    }
                    matchers[i] = CreateMatcher(nameAndPattern[1].Trim(), 0, ignoreCase);
                }
                _matchers = new MatcherVector(matchers);
            } else if (parts.Count() == 1 && !parts.Any(p => Regex.IsMatch(p, @"[;(\\*^$]"))) {
                // If there is only a single pattern without any special chars in it
                _matchers = new AnyWhereMatcher(CreateMatcher(parts.First(), 0, ignoreCase));
            } else {
                int j = 0;
                foreach (var p in parts) {
                    foreach (var s in p.Split(';')) {
                        result.Add(CreateMatcher(s, upperBoundOfGroupCount, ignoreCase));
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
                _matchers = new MatcherVector(result.Take(_itemType.Keys.Length).ToArray());
            }
        }

        public MatchResult Matches<TItem>([NotNull] AbstractItem<TItem> item, bool invert, string[] references = null) where TItem : AbstractItem<TItem> {
            if (item.Type.CommonType(_itemType) == null) {
                return new MatchResult(invert, null);
            }

            return _matchers.Matches(invert, item.Values, references);
        }
    }
}
