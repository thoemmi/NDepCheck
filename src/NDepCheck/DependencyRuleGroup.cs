using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
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
        private readonly IMatcher[] _groupMatchersOrNullForGlobalRules;
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
            _groupMatchersOrNullForGlobalRules = group == "" ? null : CreateMatchers(groupType, group, 0, ignoreCase);
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

        public bool IsNotEmpty => _allowed.Any() || _questionable.Any() || _forbidden.Any();

        /// <summary>
        /// Add one or more <c>DependencyRules</c>s from a single input line.
        /// public for testability.
        /// </summary>
        public bool AddDependencyRules([NotNull] DependencyRuleSet parent, [CanBeNull] ItemType usingItemType, [CanBeNull] ItemType usedItemType, 
                                       [NotNull] string ruleFileName, int lineNo, [NotNull] string line, bool ignoreCase, string previousRawUsingPattern, out string currentRawUsingPattern) {
            if (usingItemType == null || usedItemType == null) {
                Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleFileName}, dependency rules are ignored", parent.FileIncludeStack, lineNo);
                currentRawUsingPattern = null;
                return false;
            } else if (line.Contains(DependencyRuleSet.MAYUSE)) {
                foreach (var rule in CreateDependencyRules(parent, usingItemType, usedItemType, ruleFileName, lineNo, line, DependencyRuleSet.MAYUSE, false, ignoreCase, previousRawUsingPattern, out currentRawUsingPattern)) {
                    Add(_allowed, rule);
                }
                return true;
            } else if (line.Contains(DependencyRuleSet.MAYUSE_WITH_WARNING)) {
                foreach (var rule in CreateDependencyRules(parent, usingItemType, usedItemType, ruleFileName, lineNo, line, DependencyRuleSet.MAYUSE_WITH_WARNING, true, ignoreCase, previousRawUsingPattern, out currentRawUsingPattern)) {
                    Add(_questionable, rule);
                }
                return true;
            } else if (line.Contains(DependencyRuleSet.MUSTNOTUSE)) {
                foreach (var rule in CreateDependencyRules(parent, usingItemType, usedItemType, ruleFileName, lineNo, line, DependencyRuleSet.MUSTNOTUSE, false, 
                    ignoreCase, previousRawUsingPattern, out currentRawUsingPattern)) {
                    Add(_forbidden, rule);
                }
                return true;
            } else {
                throw new ApplicationException("Unexpected rule at " + ruleFileName + ":" + lineNo);
            }
        }

        private static void Add(List<DependencyRule> ruleList, DependencyRule rule) {
            //if (!ruleList.Any(r => r.IsSameAs(rule))) {
            ruleList.Add(rule);
            //}
        }

        private static IEnumerable<DependencyRule> CreateDependencyRules([NotNull] DependencyRuleSet parent, 
            [NotNull] ItemType usingItemType, [NotNull] ItemType usedItemType, [NotNull] string ruleFileName, int lineNo, 
            [NotNull] string line, [NotNull] string sep, bool questionableRule, bool ignoreCase, string previousRawUsingPattern, out string currentRawUsingPattern) {

            DependencyRuleRepresentation rep = new DependencyRuleRepresentation(ruleFileName, lineNo, line, questionableRule);
            int i = line.IndexOf(sep, StringComparison.Ordinal);

            string rawUsingpattern = line.Substring(0, i).Trim();
            if (rawUsingpattern == "") {
                rawUsingpattern = previousRawUsingPattern;
            }
            currentRawUsingPattern = rawUsingpattern;

            string usingPattern = parent.ExpandDefines(rawUsingpattern);

            string usedPattern = parent.ExpandDefines(line.Substring(i + sep.Length).Trim());
            var rule = new DependencyRule(usingItemType, usingPattern, usedItemType, usedPattern, rep, ignoreCase);

            if (Log.IsChattyEnabled) {
                Log.WriteInfo($"Rules used for checking {line} ({ruleFileName}:{lineNo})");
                Log.WriteInfo("  " + rule);
            }
            return new[] { rule };
        }

        public DependencyRuleGroup Combine([NotNull] DependencyRuleGroup other, bool ignoreCase) {
            return new DependencyRuleGroup(_groupType, _group,
                _allowed.Union(other._allowed),
                _questionable.Union(other._questionable),
                _forbidden.Union(other._forbidden), ignoreCase);
        }

        public bool Check([CanBeNull] IInputContext inputContext, [NotNull] IEnumerable<Dependency> dependencies) {
            bool result = true;
            int reorgCount = 0;
            int nextReorg = 200;

            foreach (Dependency d in dependencies) {
                if (_groupMatchersOrNullForGlobalRules == null || Match(_groupType, _groupMatchersOrNullForGlobalRules, d.UsingItem) != null) {
                    result &= Check(inputContext, d);
                    if (++reorgCount > nextReorg) {
                        _forbidden.Sort(_sortOnDescendingHitCount);
                        _allowed.Sort(_sortOnDescendingHitCount);
                        _questionable.Sort(_sortOnDescendingHitCount);
                        nextReorg = 6 * nextReorg / 5 + 200;
                    }
                }
            }

            return result;
        }

        private bool Check([CanBeNull] IInputContext inputContext, [NotNull] Dependency d) {
            bool ok = false;
            if (Log.IsChattyEnabled) {
                Log.WriteInfo("Checking " + d);
            }
            if (_forbidden.Any(r => r.IsMatch(d))) {
                goto DONE;
            }
            if (_allowed.Any(r => r.IsMatch(d))) {
                ok = true;
                goto DONE;
            }
            if (_questionable.Any(r => r.IsMatch(d))) {
                var ruleViolation = new RuleViolation(d, ViolationType.Warning);
                //Log.WriteViolation(ruleViolation);
                inputContext?.Add(ruleViolation);
                ok = true;
            }
        DONE:
            if (!ok) {
                var ruleViolation = new RuleViolation(d, ViolationType.Error);
                //Log.WriteViolation(ruleViolation);
                inputContext?.Add(ruleViolation);
            }

            d.MarkOkOrNotOk(ok);

            return ok;
        }

        [NotNull]
        public IEnumerable<DependencyRule> AllRules => _allowed.Concat(_forbidden).Concat(_questionable);
    }
}