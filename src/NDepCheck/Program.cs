using System;
using System.Collections.Generic;
using System.IO;

namespace NDepCheck {
    /// <remarks>
    /// Main class of NDepCheck.
    /// </remarks>
    public class Program {
        // The two "workers".
        private readonly DependencyChecker _checker;
        private readonly DependencyGrapher _grapher;
        private readonly Options _options;

        public Program(Options options) {
            _options = options;
            _checker = new DependencyChecker();
            _grapher = new DependencyGrapher(_checker, _options);
        }

        #region Main
        /// <summary>
        /// Main method. See <c>UsageAndExit</c> for the 
        /// accepted arguments. 
        /// </summary>
        public int Run() {
            System.Text.RegularExpressions.Regex.CacheSize = 1024;

            int returnValue = 0;

            foreach (var filePattern in _options.Assemblies) {
                foreach (var assemblyFilename in filePattern.ExpandFilename()) {
                    string extension = Path.GetExtension(assemblyFilename).ToLowerInvariant();
                    // Only DLLs and EXEs are checked - everything else is ignored (e.g. PDBs).
                    if (extension == ".dll" | extension == ".exe") {
                        // We remember just the highest error code - the specific errors are in the output.
                        returnValue = Math.Max(returnValue, AnalyzeAssembly(assemblyFilename));
                    }
                }
            }

            return returnValue;
        }

        private int AnalyzeAssembly(string assemblyFilename) {
            var dependencyFilename = Path.GetFileName(assemblyFilename) + ".dep";
            try {
                Log.WriteInfo("Analyzing " + assemblyFilename);
                Log.StartProcessingAssembly(Path.GetFileName(assemblyFilename));

                DependencyRuleSet ruleSetForAssembly =
                    DependencyRuleSet.Load(dependencyFilename, _options.Directories)
                    ?? _options.DefaultRuleSet;
                if (ruleSetForAssembly == null) {
                    Log.WriteError(dependencyFilename +
                               " not found in -d and -s directories, and no default rule set provided by -x");
                    return 6;
                } else {
                    try {
                       IEnumerable<Dependency> dependencies = DependencyReader.GetDependencies(assemblyFilename);
                        IEnumerable<DependencyRuleGroup> groups = ruleSetForAssembly.ExtractDependencyGroups();
                        bool success = _checker.Check(groups, dependencies, _options.ShowUnusedQuestionableRules, _options.EmitQuestionableDependenciesToErrorLog);
                        if (!success) {
                            return 3;
                        }
                        if (_options.DotFilename != null) {
                            _grapher.Graph(ruleSetForAssembly, dependencies);
                        }
                    } catch (FileNotFoundException ex) {
                        Log.WriteError("Input file " + ex.FileName + " not found");
                        return 4;
                    }
                }
            } catch (FileLoadException ex2) {
                Log.WriteError(ex2.Message);
                return 2;
            }
            return 0;
        }

        /// <summary>
        /// The static Main method.
        /// </summary>
        public static int Main(string[] args) {
            Log.Logger = new ConsoleLogger();

            var options = new Options();
            DateTime start = DateTime.Now;
            
            try {
                int result = options.ParseCommandLine(args);
                if (result != 0) {
                    return result;
                }
                Log.IsDebugEnabled = options.Debug;
                Log.IsVerboseEnabled = options.Verbose;
                var main = new Program(options);
                return main.Run();
            } catch (Exception ex) {
                string msg = "Exception occurred: " + ex;
                Log.WriteError(msg);
                if (options.Verbose) {
                    Log.WriteError(ex.StackTrace);
                }
                return 5;
            } finally {
                DateTime end = DateTime.Now;
                TimeSpan runtime = end.Subtract(start);
                const string nDepCheckTook = "NDepCheck took ";
                if (runtime < new TimeSpan(0, 0, 1)) {
                    Log.WriteInfo(nDepCheckTook + runtime.Milliseconds + " ms.");
                } else if (runtime < new TimeSpan(0, 1, 0)) {
                    Log.WriteInfo(nDepCheckTook + runtime.TotalSeconds + " s.");
                } else if (runtime < new TimeSpan(1, 0, 0)) {
                    Log.WriteInfo(nDepCheckTook + runtime.Minutes + " min and " + runtime.Seconds + " s.");
                } else {
                    Log.WriteInfo(nDepCheckTook + runtime.TotalHours + " hours.");
                }
            }
        }

        #endregion Main
    }
}