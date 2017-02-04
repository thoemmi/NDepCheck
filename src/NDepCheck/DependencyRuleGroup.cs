using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck {
    public class DependencyRuleGroup : Pattern {
        private static readonly Comparison<DependencyRule> _sortOnDescendingHitCount = (r1, r2) => r2.HitCount - r1.HitCount;

        private readonly List<DependencyRule> _allowed;
        private readonly List<DependencyRule> _questionable;
        private readonly List<DependencyRule> _forbidden;

        private readonly string _group;
        private readonly IMatcher[] _groupMatchersOrNullForGlobalRules;
        private readonly ItemType _groupType;

        private DependencyRuleGroup(ItemType groupType, string group, IEnumerable<DependencyRule> allowed,
                IEnumerable<DependencyRule> questionable, IEnumerable<DependencyRule> forbidden,
                bool ignoreCase) {
            if (groupType == null && group != "") {
                throw new ArgumentException("groupType is null, but group is not not empty", nameof(groupType));
            }

            _groupType = groupType;
            _group = group;
            _groupMatchersOrNullForGlobalRules = group == "" ? null : CreateMatchers(groupType, group, 0, ignoreCase);
            _allowed = allowed.ToList();
            _questionable = questionable.ToList();
            _forbidden = forbidden.ToList();
        }

        public DependencyRuleGroup(ItemType groupType, string group, bool ignoreCase)
            : this(groupType, group,
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(), ignoreCase) {
            // empty
        }

        public string Group => _group;

        public bool IsNotEmpty => _allowed.Any() || _questionable.Any() || _forbidden.Any();

        /// <summary>
        /// Add one or more <c>DependencyRules</c>s from a single input
        /// line.
        /// public for testability.
        /// </summary>
        public void AddDependencyRules(DependencyRuleSet parent, ItemType usingItemType, ItemType usedItemType, string ruleFileName, int lineNo, string line, bool ignoreCase) {
            if (usingItemType == null || usedItemType == null) {
                Log.WriteError("Itemtypes not defined - $ line is missing in this file, dependency rules are ignored", ruleFileName, lineNo);
            } else if (line.Contains(DependencyRuleSet.MAYUSE)) {
                foreach (var rule in CreateDependencyRules(parent, usingItemType, usedItemType, ruleFileName, lineNo, line, DependencyRuleSet.MAYUSE, false, ignoreCase)) {
                    Add(_allowed, rule);
                }
            } else if (line.Contains(DependencyRuleSet.MAYUSE_WITH_WARNING)) {
                foreach (var rule in CreateDependencyRules(parent, usingItemType, usedItemType, ruleFileName, lineNo, line, DependencyRuleSet.MAYUSE_WITH_WARNING, true, ignoreCase)) {
                    Add(_questionable, rule);
                }
            } else if (line.Contains(DependencyRuleSet.MUSTNOTUSE)) {
                foreach (var rule in CreateDependencyRules(parent, usingItemType, usedItemType, ruleFileName, lineNo, line, DependencyRuleSet.MUSTNOTUSE, false, ignoreCase)) {
                    Add(_forbidden, rule);
                }
            } else {
                throw new ApplicationException("Unexpected rule at " + ruleFileName + ":" + lineNo);
            }
        }

        private static void Add(List<DependencyRule> ruleList, DependencyRule rule) {
            //if (!ruleList.Any(r => r.IsSameAs(rule))) {
            ruleList.Add(rule);
            //}
        }

        private static IEnumerable<DependencyRule> CreateDependencyRules(DependencyRuleSet parent, ItemType usingItemType, ItemType usedItemType, string ruleFileName, int lineNo, string line, string sep, bool questionableRule, bool ignoreCase) {
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation(ruleFileName, lineNo, line, questionableRule);
            int i = line.IndexOf(sep, StringComparison.Ordinal);
            string usingPattern = parent.ExpandDefines(line.Substring(0, i).Trim());
            string usedPattern = parent.ExpandDefines(line.Substring(i + sep.Length).Trim());
            var rule = new DependencyRule(usingItemType, usingPattern, usedItemType, usedPattern, rep, ignoreCase);

            if (Log.IsChattyEnabled) {
                Log.WriteInfo($"Rules used for checking {line} ({ruleFileName}:{lineNo})");
                Log.WriteInfo("  " + rule);
            }
            return new[] { rule };
        }

        public DependencyRuleGroup Combine(DependencyRuleGroup other, bool ignoreCase) {
            return new DependencyRuleGroup(_groupType, _group,
                _allowed.Union(other._allowed),
                _questionable.Union(other._questionable),
                _forbidden.Union(other._forbidden), ignoreCase);
        }

        public bool Check(IInputContext inputContext, IEnumerable<Dependency> dependencies) {
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

        private bool Check(IInputContext inputContext, Dependency d) {
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

        public IEnumerable<DependencyRule> AllRules => _allowed.Concat(_forbidden).Concat(_questionable);
    }
}