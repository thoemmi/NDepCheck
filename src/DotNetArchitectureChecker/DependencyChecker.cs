// (c) HMMüller 2006...2010

using System;
using System.Collections.Generic;

namespace DotNetArchitectureChecker {
    /// <remarks>
    /// Class doing all the dependency checking. To do this,
    /// it contains lists of allowed and forbidden
    /// <see>DependencyRule</see>s.
    /// </remarks>
    public class DependencyChecker {
        private readonly List<DependencyRuleRepresentation> _representations = new List<DependencyRuleRepresentation>();
        private readonly Options _options;

        public DependencyChecker(Options options) {
            _options = options;
        }

        ///// <summary>
        ///// Add one or more <c>DependencyRule</c>s from a single input
        ///// line (with ---> or ---!).
        ///// </summary>
        ///// <value>
        ///// Are there any positive rules defined? - used
        ///// in main program to decide whether to proceed
        ///// with dependency checking.
        ///// </value>
        //public bool NoRulesDefined {
        //    get {
        //        return _allowed.Count == 0;
        //    }
        //}

        /// <summary>
        /// Check all dependencies against dependency rules created
        /// with <c>AddDependencyRules</c>.
        /// </summary>
        /// <returns>true if no dependencies is illegal according to our rules</returns>
        private bool Check(List<DependencyRule> allowed,
                           List<DependencyRule> questionable,
                           List<DependencyRule> forbidden,
                           IEnumerable<Dependency> dependencies, 
                           bool showUnusedQuestionableRules) {
            bool result = true;
            int reorgCount = 0;
            foreach (Dependency d in dependencies) {
                result &= Check(allowed, questionable, forbidden, d);
                if (++reorgCount%256 == 0) {
                    Comparison<DependencyRule> sortOnDescendingHitCount = (r1, r2) => r2.HitCount - r1.HitCount;
                    forbidden.Sort(sortOnDescendingHitCount);
                    allowed.Sort(sortOnDescendingHitCount);
                    questionable.Sort(sortOnDescendingHitCount);
                }
            }
            foreach (DependencyRuleRepresentation r in _representations) {
                if (showUnusedQuestionableRules && r.IsQuestionableRule && !r.WasHit) {
                    DotNetArchitectureCheckerMain.WriteInfo("Questionable rule " + r + " was never matched - maybe you can remove it!");
                } else {
                    if (_options.Verbose) {
                        DotNetArchitectureCheckerMain.WriteInfo("Rule " + r + " was hit " + r.HitCount + " times.");
                    }
                }
            }

            return result;
        }

        internal bool Check(List<DependencyRule> allowed,
                            List<DependencyRule> questionable,
                            List<DependencyRule> forbidden,
                            Dependency d) {
            bool ok = false;
            if (_options.Verbose) {
                DotNetArchitectureCheckerMain.WriteInfo("Checking " + d);
            }
            foreach (DependencyRule r in forbidden) {
                if (r.Matches(d, _options.Debug)) {
                    goto DONE;
                }
            }
            foreach (DependencyRule r in allowed) {
                if (r.Matches(d, _options.Debug)) {
                    ok = true;
                    goto DONE;
                }
            }
            foreach (DependencyRule r in questionable) {
                if (r.Matches(d, _options.Debug)) {
                    DotNetArchitectureCheckerMain.WriteWarning("Dependency " + d + " is questionable", d.FileName, d.StartLine,
                                                       d.StartColumn, d.EndLine, d.EndColumn);
                    ok = true;
                    goto DONE;
                }
            }
            DONE:
            //d.IsOk = ok;
            if (!ok) {
                DotNetArchitectureCheckerMain.WriteError(d.IllegalMessage(), d.FileName, d.StartLine, d.StartColumn, d.EndLine, d.EndColumn);
            }
            return ok;
        }

        public bool Check(DependencyRuleSet ruleSet, IEnumerable<Dependency> dependencies, bool showUnusedQuestionableRules) {
            var allowed = new List<DependencyRule>();
            var questionable = new List<DependencyRule>();
            var forbidden = new List<DependencyRule>();
            ruleSet.ExtractDependencyRules(allowed, questionable, forbidden);
            return Check(allowed, questionable, forbidden, dependencies, showUnusedQuestionableRules);
        }
    }
}