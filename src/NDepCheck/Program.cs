using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

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

        private class ThreadLoopData {
            public CheckerContext Context { get; set; }
            public int MaxErrorCode { get; set; }
        }

        /// <summary>
        /// Main method. See <c>UsageAndExit</c> for the 
        /// accepted arguments. 
        /// </summary>
        public int Run() {
            System.Text.RegularExpressions.Regex.CacheSize = 1024;

            int returnValue = 0;

            var contexts = new List<IAssemblyContext>();
            Parallel.ForEach(
                _options.Assemblies.SelectMany(filePattern => filePattern.ExpandFilename()).Where(IsAssembly),
                new ParallelOptions { MaxDegreeOfParallelism = _options.MaxCpuCount },
                () => new ThreadLoopData { Context = new CheckerContext(!String.IsNullOrWhiteSpace(_options.XmlOutput)), MaxErrorCode = 0 },
                (assemblyFilename, state, loopData) => {
                    int result = AnalyzeAssembly(loopData.Context, assemblyFilename);
                    loopData.MaxErrorCode = Math.Max(loopData.MaxErrorCode, result);
                    return loopData;
                },
                loopData => {
                    contexts.AddRange(loopData.Context.AssemblyContexts);
                    returnValue = Math.Max(returnValue, loopData.MaxErrorCode);
                });

            if (!String.IsNullOrWhiteSpace(_options.XmlOutput)) {
                WriteXmlOutput(_options.XmlOutput, contexts);
            }

            LogSummary(contexts);

            return returnValue;
        }

        private static void LogSummary(List<IAssemblyContext> contexts) {
            foreach (var context in contexts) {
                string msg = String.Format("{0}: {1} errors, {2} warnings", context.Filename, context.ErrorCount, context.WarningCount);
                if (context.ErrorCount > 0) {
                    Log.WriteError(msg);
                } else if (context.WarningCount > 0) {
                    Log.WriteWarning(msg);
                }
            }
            Log.WriteInfo(String.Format("{0} assemblies are OK.", contexts.Count(ctx => ctx.ErrorCount == 0 && ctx.WarningCount == 0)));
        }

        private static void WriteXmlOutput(string path, IEnumerable<IAssemblyContext> assemblyContexts) {
            var document = new XDocument(
                new XElement("Assemblies",
                    from ctx in assemblyContexts select new XElement("Assembly",
                        new XElement("Filename", ctx.Filename),
                        new XElement("ErrorCount", ctx.ErrorCount),
                        new XElement("WarningCount", ctx.WarningCount),
                        new XElement("Violations",
                            from violation in ctx.RuleViolations select new XElement(
                                "Violation",
                                new XElement("Type", violation.ViolationType),
                                new XElement("UsedItem", violation.Dependency.UsedItem),
                                //new XElement("UsedNamespace", violation.Dependency.UsedNamespace),
                                new XElement("UsingItem", violation.Dependency.UsingItem),
                                //new XElement("UsingNamespace", violation.Dependency.UsingNamespace),
                                new XElement("FileName", violation.Dependency.FileName),
                                new XElement("StartLine", violation.Dependency.StartLine),
                                new XElement("StartColumn", violation.Dependency.StartColumn),
                                new XElement("EndLine", violation.Dependency.EndLine),
                                new XElement("EndColumn", violation.Dependency.EndColumn)
                                ))
                        )
                ));
            var settings = new XmlWriterSettings {
                Encoding = Encoding.UTF8,
                Indent = true
            };
            using (var xmlWriter = XmlWriter.Create(path, settings)) {
                document.Save(xmlWriter);
            }
        }

        private static bool IsAssembly(string filename) {
            string extension = Path.GetExtension(filename).ToLowerInvariant();
            return extension == ".dll" || extension == ".exe";
        }

        private int AnalyzeAssembly(CheckerContext checkerContext, string assemblyFilename) {
            string dependencyFilename = Path.GetFileName(assemblyFilename) + ".dep";
            try {
                Log.WriteInfo("Analyzing " + assemblyFilename);
                using (var assemblyContext = checkerContext.OpenAssemblyContext(Path.GetFileName(assemblyFilename))) {
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
                            IEnumerable<Dependency> dependencies = DependencyReader.GetDependencies(assemblyFilename,
                                createCodeDependencies: !_options.CheckOnlyAssemblyDependencies, 
                                createAssemblyDependencies: ruleSetForAssembly.ContainsAssemblyRule);
                            IEnumerable<DependencyRuleGroup> groups = ruleSetForAssembly.ExtractDependencyGroups();
                            bool success = _checker.Check(assemblyContext, groups, dependencies, _options.ShowUnusedQuestionableRules);
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
    }
}