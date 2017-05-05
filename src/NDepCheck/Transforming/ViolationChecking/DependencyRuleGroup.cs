using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Markers;
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

        [CanBeNull]
        private readonly string _groupMarker;
        [CanBeNull]
        private readonly DependencyMatch _groupMatchOrNullForMainGroup;

        private DependencyRuleGroup([CanBeNull] string defaultName, DependencyMatch groupMatchOrNullForMainGroup,
        [NotNull] IEnumerable<DependencyRule> allowed, [NotNull] IEnumerable<DependencyRule> questionable,
                [NotNull] IEnumerable<DependencyRule> forbidden) {
            _groupMatchOrNullForMainGroup = groupMatchOrNullForMainGroup;
            _groupMarker = groupMatchOrNullForMainGroup == null
                ? null
                : AbstractMarkerSet.CreateReadableDefaultMarker(
                        new[] { groupMatchOrNullForMainGroup.UsingMatch },
                        new[] { groupMatchOrNullForMainGroup.UsedMatch }, defaultName);
            _allowed = allowed.ToList();
            _questionable = questionable.ToList();
            _forbidden = forbidden.ToList();
        }

        public DependencyRuleGroup([NotNull] string groupPattern, bool ignoreCase, ItemType usingTypeHint, ItemType usedTypeHint, string defaultName)
            : this(defaultName, groupPattern == "" ? null : DependencyMatch.Create(groupPattern, ignoreCase, usingTypeHint: usingTypeHint, usedTypeHint: usedTypeHint),
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>()) {
            // empty
        }

        [NotNull]
        public string GroupMarker => _groupMarker ?? "";

        /// <summary>
        /// Add one or more <c>DependencyRules</c>s from a single input line.
        /// public for testability.
        /// </summary>
        public bool AddDependencyRules([CanBeNull] ItemType usingItemTypeHint, [CanBeNull] ItemType usedItemTypeHint,
                                       [NotNull] string ruleSourceName, int lineNo, [NotNull] string line,
                                       bool ignoreCase, [NotNull] string previousRawUsingPattern, [NotNull] out string rawTrimmedUsingPattern) {
            Match match;
            if (TryMatch(line, "^(.*)--(.*)->(.*)", out match)) {
                rawTrimmedUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);
                IEnumerable<DependencyRule> rules = CreateDependencyRules(usingItemTypeHint, usedItemTypeHint, ruleSourceName, lineNo,
                    rawTrimmedUsingPattern, match.Groups[2].Value, match.Groups[3].Value, "--", "->", questionableRule: false, ignoreCase: ignoreCase);
                _allowed.AddRange(rules);
                return true;
            } else if (TryMatch(line, "^(.*)--(.*)-[?](.*)", out match)) {
                rawTrimmedUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);
                IEnumerable<DependencyRule> rules = CreateDependencyRules(usingItemTypeHint, usedItemTypeHint, ruleSourceName, lineNo,
                    rawTrimmedUsingPattern, match.Groups[2].Value, match.Groups[3].Value, "--", "-?", questionableRule: true, ignoreCase: ignoreCase);
                _questionable.AddRange(rules);
                return true;
            } else if (TryMatch(line, "^(.*)--(.*)-!(.*)", out match)) {
                rawTrimmedUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);
                IEnumerable<DependencyRule> rules = CreateDependencyRules(usingItemTypeHint, usedItemTypeHint, ruleSourceName, lineNo,
                    rawTrimmedUsingPattern, match.Groups[2].Value, match.Groups[3].Value, "--", "-!", questionableRule: false, ignoreCase: ignoreCase);
                _forbidden.AddRange(rules);
                return true;
            } else if (TryMatch(line, "^(.*)===>(.*)", out match)) {
                rawTrimmedUsingPattern = GetUsingPattern(match.Groups[1].Value, previousRawUsingPattern);
                string rawUsedPattern = match.Groups[2].Value;
                {
                    ItemMatch @using = new ItemMatch(usingItemTypeHint, rawTrimmedUsingPattern, 0, ignoreCase);
                    ItemMatch used = new ItemMatch(usedItemTypeHint, rawUsedPattern.Trim(), 0, ignoreCase);

                    CopyRulesWithNewUsing(_allowed, used, @using);
                    CopyRulesWithNewUsing(_questionable, used, @using);
                    CopyRulesWithNewUsing(_forbidden, used, @using);
                }

                // using may also use the right side of ===>; in other words, ===> is an implicit --->.
                _allowed.AddRange(CreateDependencyRules(usingItemTypeHint, usedItemTypeHint,
                    ruleSourceName, lineNo, rawTrimmedUsingPattern, "", rawUsedPattern, "==", "=>", questionableRule: false,
                    ignoreCase: ignoreCase));
                return true;
            } else {
                throw new ApplicationException("Unexpected rule at " + ruleSourceName + ":" + lineNo);
            }
        }

        private static void CopyRulesWithNewUsing(List<DependencyRule> rules, ItemMatch used, ItemMatch @using) {
            IEnumerable<DependencyRule> indirectRulesWithMatchingUsingPattern =
                rules.Where(r => r.MatchesUsingPattern(used)).ToArray(); // make a copy!

            rules.AddRange(indirectRulesWithMatchingUsingPattern.Select(
                    tail => new DependencyRule(new DependencyMatch(@using, tail.DependencyPattern, tail.Used), tail.Representation)));
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
            string trimmedUsingPattern = usingPattern.Trim();
            if (trimmedUsingPattern == "") {
                trimmedUsingPattern = previousRawUsingPattern;
            }
            return trimmedUsingPattern;
        }

        [NotNull]
        public DependencyRuleGroup Combine([NotNull] DependencyRuleGroup other, bool ignoreCase) {
            return new DependencyRuleGroup(_groupMarker, _groupMatchOrNullForMainGroup,
                _allowed.Union(other._allowed),
                _questionable.Union(other._questionable),
                _forbidden.Union(other._forbidden));
        }

        public bool Check([NotNull] IEnumerable<Dependency> dependencies, bool addMarker) {
            bool allOk = true;
            int reorgCount = 0;
            int nextReorg = 200;

            bool checkFully = _allowed.Any() || _questionable.Any();

            foreach (Dependency d in dependencies) {
                if (_groupMatchOrNullForMainGroup == null || _groupMatchOrNullForMainGroup.IsMatch(d)) {
                    if (checkFully) {
                        Check(d);
                    } else {
                        CheckBadOnly(d);
                    }
                    if (d.BadCt > 0) {
                        allOk = false;
                        if (addMarker) {
                            d.AddMarker(_groupMarker ?? "global");
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
            return allOk;
        }

        private void Check([NotNull] Dependency d) {
            if (_forbidden.Any(r => r.IsMatch(d))) {
                // First, we check for forbidden rules - "if it is forbidden, it is definitely forbidden"
                d.MarkAsBad();
            } else if (_allowed.Any(r => r.IsMatch(d))) {
                // Then we check for allowed - "if it is not forbidden and allowed, it is definitely allowed"
                // If there is no allowed or questionable rule, then there is an implicit 
            } else if (_questionable.Any(r => r.IsMatch(d))) {
                // Last, we check for questionable - "if it is neither allowed nor forbidden, but questionably allowed, well, so be it"
                d.MarkAsQuestionable();
            } else {
                // If no rule matches, it is bad!
                d.MarkAsBad();
            }
        }

        private void CheckBadOnly([NotNull] Dependency d) {
            if (_forbidden.Any(r => r.IsMatch(d))) {
                // First, we check for forbidden rules - "if it is forbidden, it is definitely forbidden"
                d.MarkAsBad();
            }   // If there is no allowed or questionable rule, then there is an implicit ** ---> ** rule.
        }

        [NotNull]
        public IEnumerable<DependencyRule> AllRules => _allowed.Concat(_forbidden).Concat(_questionable);
    }
}