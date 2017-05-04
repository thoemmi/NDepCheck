using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.ViolationChecking {
    public class DependencyRuleGroup {
        private static readonly Comparison<DependencyRule> _sortOnDescendingHitCount = (r1, r2) => r2.HitCount - r1.HitCount;

        [NotNull]
        private readonly List<DependencyRule> _allowed;
        [NotNull]
        private readonly List<DependencyRule> _questionable;
        [NotNull]
        private readonly List<DependencyRule> _forbidden;

        [NotNull]
        private readonly string _groupPattern;
        [CanBeNull]
        private readonly ItemMatch _groupMatchOrNullForMainGroup;

        private DependencyRuleGroup([NotNull] string groupPattern, [CanBeNull] ItemMatch groupMatchOrNullForMainGroup,
                [NotNull] IEnumerable<DependencyRule> allowed, [NotNull] IEnumerable<DependencyRule> questionable,
                [NotNull] IEnumerable<DependencyRule> forbidden) {
            _groupPattern = groupPattern;
            _groupMatchOrNullForMainGroup = groupMatchOrNullForMainGroup;
            _allowed = allowed.ToList();
            _questionable = questionable.ToList();
            _forbidden = forbidden.ToList();
        }

        public DependencyRuleGroup([NotNull] string groupPattern, [CanBeNull] ItemType groupItemTypeHintOrNull, bool ignoreCase)
            : this(groupPattern, groupPattern == "" ? null : new ItemMatch(groupItemTypeHintOrNull, groupPattern, 0, ignoreCase),
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>()) {
            // empty
        }

        [NotNull]
        public string GroupPattern => _groupPattern;

        /// <summary>
        /// Add one or more <c>DependencyRules</c>s from a single input line.
        /// public for testability.
        /// </summary>
        public bool AddDependencyRules([CanBeNull] ItemType usingItemTypeHint, [CanBeNull] ItemType usedItemTypeHint,
                                       [NotNull] string ruleSourceName, int lineNo, [NotNull] string line,
                                       bool ignoreCase, [NotNull] string previousRawUsingPattern, [NotNull] out string rawUsingPattern) {
            Match match;
            if (TryMatch(line, "^(.*)--(.*)->(.*)", out match)) {
                rawUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);
                IEnumerable<DependencyRule> rules = CreateDependencyRules(usingItemTypeHint, usedItemTypeHint, ruleSourceName, lineNo,
                    rawUsingPattern, match.Groups[2].Value, match.Groups[3].Value, "--", "->",
                    false, ignoreCase);
                _allowed.AddRange(rules);
                return true;
            } else if (TryMatch(line, "^(.*)--(.*)-[?](.*)", out match)) {
                rawUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);
                IEnumerable<DependencyRule> rules = CreateDependencyRules(usingItemTypeHint, usedItemTypeHint, ruleSourceName, lineNo,
                    rawUsingPattern, match.Groups[2].Value, match.Groups[3].Value, "--", "-?",
                    true, ignoreCase);
                _questionable.AddRange(rules);
                return true;
            } else if (TryMatch(line, "^(.*)--(.*)-!(.*)", out match)) {
                rawUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);
                IEnumerable<DependencyRule> rules = CreateDependencyRules(usingItemTypeHint, usedItemTypeHint, ruleSourceName, lineNo,
                    rawUsingPattern, match.Groups[2].Value, match.Groups[3].Value, "--", "-!",
                    false, ignoreCase);
                _forbidden.AddRange(rules);
                return true;
            } else if (TryMatch(line, "^(.*)===>(.*)", out match)) {
                rawUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);

                ItemMatch @using = new ItemMatch(usingItemTypeHint, rawUsingPattern, 0, ignoreCase);
                ItemMatch used = new ItemMatch(usedItemTypeHint, match.Groups[2].Value, 0, ignoreCase);
                IEnumerable<DependencyRule> rulesWithMatchingUsingPattern = _allowed.Where(r => r.MatchesUsingPattern(used)).ToArray(); // make a copy!

                _allowed.AddRange(rulesWithMatchingUsingPattern
                    .Select(tail => new DependencyRule(new DependencyMatch(@using, tail.DependencyPattern, tail.Used), tail.Representation)));
                return true;
            } else {
                throw new ApplicationException("Unexpected rule at " + ruleSourceName + ":" + lineNo);
            }
        }

        private bool TryMatch(string line, string pattern, out Match match) {
            match = Regex.Match(line, pattern);
            return match.Success;
        }

        private IEnumerable<DependencyRule> CreateDependencyRules([CanBeNull] ItemType usingItemTypeHint, [CanBeNull] ItemType usedItemTypeHint,
            [NotNull] string ruleSourceName, int lineNo, [NotNull] string usingPattern, [NotNull] string dependencyPattern, [NotNull] string usedPattern,
            [NotNull] string leftRepresentationPart, [NotNull] string rightRepresentationPart, bool questionableRule, bool ignoreCase) {

            string trimmedUsingPattern = usingPattern.Trim();
            string trimmedDependencyPattern = dependencyPattern.Trim();
            string trimmedUsedPattern = usedPattern.Trim();

            string repString = trimmedUsingPattern + " " + leftRepresentationPart + trimmedDependencyPattern + rightRepresentationPart + trimmedUsedPattern;
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation(ruleSourceName, lineNo, repString, questionableRule);

            var match = new DependencyMatch(usingItemTypeHint, trimmedUsingPattern, trimmedDependencyPattern, usedItemTypeHint, trimmedUsedPattern, ignoreCase);
            var head = new DependencyRule(match, rep);

            var result = new List<DependencyRule> { head };

            if (Log.IsVerboseEnabled) {
                Log.WriteInfo($"Matchers used for checking {repString} ({ruleSourceName}:{lineNo})");
                // TODO: Put into DependencyMatch constructor
                ////Log.WriteInfo("  Using: " + string.Join<IMatcher>(", ", head.Using.Matchers));
                ////Log.WriteInfo("   Used: " + string.Join<IMatcher>(", ", head.Used.Matchers));
            }

            return result;
        }

        [NotNull]
        private static string GetUsingPattern([NotNull] string usingPattern, [NotNull] string previousRawUsingPattern) {
            if (usingPattern == "") {
                usingPattern = previousRawUsingPattern;
            }
            return usingPattern;
        }

        [NotNull]
        public DependencyRuleGroup Combine([NotNull] DependencyRuleGroup other, bool ignoreCase) {
            return new DependencyRuleGroup(_groupPattern, _groupMatchOrNullForMainGroup,
                _allowed.Union(other._allowed),
                _questionable.Union(other._questionable),
                _forbidden.Union(other._forbidden));
        }

        public bool Check([NotNull] IEnumerable<Dependency> dependencies, bool addMarker) {
            bool allOk = true;
            if (IsCheckingGroup) {
                int reorgCount = 0;
                int nextReorg = 200;

                foreach (Dependency d in dependencies) {
                    if (_groupMatchOrNullForMainGroup == null ||
                        _groupMatchOrNullForMainGroup.Matches(d.UsingItem) != null) {

                        Check(d);
                        if (d.BadCt > 0) {
                            allOk = false;
                            if (addMarker) {
                                d.AddMarker(_groupPattern == "" ? "global" : _groupPattern);
                            }
                        }
                        if (++reorgCount > nextReorg) {
                            _forbidden.Sort(_sortOnDescendingHitCount);
                            _allowed.Sort(_sortOnDescendingHitCount);
                            _questionable.Sort(_sortOnDescendingHitCount);
                            nextReorg = 6 * nextReorg / 5 + 200;
                        }
                    }
                }
            } else {
                Log.WriteInfo("No allowed or questionable rules in " + (GroupPattern == "" ? "global group" : "group " + GroupPattern) + ", therefore no checking done for this group.");
            }
            return allOk;
        }

        public bool IsCheckingGroup => _allowed.Any() || _questionable.Any();

        private void Check([NotNull] Dependency d) {
            if (_forbidden.Any(r => r.IsMatch(d))) {
                // First we check for forbidden - "if it is forbidden, it IS forbidden"
                d.MarkAsBad();
            } else if (_allowed.Any(r => r.IsMatch(d))) {
                // Then, we check for allwoed - "if it is not forbidden and allowed, then it IS allowed (and never questionable)"
            } else if (_questionable.Any(r => r.IsMatch(d))) {
                // Last, we check for questionable - "if it is questionable, it is questionable"
                d.MarkAsQuestionable();
            } else {
                // If no rule matches, it is bad!
                d.MarkAsBad();
            }
        }

        [NotNull]
        public IEnumerable<DependencyRule> AllRules => _allowed.Concat(_forbidden).Concat(_questionable);
    }
}