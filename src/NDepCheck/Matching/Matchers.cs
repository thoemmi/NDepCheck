using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Matching {
    internal sealed class AlwaysMatcher : IMatcher {
        private readonly bool _alsoMatchDot;
        private readonly int _groupCount;

        private static readonly string[] NO_STRING = new string[0];

        public static readonly AlwaysMatcher ALL = new AlwaysMatcher(alsoMatchDot: true, groupCount: 0);

        public AlwaysMatcher(bool alsoMatchDot, int groupCount) {
            _alsoMatchDot = alsoMatchDot;
            _groupCount = groupCount;
        }

        private bool IsMatch(string value) {
            return _alsoMatchDot || !value.Contains('.');
        }

        public IEnumerable<string> Matches(string value, string[] ignoredReferences) {
            return IsMatch(value) ? _groupCount == 0 ? NO_STRING : Enumerable.Repeat(value, _groupCount) : null;
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

        public bool IsMatch(string value, string[] references) {
            return value == "";
        }

        public IEnumerable<string> Matches(string value, string[] ignoredReferences) {
            return value == "" ? _groups : null;
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

        public IEnumerable<string> Matches(string value, string[] ignoredReferences) {
            string[] result;
            if (Check(value, out result)) {
                return result;
            } else {
                return Remember(value, _isMatch(value, _segment)
                    ? (_resultGroupCt == 0 ? ItemPattern.NO_GROUPS : Enumerable.Repeat(value, _resultGroupCt).ToArray())
                    : null);
            }
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

        public string MatchString => _segment;

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
                  GetStartSegment(segment),
                  (value, seg) => value.IndexOf(seg, GetComparisonType(ignoreCase)) == 0,
                  ignoreCase, maxSize) {
        }

        private static string GetStartSegment(string segment) {
            string trimmedWithPossibleDotsAtEnd = segment.TrimStart('(').TrimEnd(')').TrimEnd('*');
            string trimmed = trimmedWithPossibleDotsAtEnd.TrimEnd('.');
            // Lone dots at the start survive, so that a pattern like ".**" creates a StartsWithMatcher with segment "."
            return trimmed == "" ? trimmedWithPossibleDotsAtEnd : trimmed;
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

        private readonly int _upperBoundOfGroupCount;
        private readonly Regex _regex;
        private readonly string _fixedPrefix;
        private readonly string _fixedSuffix;

        internal static string CreateGroupPrefix(int upperBoundOfGroupCount) {
            return string.Join("", Enumerable.Repeat("([^" + GROUPSEP + "]*)" + GROUPSEP, upperBoundOfGroupCount));
        }

        public RegexMatcher([NotNull]string pattern, bool ignoreCase, int upperBoundOfGroupCount,
                            [CanBeNull] string fixedPrefix, [CanBeNull] string fixedSuffix, int maxSize = 1000) : base(maxSize) {
            _upperBoundOfGroupCount = upperBoundOfGroupCount;
            try {
                _regex = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            } catch (ArgumentException) {
                Log.WriteError($"Cannot parse or interpret '{pattern}' - maybe (...) are missing on left side of rule?");
                throw;
            }
            _fixedPrefix = (fixedPrefix ?? "").Replace("(", "").Replace(")", "");
            _fixedSuffix = (fixedSuffix ?? "").Replace("(", "").Replace(")", "");
        }

        public IEnumerable<string> Matches(string value, string[] references) {
            string matchValue;
            if (_upperBoundOfGroupCount > 0 && references != null) {
                // Idea: From groups (e.g. a, b, c) and value(\1xy\2) construct the string a#b#c#\1xy\2.
                // The pattern created (see the constructor calls in CreateMatcher) is ([^#]*)#([^#]*)#([^#]*)#pattern
                // Thus, the groups prefixed to the string match the prefixed group patterns.
                int fillupWithArbitraryStringsCount = _upperBoundOfGroupCount - references.Length;
                IEnumerable<string> groupsWithFillUps = references.Concat(Enumerable.Repeat("IGNORE", fillupWithArbitraryStringsCount));
                string joinedGroupsWithFillUps = string.Join("", groupsWithFillUps.Select(g => g + GROUPSEP));
                matchValue = joinedGroupsWithFillUps + value;
            } else {
                matchValue = value;
            }

            Match m = _regex.Match(matchValue);
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
}