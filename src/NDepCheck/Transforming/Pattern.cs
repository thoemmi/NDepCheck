// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    /// <remarks>
    /// Parent class for pattern objects (currently,
    /// <see>DependencyRule</see> and <see>Projection</see>).
    /// This class provides the helper methods to produce
    /// regular expressions from wildcard patterns; and to
    /// extend regular expression in four ways (not at all,
    /// inner classes, methods, and methods of inner classes).
    /// </remarks>
    public sealed class ItemPattern {
        // needed so that we can work distinguish user's ("meta") *, . and \ from the ones in replacements ("regex").
        public const string ASTERISK_ESCAPE = "@#@";
        public const string DOT_ESCAPE = "@&@";
        public const string BACKSLASH_ESCAPE = "@$@";

        private readonly IMatcher[] _matchers;

        internal static readonly string[] NO_GROUPS = new string[0];
        
        public ItemPattern([NotNull] ItemType itemType, [NotNull] string itemPattern, int estimatedGroupCount, bool ignoreCase) {
            _itemType = itemType;
            _matchers = CreateMatchers(itemType, itemPattern, estimatedGroupCount, ignoreCase);
        }

        public IMatcher[] Matchers => _matchers;


        private static string EscapePattern(string pattern) {
            return pattern.Replace("*", ASTERISK_ESCAPE).Replace(".", DOT_ESCAPE).Replace(@"\", BACKSLASH_ESCAPE);
        }

        private static string UnescapePattern(string pattern) {
            return pattern.Replace(ASTERISK_ESCAPE, "*").Replace(DOT_ESCAPE, ".").Replace(BACKSLASH_ESCAPE, @"\");
        }

        private const string SEPARATORS_REGEX = @"[./\\]";
        private const string NON_SEPARATORS_REGEX = @"[^./\\]";

        private static readonly string CHARACTER_REGEX = EscapePattern(@"[^./:;+""'\\^$%()*]");
        private static readonly string ASTERISK_NEAR_LETTER_PATTERN = EscapePattern(CHARACTER_REGEX + "*");
        private static readonly string ASTERISK_ALONE_PATTERN = EscapePattern(CHARACTER_REGEX + "+");

        private static readonly string ASTERISKS_AFTER_LETTER_PATTERN =
            EscapePattern(@"(?:" + ASTERISK_NEAR_LETTER_PATTERN + @"(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*)?");

        private static readonly string ASTERISKS_PATTERN = EscapePattern(@"(?:" + ASTERISK_ALONE_PATTERN + @"(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*)?");
        private static readonly string SEPARATOR_AND_ASTERISKS_PATTERN = EscapePattern(@"(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*");
        private readonly ItemType _itemType;

        [NotNull]
        public static string ExpandAsterisks([NotNull] string pattern, bool ignoreCase) {
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
        public static IMatcher[] CreateMatchers([NotNull] ItemType type, [NotNull] string itemPattern, int estimatedGroupCount, bool ignoreCase) {
            var result = new List<IMatcher>();

            const string UNCOLLECTED_GROUP = "(?:";
            const string UNCOLLECTED_GROUP_MASK = "(?#@#";
            IEnumerable<string> parts = itemPattern.Replace(UNCOLLECTED_GROUP, UNCOLLECTED_GROUP_MASK)
                .Split(':')
                .Select(p => p.Replace(UNCOLLECTED_GROUP_MASK, UNCOLLECTED_GROUP))
                .ToArray();

            if (parts.First() == type.Name) {
                // Rules may optionally start with the correct type name (when they are copied from e.g. from a violation textfile).
                parts = parts.Skip(1);
            }

            int j = 0;
            foreach (var p in parts) {
                foreach (var s in p.Split(';')) {
                    result.Add(CreateMatcher(s, estimatedGroupCount, ignoreCase));
                    j++;
                }
                while (j > 0 && j < type.Keys.Length && type.Keys[j - 1] == type.Keys[j]) {
                    result.Add(new AlwaysMatcher(alsoMatchDot: true, groupCount: 0));
                    j++;
                }
            }
            while (j < type.Keys.Length) {
                result.Add(new AlwaysMatcher(alsoMatchDot: true, groupCount: 0));
                j++;
            }
            return result.Take(type.Keys.Length).ToArray();
        }

        [NotNull]
        private static IMatcher CreateMatcher([NotNull] string segment, int estimatedGroupCount, bool ignoreCase) {
            if (Regex.IsMatch(segment, @"^[\(\)-]+$")) { // Examples: (-), ()-, -(), ((-)), ((-))()
                return new EmptyStringMatcher(groupCount: segment.Count(c => c == '('));
            } else if (string.IsNullOrWhiteSpace(segment) || Regex.IsMatch(segment, @"^\(*\*+\)*$")) { // Examples: empty string, *, **, (**), (((****)))
                return new AlwaysMatcher(alsoMatchDot: string.IsNullOrWhiteSpace(segment) || segment.Count(c => c == '*') > 1, groupCount: segment.Count(c => c == '('));
            } else if (segment.StartsWith("^")) {
                string pattern = segment.TrimStart('^');
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(estimatedGroupCount) + pattern, ignoreCase, estimatedGroupCount);
            } else if (segment.EndsWith("$")) {
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(estimatedGroupCount) + ".*" + segment, ignoreCase, estimatedGroupCount);
            } else if (estimatedGroupCount == 0 && HasNoRegexCharsExceptPeriod(segment)) {
                // TODO: Also allow suurrounding ()
                return new EqualsMatcher(segment, ignoreCase);
            } else if (IsPrefixAndSuffixAsterisksPattern(segment)) {
                // TODO: Also allow suurrounding ()
                return new ContainsMatcher(segment, ignoreCase);
            } else if (IsSuffixAsterisksPattern(segment)) {
                // TODO: Also allow suurrounding ()
                return new StartsWithMatcher(segment, ignoreCase);
            } else if (IsPrefixAsterisksPattern(segment)) {
                // TODO: Also allow suurrounding ()
                return new EndsWithMatcher(segment, ignoreCase);
            } else {
                string pattern = ExpandAsterisks(segment, ignoreCase);
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(estimatedGroupCount) + pattern + "$", ignoreCase, estimatedGroupCount);
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
            return !Regex.IsMatch(segment, @"[\\*()+?]");
        }

        public string[] Match([NotNull] Item item) {
            return Match(_itemType, _matchers, item);
        }

        [CanBeNull]
        public static string[] Match([NotNull] ItemType type, [NotNull] IMatcher[] matchers, [NotNull] Item item) {
            if (!item.Type.Equals(type)) {
                return null;
            }

            string[] groupsInItem = NO_GROUPS;

            for (int i = 0; i < matchers.Length; i++) {
                IMatcher matcher = matchers[i];
                string value = item.Values[i];
                string[] groups = matcher.Match(value);
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

        public AlwaysMatcher(bool alsoMatchDot, int groupCount) {
            _alsoMatchDot = alsoMatchDot;
            _groupCount = groupCount;
        }

        public bool IsMatch(string value, string[] groups) {
            return _alsoMatchDot || !value.Contains('.');
        }

        public string[] Match(string value) {
            return Enumerable.Repeat(value, _groupCount).ToArray();
        }

        public bool MatchesAlike(IMatcher other) {
            return other is AlwaysMatcher;
        }

        public override string ToString() {
            return "[**]";
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

        public string[] Match(string value) {
            return value == "" ? _groups : null;
        }

        public bool MatchesAlike(IMatcher other) {
            return other is EmptyStringMatcher;
        }

        public override string ToString() {
            return "[-]";
        }
    }

    public abstract class AbstractRememberingMatcher {
        private readonly Dictionary<string, string[]> _seenStrings = new Dictionary<string, string[]>();

        protected bool Check(string s, out string[] entry) {
            return _seenStrings.TryGetValue(s, out entry);
        }

        protected string[] Remember(string s, string[] groupsOrNullForNonMatch) {
            _seenStrings.Add(s, groupsOrNullForNonMatch);
            // TODO: Limit size
            return groupsOrNullForNonMatch;
        }
    }

    internal abstract class AbstractDelegateMatcher : AbstractRememberingMatcher, IMatcher {
        protected readonly string _segment;
        private readonly Func<string, string, bool> _isMatch;
        private readonly bool _ignoreCase;

        protected static StringComparison GetComparisonType(bool ignoreCase) {
            return ignoreCase ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
        }

        protected AbstractDelegateMatcher(string segment, Func<string, string, bool> isMatch, bool ignoreCase) {
            _segment = segment;
            _isMatch = isMatch;
            _ignoreCase = ignoreCase;
        }

        public bool IsMatch(string value, string[] groups) {
            return Match(value) != null;
        }

        public string[] Match(string value) {
            string[] result;
            if (Check(value, out result)) {
                return result;
            } else {
                return Remember(value, _isMatch(value, _segment) ? ItemPattern.NO_GROUPS : null);
            }
        }

        public virtual bool MatchesAlike(IMatcher other) {
            return other.GetType() == GetType() && string.Compare((other as AbstractDelegateMatcher)?._segment, _segment, _ignoreCase) == 0;
        }
    }

    internal sealed class ContainsMatcher : AbstractDelegateMatcher {
        public ContainsMatcher(string segment, bool ignoreCase)
            : base(segment.Trim('*').Trim('.'), (value, seg) => value.IndexOf(seg, GetComparisonType(ignoreCase)) >= 0, ignoreCase) {
        }

        public override string ToString() {
            return "[*" + _segment + "*]";
        }
    }

    internal sealed class EqualsMatcher : AbstractDelegateMatcher {
        public EqualsMatcher(string segment, bool ignoreCase)
            : base(segment, (value, seg) => string.Compare(value, seg, ignoreCase) == 0, ignoreCase) {
        }

        public override string ToString() {
            return "[" + _segment + "]";
        }
    }

    internal sealed class StartsWithMatcher : AbstractDelegateMatcher {
        public StartsWithMatcher(string segment, bool ignoreCase)
            : base(segment.TrimEnd('*').TrimEnd('.'), (value, seg) => value.IndexOf(seg, GetComparisonType(ignoreCase)) == 0, ignoreCase) {
        }

        public override string ToString() {
            return "[" + _segment + "*]";
        }
    }

    internal sealed class EndsWithMatcher : AbstractDelegateMatcher {
        public EndsWithMatcher(string segment, bool ignoreCase)
            : base(segment.TrimStart('*').TrimStart('.'), (value, seg) => value.LastIndexOf(seg, GetComparisonType(ignoreCase)) >= value.Length - segment.Length, ignoreCase) {
        }

        public override string ToString() {
            return "[*" + _segment + "]";
        }
    }

    internal sealed class RegexMatcher : AbstractRememberingMatcher, IMatcher {
        private const char GROUPSEP = '#'; // TODO: Durch nbsp o.ä. ersetzen

        private readonly int _estimatedGroupCount;
        private readonly Regex _regex;

        internal static string CreateGroupPrefix(int estimatedGroupCount) {
            return string.Join("", Enumerable.Repeat("([^" + GROUPSEP + "]*)" + GROUPSEP, estimatedGroupCount));
        }

        public RegexMatcher([NotNull]string pattern, bool ignoreCase, int estimatedGroupCount) {
            _estimatedGroupCount = estimatedGroupCount;
            _regex = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
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

        public string[] Match(string value) {
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

        public string[] Match(string value) {
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
    }
}
