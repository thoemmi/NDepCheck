using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.ViolationChecking {
    public class DependencyRuleSet {
        [NotNull]
        private readonly IEnumerable<DependencyRuleGroup> _ruleGroups;

        [NotNull]
        private readonly IEnumerable<DependencyRuleSet> _includedRuleSets;

        public DependencyRuleSet(List<DependencyRuleGroup> ruleGroups,
                                 IEnumerable<DependencyRuleSet> children) {
            _ruleGroups = ruleGroups;
            _includedRuleSets = children;
        }

        internal IEnumerable<DependencyRuleGroup> GetAllDependencyGroupsWithRules(bool ignoreCase) {
            var result = new Dictionary<string, DependencyRuleGroup>();
            CombineGroupsFromChildren(result, new List<DependencyRuleSet>(), ignoreCase);
            return result.Values.Where(g => g.AllRules.Any());
        }

        private void CombineGroupsFromChildren([NotNull] Dictionary<string, DependencyRuleGroup> result, [NotNull] List<DependencyRuleSet> visited, bool ignoreCase) {
            if (visited.Contains(this)) {
                return;
            }
            visited.Add(this);
            foreach (var g in _ruleGroups) {
                if (result.ContainsKey(g.GroupMarker)) {
                    result[g.GroupMarker] = result[g.GroupMarker].Combine(g, ignoreCase);
                } else {
                    result[g.GroupMarker] = g;
                }
            }
            foreach (var includedRuleSet in _includedRuleSets) {
                includedRuleSet.CombineGroupsFromChildren(result, visited, ignoreCase);
            }
        }
    }
}
