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

        public int CheckDependencies([NotNull]IGlobalContext checkerContext, [NotNull]Options options, out IEnumerable<DependencyRuleGroup> checkedGroups) {
            int result = 0;
            if (_violations == null) {
                // We check only once - an input context can be checked multiple times depending on the command line options.
                _violations = new List<RuleViolation>();

                try {
                    DependencyRuleSet ruleSetForAssembly = GetOrCreateDependencyRuleSetMayBeCalledInParallel(checkerContext, options, "" /*???*/);
                    if (ruleSetForAssembly == null) {
                        checkedGroups = null;
                        Log.WriteError("No rule set found for file " + _filename);
                        if (Log.IsChattyEnabled) {
                            Log.WriteInfo("Looked at " + _filename);
                        }
                        result = 6;
                    } else {
                        try {
                            checkedGroups = ruleSetForAssembly.ExtractDependencyGroups(options.IgnoreCase).Where(g => g.IsNotEmpty).ToArray();
                            if (checkedGroups.Any()) {
                                Log.WriteInfo("Analyzing " + _filename);

                                var sw = new Stopwatch();
                                sw.Start();

                                bool success = CheckDependencies(checkedGroups, _dependencies);

                                sw.Stop();
                                int elapsed = (int) sw.Elapsed.TotalMilliseconds;
                                Log.WriteInfo($"Analyzing {_filename} took {elapsed} ms{(elapsed > 5000 ? " (longer than 5s)" : "")}");

                                if (!success) {
                                    result = 3;
                                }
                            }
                        } catch (FileNotFoundException ex) {
                            checkedGroups = null;
                            Log.WriteError("Input file " + ex.FileName + " not found");
                            result = 4;
                        }
                    }

                } catch (FileLoadException ex2) {
                    checkedGroups = null;
                    Log.WriteError(ex2.Message);
                    result = 2;
                }
            } else {
                checkedGroups = null;
            }
            return result;
        }

        public DependencyRuleSet GetOrCreateDependencyRuleSetMayBeCalledInParallel(IGlobalContext checkerContext, Options options, string fileIncludeStack) {
            string dependencyFilename = Path.GetFileName(_filename) + options.RuleFileExtension;
            return checkerContext.GetOrCreateDependencyRuleSet_MayBeCalledInParallel(options, dependencyFilename, fileIncludeStack);
        }

        /// <summary>
        /// Check all dependencies against dependency rules created
        /// with <c>AddDependencyRules</c>.
        /// </summary>
        /// <returns>true if no dependencies is illegal according to our rules</returns>
        public bool CheckDependencies(IEnumerable<DependencyRuleGroup> groups, IEnumerable<Dependency> dependencies) {
            return groups.All(g => g.Check(this, dependencies));
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