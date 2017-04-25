// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public sealed class ItemPattern {
        // needed so that we can work distinguish user's ("meta") *, . and \ from the ones in replacements ("regex").
        public const string ASTERISK_ESCAPE = "@#@";
        public const string DOT_ESCAPE = "@&@";
        public const string BACKSLASH_ESCAPE = "@$@";

        private const string SEPARATORS_REGEX = @"[./\\]";
        private const string NON_SEPARATORS_REGEX = @"[^./\\]";

        private static readonly string CHARACTER_REGEX = EscapePattern(@"[^./:;+""'\\^$%()*]");
        private static readonly string ASTERISK_NEAR_LETTER_PATTERN = EscapePattern(CHARACTER_REGEX + "*");
        private static readonly string ASTERISK_ALONE_PATTERN = EscapePattern(CHARACTER_REGEX + "+");

        private static readonly string ASTERISKS_AFTER_LETTER_PATTERN =
            EscapePattern("(?:" + ASTERISK_NEAR_LETTER_PATTERN + "(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*)?");

        private static readonly string ASTERISKS_PATTERN = EscapePattern("(?:" + ASTERISK_ALONE_PATTERN + "(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*)?");
        private static readonly string SEPARATOR_AND_ASTERISKS_PATTERN = EscapePattern("(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*");

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
                    string keyAndSubkey = nameAndPattern[0];
                    int i = _itemType.IndexOf(keyAndSubkey);
                    if (i < 0) {
                        throw new ApplicationException($"Key '{keyAndSubkey}' not defined in item type {_itemType.Name}");
                    }
                    _matchers[i] = CreateMatcher(nameAndPattern[1], 0, ignoreCase);
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

        private static string EscapePattern(string pattern) {
            return pattern.Replace("*", ASTERISK_ESCAPE).Replace(".", DOT_ESCAPE).Replace(@"\", BACKSLASH_ESCAPE);
        }

        private static string UnescapePattern(string pattern) {
            return pattern.Replace(ASTERISK_ESCAPE, "*").Replace(DOT_ESCAPE, ".").Replace(BACKSLASH_ESCAPE, @"\");
        }

        [NotNull]
        private static string ExpandAsterisks([NotNull] string pattern, bool ignoreCase) {
            // . and \ must be replaced after loops so that looking for separators works!
            RegexOptions regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

            while (pattern.Contains("**")) {
                int indexOfPath = pattern.IndexOf("**", StringComparison.Ordinal);
                bool incorporateSeparatorOnLeft = indexOfPath > 0 && Regex.IsMatch(pattern.Substring(indexOfPath - 1, 1), SEPARATORS_REGEX);

                if (incorporateSeparatorOnLeft) {
                    pattern = Interpolate(pattern, indexOfPath - 1, 3, SEPARATOR_AND_ASTERISKS_PATTERN);
                } else {
                    bool matchNonSeparatorOnLeft = Regex.IsMatch(pattern.Substring(0, indexOfPath), ".*" + NON_SEPARATORS_REGEX + @"[\)]*$", regexOptions);
                    pattern = Interpolate(pattern, indexOfPath, 2, matchNonSeparatorOnLeft ? ASTERISKS_AFTER_LETTER_PATTERN : ASTERISKS_PATTERN);
                }
            }

            while (pattern.Contains("*")) {
                int indexOfPath = pattern.IndexOf("*", StringComparison.Ordinal);
                bool matchNonSeparatorOnLeft = Regex.IsMatch(pattern.Substring(0, indexOfPath), NON_SEPARATORS_REGEX + @"[\)]*$", regexOptions);
                bool matchNonSeparatorOnRight = Regex.IsMatch(pattern.Substring(indexOfPath + 1), @"^[\(]*" + NON_SEPARATORS_REGEX, regexOptions);
                pattern = Interpolate(pattern, indexOfPath, 1, matchNonSeparatorOnLeft || matchNonSeparatorOnRight ? ASTERISK_NEAR_LETTER_PATTERN : ASTERISK_ALONE_PATTERN);
            }

            pattern = pattern.Replace(".", "[.]");

            return UnescapePattern(pattern);
        }

        [NotNull]
        private static string Interpolate([NotNull] string pattern, int indexOfIdentPart, int replacedLength, [NotNull] string replacement) {
            return pattern.Substring(0, indexOfIdentPart) + replacement + pattern.Substring(indexOfIdentPart + replacedLength);
        }

        [NotNull]
        private static IMatcher CreateMatcher([NotNull] string segment, int estimatedGroupCount, bool ignoreCase) {
            if (Regex.IsMatch(segment, @"^[\(\)-]+$")) { // Examples: (-), ()-, -(), ((-)), ((-))()
                return new EmptyStringMatcher(groupCount: segment.Count(c => c == '('));
            } else if (string.IsNullOrWhiteSpace(segment) || Regex.IsMatch(segment, @"^\(*\*+\)*$")) { // Examples: empty string, *, **, (**), (((****)))
                return new AlwaysMatcher(alsoMatchDot: string.IsNullOrWhiteSpace(segment) || segment.Count(c => c == '*') > 1, groupCount: segment.Count(c => c == '('));
            } else if (segment.StartsWith("^")) {
                string pattern = segment.TrimStart('^');
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(estimatedGroupCount) + pattern, ignoreCase, estimatedGroupCount, null, null);
            } else if (segment.EndsWith("$")) {
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(estimatedGroupCount) + ".*" + segment, ignoreCase, estimatedGroupCount, null, null);
            } else if (estimatedGroupCount == 0 && HasNoRegexCharsExceptPeriod(segment.TrimStart('(').TrimEnd(')'))) {
                return new EqualsMatcher(segment, ignoreCase);
            } else if (IsPrefixAndSuffixAsterisksPattern(segment.TrimStart('(').TrimEnd(')'))) {
                return new ContainsMatcher(segment, ignoreCase);
            } else if (IsSuffixAsterisksPattern(segment.TrimStart('(').TrimEnd(')'))) {
                return new StartsWithMatcher(segment, ignoreCase);
            } else if (IsPrefixAsterisksPattern(segment.TrimStart('(').TrimEnd(')'))) {
                return new EndsWithMatcher(segment, ignoreCase);
            } else {
                string fixedPrefix = Regex.Match(segment, @"^[^*+\\]*").Value.TrimEnd('.');
                string fixedSuffix = Regex.Match(segment, @"[^*+\\]*$").Value.TrimStart('.');

                string pattern = ExpandAsterisks(segment, ignoreCase);
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(estimatedGroupCount) + pattern + "$",
                                        ignoreCase, estimatedGroupCount, fixedPrefix, fixedSuffix);
            }
        }

        private static bool IsPrefixAndSuffixAsterisksPattern([NotNull] string segment) {
            return segment.StartsWith("**") && segment.EndsWith("**") && HasNoRegexCharsExceptPeriod(segment.Trim('*'));
        }

        private static bool IsPrefixAsterisksPattern([NotNull] string segment) {
            return segment.StartsWith("**") && HasNoRegexCharsExceptPeriod(segment.TrimStart('*'));
        }

        private static bool IsSuffixAsterisksPattern([NotNull] string segment) {
            return segment.EndsWith("**") && HasNoRegexCharsExceptPeriod(segment.TrimEnd('*'));
        }

        private static bool HasNoRegexCharsExceptPeriod(string segment) {
            // See e.g. http://stackoverflow.com/questions/399078/what-special-characters-must-be-escaped-in-regular-expressions
            return !Regex.IsMatch(segment, @"[$*+?()[{\\|^]");
        }

        public string[] Matches([NotNull] Item item) {
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

    internal sealed class AlwaysMatcher : IMatcher {
        private readonly bool _alsoMatchDot;
        private readonly int _groupCount;

        public static readonly AlwaysMatcher ALL = new AlwaysMatcher(alsoMatchDot: true, groupCount: 0);

        public AlwaysMatcher(bool alsoMatchDot, int groupCount) {
            _alsoMatchDot = alsoMatchDot;
            _groupCount = groupCount;
        }

        public bool IsMatch(string value, string[] groups) {
            return _alsoMatchDot || !value.Contains('.');
        }

        public string[] Matches(string value) {
            return Enumerable.Repeat(value, _groupCount).ToArray();
        }

        public bool MatchesAlike(IMatcher other) {
            return other is AlwaysMatcher;
        }

        public override string ToString() {
            return "[**]";
        }

        public string GetKnownFixedPrefix() {
            return "";
        }

        public string GetKnownFixedSufffix() {
            return "";
        }
    }

    internal sealed class EmptyStringMatcher : IMatcher {
        private readonly string[] _groups;

        public EmptyStringMatcher(int groupCount) {
            _groups = Enumerable.Repeat("", groupCount).ToArray();
        }

        public bool IsMatch(string value, string[] groups) {
            return value == "";
        }

        public string[] Matches(string value) {
            return value == "" ? _groups : null;
        }

        public bool MatchesAlike(IMatcher other) {
            return other is EmptyStringMatcher;
        }

        public override string ToString() {
            return "[-]";
        }

        public string GetKnownFixedPrefix() {
            return "";
        }

        public string GetKnownFixedSufffix() {
            return "";
        }
    }

    internal abstract class AbstractRememberingMatcher {
        private readonly int _maxSize;
        private Dictionary<string, string[]> _seenStrings = new Dictionary<string, string[]>();

        protected AbstractRememberingMatcher(int maxSize) {
            _maxSize = maxSize;
        }

        protected bool Check(string s, out string[] entry) {
            return _seenStrings.TryGetValue(s, out entry);
        }

        protected string[] Remember(string s, string[] groupsOrNullForNonMatch) {
            if (_seenStrings.Count > _maxSize) {
                // Throw out 25% of the entries
                _seenStrings = _seenStrings.Skip(_maxSize / 4).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            _seenStrings.Add(s, groupsOrNullForNonMatch);
            return groupsOrNullForNonMatch;
        }
    }

    internal abstract class AbstractRememberingDelegateMatcher : AbstractRememberingMatcher, IMatcher {
        protected readonly string _segment;
        private readonly Func<string, string, bool> _isMatch;
        private readonly bool _ignoreCase;
        private readonly int _resultGroupCt;

        public bool IgnoreCase => _ignoreCase;

        protected static StringComparison GetComparisonType(bool ignoreCase) {
            return ignoreCase ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
        }

        protected AbstractRememberingDelegateMatcher(int resultGroupCt, string segment, Func<string, string, bool> isMatch, bool ignoreCase, int maxSize)
            : base(maxSize) {
            _segment = segment;
            _resultGroupCt = resultGroupCt;
            _isMatch = isMatch;
            _ignoreCase = ignoreCase;
        }

        public bool IsMatch(string value, string[] groups) {
            return Matches(value) != null;
        }

        public string[] Matches(string value) {
            string[] result;
            if (Check(value, out result)) {
                return result;
            } else {
                return Remember(value, _isMatch(value, _segment)
                    ? (_resultGroupCt == 0 ? ItemPattern.NO_GROUPS : Enumerable.Repeat(value, _resultGroupCt).ToArray())
                    : null);
            }
        }

        public virtual bool MatchesAlike(IMatcher other) {
            return other.GetType() == GetType() && string.Compare((other as AbstractRememberingDelegateMatcher)?._segment, _segment, _ignoreCase) == 0;
        }

        [NotNull]
        public abstract string GetKnownFixedPrefix();
        [NotNull]
        public abstract string GetKnownFixedSufffix();
    }

    internal sealed class ContainsMatcher : AbstractRememberingDelegateMatcher {
        public ContainsMatcher(string segment, bool ignoreCase, int maxSize = 1000)
            : base(segment.TakeWhile(c => c == '(').Count(),
                  segment.TrimStart('(').TrimEnd(')').Trim('*').Trim('.'),
                  (value, seg) => value.IndexOf(seg, GetComparisonType(ignoreCase)) >= 0,
                  ignoreCase, maxSize) {
        }

        public override string ToString() {
            return "[*" + _segment + "*]";
        }

        public override string GetKnownFixedPrefix() {
            return "";
        }

        public override string GetKnownFixedSufffix() {
            return "";
        }
    }

    internal sealed class EqualsMatcher : AbstractRememberingDelegateMatcher {
        public EqualsMatcher(string segment, bool ignoreCase, int maxSize = 1000)
            : base(segment.TakeWhile(c => c == '(').Count(),
                  segment.TrimStart('(').TrimEnd(')'),
                  (value, seg) => string.Compare(value, seg, ignoreCase) == 0,
                  ignoreCase, maxSize) {
        }

        public override string ToString() {
            return "[" + _segment + "]";
        }

        public override string GetKnownFixedPrefix() {
            return _segment;
        }

        public override string GetKnownFixedSufffix() {
            return _segment;
        }
    }

    internal sealed class StartsWithMatcher : AbstractRememberingDelegateMatcher {
        public StartsWithMatcher(string segment, bool ignoreCase, int maxSize = 1000)
            : base(segment.TakeWhile(c => c == '(').Count(),
                  segment.TrimStart('(').TrimEnd(')').TrimEnd('*').TrimEnd('.'),
                  (value, seg) => value.IndexOf(seg, GetComparisonType(ignoreCase)) == 0,
                  ignoreCase, maxSize) {
        }

        public override string ToString() {
            return "[" + _segment + "*]";
        }

        public override string GetKnownFixedPrefix() {
            return _segment;
        }

        public override string GetKnownFixedSufffix() {
            return "";
        }
    }

    internal sealed class EndsWithMatcher : AbstractRememberingDelegateMatcher {
        public EndsWithMatcher(string segment, bool ignoreCase, int maxSize = 1000)
            : base(segment.TakeWhile(c => c == '(').Count(),
                  segment.TrimStart('(').TrimEnd(')').TrimStart('*').TrimStart('.'),
                  (value, seg) => value.LastIndexOf(seg, GetComparisonType(ignoreCase)) >= value.Length - segment.Length,
                  ignoreCase, maxSize) {
        }

        public override string ToString() {
            return "[*" + _segment + "]";
        }

        public override string GetKnownFixedPrefix() {
            return "";
        }

        public override string GetKnownFixedSufffix() {
            return _segment;
        }
    }

    internal sealed class RegexMatcher : AbstractRememberingMatcher, IMatcher {
        private const char GROUPSEP = '#'; // TODO: Durch nbsp o.ä. ersetzen

        private readonly int _estimatedGroupCount;
        private readonly Regex _regex;
        private readonly string _fixedPrefix;
        private readonly string _fixedSuffix;

        internal static string CreateGroupPrefix(int estimatedGroupCount) {
            return string.Join("", Enumerable.Repeat("([^" + GROUPSEP + "]*)" + GROUPSEP, estimatedGroupCount));
        }

        public RegexMatcher([NotNull]string pattern, bool ignoreCase, int estimatedGroupCount,
                            [CanBeNull] string fixedPrefix, [CanBeNull] string fixedSuffix, int maxSize = 1000) : base(maxSize) {
            _estimatedGroupCount = estimatedGroupCount;
            _regex = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            _fixedPrefix = (fixedPrefix ?? "").Replace("(", "").Replace(")", "");
            _fixedSuffix = (fixedSuffix ?? "").Replace("(", "").Replace(")", "");
        }

        public bool IsMatch(string value, string[] groups) {
            // Idea: From groups (e.g. a, b, c) and value(\1xy\2) construct the string a#b#c#\1xy\2.
            // The pattern created (see the constructor calls in CreateMatcher) is ([^#]*)#([^#]*)#([^#]*)#pattern
            // Thus, the groups prefixed to the string match the prefixed group patterns.
            int fillupWithArbitraryStringsCount = EstimatedGroupCount() - groups.Length;
            IEnumerable<string> groupsWithFillUps = groups.Concat(Enumerable.Range(0, fillupWithArbitraryStringsCount).Select(_ => "IGNORE"));
            string joinedGroupsWithFillUps = string.Join("", groupsWithFillUps.Select(g => g + GROUPSEP));

            bool isMatch = _regex.IsMatch(joinedGroupsWithFillUps + value);
            return isMatch;
        }

        public string[] Matches(string value) {
            Match m = _regex.Match(value);
            if (m.Success) {
                string[] groups = new string[m.Groups.Count - 1];
                for (int i = 1; i < m.Groups.Count; i++) {
                    groups[i - 1] = m.Groups[i].Value;
                }
                return groups;
            } else {
                return null;
            }
        }

        public bool MatchesAlike(IMatcher other) {
            return (other as RegexMatcher)?._regex.Equals(_regex) ?? false;
        }

        public int EstimatedGroupCount() {
            return _estimatedGroupCount;
        }

        public override string ToString() {
            return "[/" + _regex + "/]";
        }

        public string GetKnownFixedPrefix() {
            return _fixedPrefix;
        }

        public string GetKnownFixedSufffix() {
            return _fixedSuffix;
        }
    }

    // TODO: NOCH NICHT IN BETRIEB ...
    internal class RegexMatcherWithBackReferences : IMatcher {
        private const char GROUPSEP = '#'; // TODO: Durch nbsp o.ä. ersetzen

        private readonly int _estimatedGroupCount;
        private readonly Regex _regex;

        public RegexMatcherWithBackReferences(string pattern, bool ignoreCase, int estimatedGroupCount) {
            _estimatedGroupCount = estimatedGroupCount;
            _regex = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }

        public bool IsMatch(string value, string[] groups) {

            int fillupWithArbitraryStringsCount = EstimatedGroupCount() - groups.Length;
            IEnumerable<string> groupsWithFillUps = groups.Concat(Enumerable.Range(0, fillupWithArbitraryStringsCount).Select(_ => "IGNORE"));
            string joinedGroupsWithFillUps = string.Join("", groupsWithFillUps.Select(g => g + GROUPSEP));

            bool isMatch = _regex.IsMatch(joinedGroupsWithFillUps + value);
            return isMatch;
        }

        public string[] Matches(string value) {
            Match m = _regex.Match(value);
            if (m.Success) {
                string[] groups = new string[m.Groups.Count - 1];
                for (int i = 1; i < m.Groups.Count; i++) {
                    groups[i - 1] = m.Groups[i].Value;
                }
                return groups;
            } else {
                return null;
            }
        }

        public int EstimatedGroupCount() {
            return _estimatedGroupCount;
        }

        public bool MatchesAlike(IMatcher other) {
            throw new NotImplementedException();
        }

        public string GetKnownFixedPrefix() {
            throw new NotImplementedException();
        }

        public string GetKnownFixedSufffix() {
            throw new NotImplementedException();
        }
    }
}
