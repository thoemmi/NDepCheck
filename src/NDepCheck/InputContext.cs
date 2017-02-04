using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    internal class InputContext : IInputContext {
        [NotNull]
        private readonly string _filename;
        [NotNull]
        private readonly Dependency[] _dependencies;

        private List<RuleViolation> _violations;
        private int _warningCt;
        private int _errorCt;

        public InputContext([NotNull]string filename, [NotNull]Dependency[] dependencies) {
            _filename = filename;
            _dependencies = dependencies;
        }

        public int CheckDependencies([NotNull]IGlobalContext checkerContext, [NotNull]Options options) {
            int result = 0;
            if (_violations == null) {
                _violations = new List<RuleViolation>();

                try {
                    DependencyRuleSet ruleSetForAssembly = GetOrCreateDependencyRuleSetMayBeCalledInParallel(checkerContext, options);
                    if (ruleSetForAssembly == null) {
                        Log.WriteError("No rule set found for file " + _filename);
                        result = 6;
                    } else {
                        try {
                            IEnumerable<DependencyRuleGroup> groups = ruleSetForAssembly.ExtractDependencyGroups(options.IgnoreCase).Where(g => g.IsNotEmpty).ToArray();
                            if (groups.Any()) {
                                Log.WriteInfo("Analyzing " + _filename);

                                var sw = new Stopwatch();
                                sw.Start();
                                
                                bool success = CheckDependencies(groups, _dependencies, options.ShowUnusedQuestionableRules, options.ShowUnusedRules);

                                sw.Stop();
                                int elapsed = (int) sw.Elapsed.TotalMilliseconds;
                                Log.WriteInfo($"Analyzing {_filename} took {elapsed} ms{(elapsed > 5000 ? " (longer than 5s)" : "")}");

                                if (!success) {
                                    result = 3;
                                }
                            }
                        } catch (FileNotFoundException ex) {
                            Log.WriteError("Input file " + ex.FileName + " not found");
                            result = 4;
                        }
                    }

                } catch (FileLoadException ex2) {
                    Log.WriteError(ex2.Message);
                    result = 2;
                }

            }
            return result;
        }

        public DependencyRuleSet GetOrCreateDependencyRuleSetMayBeCalledInParallel(IGlobalContext checkerContext, Options options) {
            string dependencyFilename = Path.GetFileName(_filename) + ".dep";
            return checkerContext.GetOrCreateDependencyRuleSet_MayBeCalledInParallel(options, dependencyFilename);
        }

        /// <summary>
        /// Check all dependencies against dependency rules created
        /// with <c>AddDependencyRules</c>.
        /// </summary>
        /// <returns>true if no dependencies is illegal according to our rules</returns>
        public bool CheckDependencies(IEnumerable<DependencyRuleGroup> groups, IEnumerable<Dependency> dependencies, bool showUnusedQuestionableRules, bool showUnusedRules) {
            bool result = true;
            foreach (var g in groups) {
                result &= g.Check(this, dependencies);
            }
            foreach (var r in groups.SelectMany(g => g.AllRules).Select(r => r.Representation).Distinct().OrderBy(r => r.RuleFileName).ThenBy(r => r.LineNo)) {
                if (showUnusedQuestionableRules && r.IsQuestionableRule && !r.WasHit) {
                    Log.WriteInfo("Questionable rule " + r + " was never matched - maybe you can remove it!");
                } else if (showUnusedRules && !r.WasHit) {
                    Log.WriteInfo("Questionable rule " + r + " was never matched - maybe you can remove it!");
                } else {
                    if (Log.IsChattyEnabled) {
                        Log.WriteInfo("Rule " + r + " was hit " + r.HitCount + " times.");
                    }
                }
            }
            return result;
        }

        public void Add(RuleViolation ruleViolation) {
            _violations.Add(ruleViolation);
            switch (ruleViolation.ViolationType) {
                case ViolationType.Warning:
                    _warningCt++;
                    break;
                case ViolationType.Error:
                    _errorCt++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ruleViolation), "Internal error - unknown ViolationType " + ruleViolation.ViolationType);
            }
        }

        public IEnumerable<RuleViolation> RuleViolations => _violations;

        public string Filename => _filename;

        public int ErrorCount => _errorCt;

        public int WarningCount => _warningCt;

        public IEnumerable<Dependency> Dependencies => _dependencies;
    }
}