// (c) HMMüller 2006...2015

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck {
    /// <remarks>
    /// Parent class for pattern objects (currently,
    /// <see>DependencyRule</see> and <see>GraphAbstraction</see>).
    /// This class provides the helper methods to produce
    /// regular expressions from wildcard patterns; and to
    /// extend regular expression in four ways (not at all,
    /// inner classes, methods, and methods of inner classes).
    /// </remarks>
    public abstract class Pattern {
        // needed so that we can work distinguish user's ("meta") *, . and \ from the ones in replacements ("regex").
        protected const string ASTERISK_ESCAPE = "@#@";
        protected const string DOT_ESCAPE = "@&@";
        protected const string BACKSLASH_ESCAPE = "@$@";

        private static string EscapePattern(string pattern) {
            return pattern.Replace("*", ASTERISK_ESCAPE).Replace(".", DOT_ESCAPE).Replace(@"\", BACKSLASH_ESCAPE);
        }

        private static string UnescapePattern(string pattern) {
            return pattern.Replace(ASTERISK_ESCAPE, "*").Replace(DOT_ESCAPE, ".").Replace(BACKSLASH_ESCAPE, @"\");
        }

        private const string SEPARATORS_REGEX = @"[./\\]";
        private const string NON_SEPARATORS_REGEX = @"[^./\\]";

        private static readonly string CHARACTER_REGEX = EscapePattern(@"[^./:;+""'\\^$%&()*]");
        private static readonly string ASTERISK_NEAR_LETTER_PATTERN = EscapePattern(CHARACTER_REGEX + "*");
        private static readonly string ASTERISK_ALONE_PATTERN = EscapePattern(CHARACTER_REGEX + "+");

        private static readonly string ASTERISKS_AFTER_LETTER_PATTERN =
            EscapePattern(@"(?:" + ASTERISK_NEAR_LETTER_PATTERN + @"(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*)?");

        private static readonly string ASTERISKS_PATTERN = EscapePattern(@"(?:" + ASTERISK_ALONE_PATTERN + @"(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*)?");
        private static readonly string SEPARATOR_AND_ASTERISKS_PATTERN = EscapePattern(@"(?:" + SEPARATORS_REGEX + ASTERISK_ALONE_PATTERN + ")*");

        public const char GROUPSEP = '#'; // TODO: Durch nbsp o.ä. ersetzen

        [NotNull]
        protected static string ExpandAsterisks([NotNull] string pattern, bool ignoreCase) {
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
        protected static IMatcher[] CreateMatchers([NotNull] ItemType type, [NotNull] string itemPattern, int estimatedGroupCount, bool ignoreCase) {
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
                    result.Add(new AlwaysMatcher());
                    j++;
                }
            }
            while (j < type.Keys.Length) {
                result.Add(new AlwaysMatcher());
                j++;
            }
            return result.Take(type.Keys.Length).ToArray();
        }

        [NotNull]
        private static IMatcher CreateMatcher([NotNull] string segment, int estimatedGroupCount, bool ignoreCase) {
            string groupPrefix = string.Join("", Enumerable.Repeat("([^" + GROUPSEP + "]*)" + GROUPSEP, estimatedGroupCount));
            if (segment == "-") {
                return new EmptyStringMatcher();
            } else if (string.IsNullOrWhiteSpace(segment) || segment.Trim('*') == "") {
                return new AlwaysMatcher();
            } else if (segment.StartsWith("^")) {
                string pattern = segment.TrimStart('^');
                return new RegexMatcher("^" + groupPrefix + pattern, ignoreCase, estimatedGroupCount);
            } else if (segment.EndsWith("$")) {
                string pattern = segment;
                return new RegexMatcher("^" + groupPrefix + ".*" + pattern, ignoreCase, estimatedGroupCount);
            } else if (IsPrefixAndSuffixAsterisksPattern(segment)) {
                return new ContainsMatcher(segment, ignoreCase);
            } else if (IsSuffixAsterisksPattern(segment)) {
                return new StartsWithMatcher(segment, ignoreCase);
            } else if (IsPrefixAsterisksPattern(segment)) {
                return new EndsWithMatcher(segment, ignoreCase);
            } else {
                string pattern = ExpandAsterisks(segment, ignoreCase);
                return new RegexMatcher("^" + groupPrefix + pattern + "$", ignoreCase, estimatedGroupCount);
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

        internal static readonly string[] NO_GROUPS = new string[0];

        [CanBeNull]
        protected static string[] Match([NotNull] ItemType type, [NotNull] IMatcher[] matchers, [NotNull] Item item) {
            if (item.Type != type) {
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

    internal class AlwaysMatcher : IMatcher {
        public bool IsMatch(string value, string[] groupsInUsing) {
            return true;
        }

        public string[] Match(string value) {
            return Pattern.NO_GROUPS;
        }
    }

    internal class EmptyStringMatcher : IMatcher {
        public bool IsMatch(string value, string[] groupsInUsing) {
            return value == "";
        }

        public string[] Match(string value) {
            return value == "" ? Pattern.NO_GROUPS : null;
        }
    }

    internal class ContainsMatcher : AbstractDelegateMatcher {
        public ContainsMatcher(string segment, bool ignoreCase)
            : base(segment.Trim('*').Trim('.'), (value, seg) => value.IndexOf(seg, GetComparisonType(ignoreCase)) >= 0) { }
    }

    internal class StartsWithMatcher : AbstractDelegateMatcher {
        public StartsWithMatcher(string segment, bool ignoreCase)
            : base(segment.TrimEnd('*').TrimEnd('.'), (value, seg) => value.IndexOf(seg, GetComparisonType(ignoreCase)) == 0) { }
    }

    internal class EndsWithMatcher : AbstractDelegateMatcher {
        public EndsWithMatcher(string segment, bool ignoreCase)
            : base(segment.TrimStart('*').TrimStart('.'), (value, seg) => value.LastIndexOf(seg, GetComparisonType(ignoreCase)) >= value.Length - segment.Length) { }
    }

    public interface IMatcher {
        bool IsMatch([NotNull]string value, [NotNull]string[] groupsInUsing);

        [CanBeNull]
        string[] Match([NotNull]string value);
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
        private readonly string _segment;
        private readonly Func<string, string, bool> _isMatch;

        protected static StringComparison GetComparisonType(bool ignoreCase) {
            return ignoreCase ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
        }

        protected AbstractDelegateMatcher(string segment, Func<string, string, bool> isMatch) {
            _segment = segment;
            _isMatch = isMatch;
        }

        public bool IsMatch(string value, string[] groupsInUsing) {
            return Match(value) != null;
        }

        public string[] Match(string value) {
            string[] result;
            if (Check(value, out result)) {
                return result;
            } else {
                return Remember(value, _isMatch(value, _segment) ? Pattern.NO_GROUPS : null);
            }
        }
    }

    internal class RegexMatcher : AbstractRememberingMatcher, IMatcher {
        private readonly int _estimatedGroupCount;
        private readonly Regex _regex;

        public RegexMatcher([NotNull]string pattern, bool ignoreCase, int estimatedGroupCount) {
            _estimatedGroupCount = estimatedGroupCount;
            _regex = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }

        public bool IsMatch(string value, string[] groupsInUsing) {
            int fillupWithArbitraryStringsCount = EstimatedGroupCount() - groupsInUsing.Length;
            IEnumerable<string> groupsWithFillUps = groupsInUsing.Concat(Enumerable.Range(0, fillupWithArbitraryStringsCount).Select(_ => "IGNORE"));
            string joinedGroupsWithFillUps = string.Join("", groupsWithFillUps.Select(g => g + Pattern.GROUPSEP));

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

        // TODO: NOCH NICHT IN BETRIEB ...
        internal class RegexMatcherWithBackReferences : IMatcher {
            private readonly int _estimatedGroupCount;
            private readonly Regex _regex;

            public RegexMatcherWithBackReferences(string pattern, bool ignoreCase, int estimatedGroupCount) {
                _estimatedGroupCount = estimatedGroupCount;
                _regex = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            }

            public bool IsMatch(string value, string[] groupsInUsing) {
                int fillupWithArbitraryStringsCount = EstimatedGroupCount() - groupsInUsing.Length;
                IEnumerable<string> groupsWithFillUps = groupsInUsing.Concat(Enumerable.Range(0, fillupWithArbitraryStringsCount).Select(_ => "IGNORE"));
                string joinedGroupsWithFillUps = string.Join("", groupsWithFillUps.Select(g => g + Pattern.GROUPSEP));

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
        }
    }
}
