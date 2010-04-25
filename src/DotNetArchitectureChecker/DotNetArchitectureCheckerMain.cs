using System;
using System.Collections.Generic;
using System.IO;

namespace DotNetArchitectureChecker {
    /// <remarks>
    /// Main class of DotNetArchitectureChecker.
    /// </remarks>
    public class DotNetArchitectureCheckerMain {
        public static ILogger Logger = new ConsoleLogger();

        // The two "workers".
        private readonly DependencyChecker _checker;
        private readonly DependencyGrapher _grapher;
        private readonly Options _options;

        public DotNetArchitectureCheckerMain(Options options) {
            _options = options;
            _checker = new DependencyChecker(_options);
            _grapher = new DependencyGrapher(_checker, _options);
        }

        #region WriteHelpers

        internal static void WriteError(string msg) {
            Logger.WriteError(msg);
        }

        internal static void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine,
                                        uint endColumn) {
            Logger.WriteError(msg, fileName, startLine, startColumn, endLine, endColumn);
        }

        internal static void WriteWarning(string msg) {
            Logger.WriteWarning(msg);
        }

        internal static void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine,
                                          uint endColumn) {
            Logger.WriteWarning(msg, fileName, startLine, startColumn, endLine, endColumn);
        }

        internal static void WriteInfo(string msg) {
            Logger.WriteInfo(msg);
        }

        internal static void WriteDebug(string msg) {
            Logger.WriteDebug(msg);
        }

        #endregion WriteHelpers
        
        #region Main
        
        /// <summary>
        /// Main method. See <c>UsageAndExit</c> for the 
        /// accepted arguments. 
        /// </summary>
        public int Run() {
            int returnValue = 0;

            foreach (string filePattern in _options.Assemblies) {
                foreach (var assemblyFilename in ExpandFilename(filePattern)) {
                    var dependencyFilename = Path.GetFileName(assemblyFilename) + ".dep";
                    // TODO: @hmmueller: is it ok to remember the highest error code?
                    returnValue = Math.Max(returnValue, AnalyzeAssembly(assemblyFilename, dependencyFilename));
                }
            }

            return returnValue;
        }

        private int AnalyzeAssembly(string assemblyFilename, string dependencyFilename) {
            try {
                WriteInfo("Analyzing " + assemblyFilename);

                DependencyRuleSet ruleSetForAssembly =
                    DependencyRuleSet.Load(dependencyFilename, _options.Directories, _options.Verbose)
                    ?? _options.DefaultRuleSet;
                if (ruleSetForAssembly == null) {
                    WriteError(dependencyFilename +
                               " not found in -d and -s directories, and no default rule set provided by -x");
                    return 6;
                } else {
                    try {
                        IEnumerable<Dependency> dependencies = DependencyReader.GetDependencies(assemblyFilename);
                        bool success = _checker.Check(ruleSetForAssembly, dependencies, _options.ShowUnusedQuestionableRules);
                        if (!success) {
                            return 3;
                        }
                        if (_options.DotFilename != null) {
                            _grapher.Graph(ruleSetForAssembly, dependencies);
                        }
                    } catch (FileNotFoundException ex) {
                        WriteError("Input file " + ex.FileName + " not found");
                        return 4;
                    }
                }
            } catch (FileLoadException ex2) {
                WriteError(ex2.Message);
                return 2;
            }
            return 0;
        }

        private static IEnumerable<string> ExpandFilename(string filename) {
            if (filename.StartsWith("@")) {
                using (TextReader nameFile = new StreamReader(filename.Substring(1))) {
                    for (; ; ) {
                        string name = nameFile.ReadLine();
                        if (name == null) {
                            break;
                        }
                        name = name.Trim();
                        if (name != "") {
                            yield return name;
                        }
                    }
                }
            } else if (filename.Contains("*") || filename.Contains("?")) {
                int sepPos = filename.LastIndexOf(Path.DirectorySeparatorChar);

                string dir = sepPos < 0 ? "." : filename.Substring(0, sepPos);
                string filePattern = sepPos < 0 ? filename : filename.Substring(sepPos + 1);
                foreach (string name in Directory.GetFiles(dir, filePattern)) {
                    yield return name;
                }
            } else if (Directory.Exists(filename)) {
                foreach (string name in Directory.GetFiles(filename, "*.dll")) {
                    yield return name;
                }
                foreach (string name in Directory.GetFiles(filename, "*.exe")) {
                    yield return name;
                }
            } else {
                yield return filename;
            }
        }

        /// <summary>
        /// The static Main method.
        /// </summary>
        public static int Main(string[] args) {
            var options = new Options();
            int result = options.ParseCommandLine(args);
            if (result != 0) {
                return result;
            }

            var main = new DotNetArchitectureCheckerMain(options);
            DateTime start = DateTime.Now;
            try {
                return main.Run();
            } catch (Exception ex) {
                string msg = "Exception occurred: " + ex;
                WriteError(msg);
                if (main._options.Verbose) {
                    WriteError(ex.StackTrace);
                }
                return 5;
            } finally {
                DateTime end = DateTime.Now;
                TimeSpan runtime = end.Subtract(start);
                if (runtime < new TimeSpan(0, 0, 1)) {
                    WriteInfo("DC took " + runtime.Milliseconds + " ms.");
                } else if (runtime < new TimeSpan(0, 1, 0)) {
                    WriteInfo("DC took " + runtime.TotalSeconds + " s.");
                } else if (runtime < new TimeSpan(1, 0, 0)) {
                    WriteInfo("DC took " + runtime.Minutes + " min and " + runtime.Seconds + " s.");
                } else {
                    WriteInfo("DC took " + runtime.TotalHours + " hours.");
                }
            }
        }

        #endregion Main
    }
}