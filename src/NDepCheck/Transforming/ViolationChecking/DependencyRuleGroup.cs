using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.ViolationChecking {
    public class DependencyRuleGroup : Pattern {
        private static readonly Comparison<DependencyRule> _sortOnDescendingHitCount = (r1, r2) => r2.HitCount - r1.HitCount;

        [NotNull]
        private readonly List<DependencyRule> _allowed;
        [NotNull]
        private readonly List<DependencyRule> _questionable;
        [NotNull]
        private readonly List<DependencyRule> _forbidden;

        [NotNull]
        private readonly string _group;
        [CanBeNull]
        private readonly IMatcher[] _groupMatchersOrNullForMainGroup;
        [NotNull]
        private readonly ItemType _groupType;

        private DependencyRuleGroup([NotNull] ItemType groupType, [NotNull] string group, [NotNull] IEnumerable<DependencyRule> allowed,
                [NotNull] IEnumerable<DependencyRule> questionable, [NotNull] IEnumerable<DependencyRule> forbidden,
                bool ignoreCase) {
            if (groupType == null && group != "") {
                throw new ArgumentException("groupType is null, but group is not empty", nameof(groupType));
            }

            _groupType = groupType;
            _group = group;
            _groupMatchersOrNullForMainGroup = group == "" ? null : CreateMatchers(groupType, group, 0, ignoreCase);
            _allowed = allowed.ToList();
            _questionable = questionable.ToList();
            _forbidden = forbidden.ToList();
        }

        public DependencyRuleGroup([NotNull] ItemType groupType, [NotNull] string group, bool ignoreCase)
            : this(groupType, group,
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(), ignoreCase) {
            // empty
        }

        [NotNull]
        public string Group => _group;

        /// <summary>
        /// Add one or more <c>DependencyRules</c>s from a single input line.
        /// public for testability.
        /// </summary>
        public bool AddDependencyRules([NotNull] ItemType usingItemType, [NotNull] ItemType usedItemType,
                                       [NotNull] string ruleSourceName, int lineNo, [NotNull] string line, bool ignoreCase, string previousRawUsingPattern, out string rawUsingPattern) {
            if (line.Contains(ViolationChecking.CheckDeps.MAY_USE)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(usingItemType, usedItemType, ruleSourceName, lineNo, line,
                    ViolationChecking.CheckDeps.MAY_USE, false, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _allowed.AddRange(rules);
                return true;
            } else if (line.Contains(ViolationChecking.CheckDeps.MAY_USE_RECURSIVE)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(usingItemType, usedItemType, ruleSourceName, lineNo, line,
                                                           ViolationChecking.CheckDeps.MAY_USE_RECURSIVE, false, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _allowed.AddRange(rules);
                return true;
            } else if (line.Contains(ViolationChecking.CheckDeps.MAY_USE_WITH_WARNING)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(usingItemType, usedItemType, ruleSourceName, lineNo, line,
                    ViolationChecking.CheckDeps.MAY_USE_WITH_WARNING, true, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _questionable.AddRange(rules);
                return true;
            } else if (line.Contains(ViolationChecking.CheckDeps.MUST_NOT_USE)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(usingItemType, usedItemType, ruleSourceName, lineNo, line,
                                                           ViolationChecking.CheckDeps.MUST_NOT_USE, false, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _forbidden.AddRange(rules);
                return true;
            } else {
                throw new ApplicationException("Unexpected rule at " + ruleSourceName + ":" + lineNo);
            }
        }

        private IEnumerable<DependencyRule> CreateDependencyRule([NotNull] ItemType usingItemType, [NotNull] ItemType usedItemType, [NotNull] string ruleSourceName, int lineNo,
            [NotNull] string line, [NotNull] string use, bool questionableRule, bool ignoreCase, 
            string previousRawUsingPattern, out string currentRawUsingPattern) {

            int i = line.IndexOf(use, StringComparison.Ordinal);

            string rawUsingpattern = line.Substring(0, i).Trim();
            if (rawUsingpattern == "") {
                rawUsingpattern = previousRawUsingPattern;
            }
            currentRawUsingPattern = rawUsingpattern;

            string usingPattern = rawUsingpattern;

            string rawUsedPattern = line.Substring(i + use.Length).Trim();
            string usedPattern = rawUsedPattern;

            string repString = rawUsingpattern + " " + use + " " + rawUsedPattern;
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation(ruleSourceName, lineNo, repString, questionableRule);

            var head = new DependencyRule(usingItemType, usingPattern, usedItemType, usedPattern, rep, ignoreCase);
            var result = new List<DependencyRule> { head };

            if (Log.IsVerboseEnabled) {
                Log.WriteInfo($"Matchers used for checking {repString} ({ruleSourceName}:{lineNo})");
                Log.WriteInfo("  Using: " + string.Join<IMatcher>(", ", head.Using));
                Log.WriteInfo("   Used: " + string.Join<IMatcher>(", ", head.Used));
            }

            if (use == ViolationChecking.CheckDeps.MAY_USE_RECURSIVE) {
                IEnumerable<DependencyRule> rulesWithMatchingUsingPattern = _allowed.Where(r => r.MatchesUsingPattern(head.Used));

                result.AddRange(rulesWithMatchingUsingPattern.Select(tail => new DependencyRule(usingItemType, head.Using, usedItemType, tail.Used, rep)));
            }

            return result;
        }

        public DependencyRuleGroup Combine([NotNull] DependencyRuleGroup other, bool ignoreCase) {
            return new DependencyRuleGroup(_groupType, _group,
                _allowed.Union(other._allowed),
                _questionable.Union(other._questionable),
                _forbidden.Union(other._forbidden), ignoreCase);
        }

        public bool Check([NotNull] IEnumerable<Dependency> dependencies) {
            int reorgCount = 0;
            int nextReorg = 200;
            bool allOk = true;

            foreach (Dependency d in dependencies) {
                if (_groupMatchersOrNullForMainGroup == null || Match(_groupType, _groupMatchersOrNullForMainGroup, d.UsingItem) != null) {
                    Check(d);
                    allOk &= d.BadCt == 0;
                    if (++reorgCount > nextReorg) {
                        _forbidden.Sort(_sortOnDescendingHitCount);
                        _allowed.Sort(_sortOnDescendingHitCount);
                        _questionable.Sort(_sortOnDescendingHitCount);
                        nextReorg = 6 * nextReorg / 5 + 200;
                    }
                }
            }
            return allOk;
        }

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