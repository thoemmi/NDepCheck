using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        private class ThreadData {
            public CheckerContext Context { get; set; }
            public int MaxErrorCode { get; set; }
        }

        #region Main
        /// <summary>
        /// Main method. See <c>UsageAndExit</c> for the 
        /// accepted arguments. 
        /// </summary>
        public int Run() {
            System.Text.RegularExpressions.Regex.CacheSize = 1024;

            int returnValue = 0;

            int maxDegree = Math.Max(1, _options.MaxCpuCount);
            Parallel.ForEach(
                _options.Assemblies.SelectMany(filePattern => filePattern.ExpandFilename()).Where(IsAssembly),
                new ParallelOptions { MaxDegreeOfParallelism = maxDegree },
                () => new ThreadData { Context = new CheckerContext(), MaxErrorCode = 0 },
                (assemblyFilename, state, loopData) => {
                    var result = AnalyzeAssembly(loopData.Context, assemblyFilename);
                    loopData.MaxErrorCode = Math.Max(loopData.MaxErrorCode, result);
                    return loopData;
                },
                loopData => returnValue = Math.Max(returnValue, loopData.MaxErrorCode)
                );

            return returnValue;
        }

        private static bool IsAssembly(string filename) {
            var extension = Path.GetExtension(filename).ToLowerInvariant();
            return extension == ".dll" || extension == ".exe";
        }

        private int AnalyzeAssembly(CheckerContext checkerContext, string assemblyFilename) {
            var dependencyFilename = Path.GetFileName(assemblyFilename) + ".dep";
            try {
                Log.WriteInfo("Analyzing " + assemblyFilename);
                Log.StartProcessingAssembly(Path.GetFileName(assemblyFilename));

                DependencyRuleSet ruleSetForAssembly = checkerContext.Load(dependencyFilename, _options.Directories);
                if (ruleSetForAssembly == null && !String.IsNullOrEmpty(_options.DefaultRuleSetFile)) {
                    ruleSetForAssembly = checkerContext.Create(new DirectoryInfo("."), _options.DefaultRuleSetFile);
                }
                if (ruleSetForAssembly == null) {
                    Log.WriteError(dependencyFilename +
                               " not found in -d and -s directories, and no default rule set provided by -x");
                    return 6;
                } else {
                    try {
                       IEnumerable<Dependency> dependencies = DependencyReader.GetDependencies(assemblyFilename);
                        IEnumerable<DependencyRuleGroup> groups = ruleSetForAssembly.ExtractDependencyGroups();
                        bool success = _checker.Check(groups, dependencies, _options.ShowUnusedQuestionableRules);
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
                if (runtime < new TimeSpan(0, 0, 1)) {
                    Log.WriteInfo("DC took " + runtime.Milliseconds + " ms.");
                } else if (runtime < new TimeSpan(0, 1, 0)) {
                    Log.WriteInfo("DC took " + runtime.TotalSeconds + " s.");
                } else if (runtime < new TimeSpan(1, 0, 0)) {
                    Log.WriteInfo("DC took " + runtime.Minutes + " min and " + runtime.Seconds + " s.");
                } else {
                    Log.WriteInfo("DC took " + runtime.TotalHours + " hours.");
                }
            }
        }

        #endregion Main
    }
}