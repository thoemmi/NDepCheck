using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.ViolationChecking {
    public class DependencyRuleSet {
        [NotNull]
        private readonly IEnumerable<DependencyRuleGroup> _ruleGroups;

        ////[CanBeNull]
        ////public string FullSourceName {
        ////    get;
        ////}

        //[NotNull]
        //public string FileIncludeStack {
        //    get;
        //}

        [NotNull]
        private readonly IEnumerable<DependencyRuleSet> _includedRuleSets;

        public DependencyRuleSet(List<DependencyRuleGroup> ruleGroups,
                                 IEnumerable<DependencyRuleSet> children) {
            _ruleGroups = ruleGroups;
            _includedRuleSets = children;
        }

        internal IEnumerable<DependencyRuleGroup> GetAllDependencyGroups(bool ignoreCase) {
            var result = new Dictionary<string, DependencyRuleGroup>();
            CombineGroupsFromChildren(result, new List<DependencyRuleSet>(), ignoreCase);
            return result.Values;
        }

        private void CombineGroupsFromChildren([NotNull] Dictionary<string, DependencyRuleGroup> result, [NotNull] List<DependencyRuleSet> visited, bool ignoreCase) {
            if (visited.Contains(this)) {
                return;
            }
            visited.Add(this);
            foreach (var g in _ruleGroups) {
                if (result.ContainsKey(g.Group)) {
                    result[g.Group] = result[g.Group].Combine(g, ignoreCase);
                } else {
                    result[g.Group] = g;
                }
            }
            foreach (var includedRuleSet in _includedRuleSets) {
                includedRuleSet.CombineGroupsFromChildren(result, visited, ignoreCase);
            }
        }
    }
}
