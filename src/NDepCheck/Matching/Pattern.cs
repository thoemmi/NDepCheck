using System;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public abstract class Pattern {
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
        protected internal static IMatcher CreateMatcher([NotNull] string segment, int upperBoundOfGroupCount, bool ignoreCase) {
            if (Regex.IsMatch(segment, @"^[\(\)-]+$")) { // Examples: (-), ()-, -(), ((-)), ((-))()
                return new EmptyStringMatcher(groupCount: segment.Count(c => c == '('));
            } else if (string.IsNullOrWhiteSpace(segment) || Regex.IsMatch(segment, @"^\(*\*+\)*$")) { // Examples: empty string, *, **, (**), (((****)))
                return new AlwaysMatcher(alsoMatchDot: string.IsNullOrWhiteSpace(segment) || segment.Count(c => c == '*') > 1, groupCount: segment.Count(c => c == '('));
            } else if (segment.StartsWith("^")) {
                string pattern = segment.TrimStart('^');
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(upperBoundOfGroupCount) + pattern, ignoreCase, upperBoundOfGroupCount, null, null);
            } else if (segment.EndsWith("$")) {
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(upperBoundOfGroupCount) + ".*" + segment, ignoreCase, upperBoundOfGroupCount, null, null);
            } else if (upperBoundOfGroupCount == 0 && HasNoRegexCharsExceptPeriod(segment.TrimStart('(').TrimEnd(')'))) {
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
                return new RegexMatcher("^" + RegexMatcher.CreateGroupPrefix(upperBoundOfGroupCount) + pattern + "$",
                    ignoreCase, upperBoundOfGroupCount, fixedPrefix, fixedSuffix);
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
    }
}