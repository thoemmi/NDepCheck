// (c) HMMüller 2006...2010

using System.Collections.Generic;

namespace NDepCheck {
    /// <remarks>
    /// Class doing all the dependency checking. To do this,
    /// it contains lists of allowed and forbidden
    /// <see>DependencyRule</see>s.
    /// </remarks>
    public class DependencyChecker {
        private readonly List<DependencyRuleRepresentation> _representations = new List<DependencyRuleRepresentation>();

        /// <summary>
        /// Check all dependencies against dependency rules created
        /// with <c>AddDependencyRules</c>.
        /// </summary>
        /// <returns>true if no dependencies is illegal according to our rules</returns>
        public bool Check(IEnumerable<DependencyRuleGroup> groups, IEnumerable<Dependency> dependencies,
                          bool showUnusedQuestionableRules, bool emitQuestionableDependenciesToErrorLog) {
            bool result = true;
            foreach (var g in groups) {
                result &= g.Check(dependencies, emitQuestionableDependenciesToErrorLog);
            }
            foreach (DependencyRuleRepresentation r in _representations) {
                if (showUnusedQuestionableRules && r.IsQuestionableRule && !r.WasHit) {
                    Log.WriteInfo("Questionable rule " + r + " was never matched - maybe you can remove it!");
                } else {
                    if (Log.IsVerboseEnabled) {
                        Log.WriteInfo("Rule " + r + " was hit " + r.HitCount + " times.");
                    }
                }
            }
            return result;
        }
    }
}