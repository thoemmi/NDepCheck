using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.GraphTransformations;

namespace NDepCheck {
    public class GlobalContext : IGlobalContext {
        [NotNull]
        private readonly ConcurrentDictionary<string, DependencyRuleSet> _fullFilename2RulesetCache = new ConcurrentDictionary<string, DependencyRuleSet>();
        [NotNull]
        private readonly List<InputContext> _inputContexts = new List<InputContext>();
        [CanBeNull]
        private IEnumerable<Dependency> _reducedGraph;
        [NotNull]
        private readonly Program _program;

        public GlobalContext([NotNull] Program program) {
            _program = program;
        }

        [NotNull]
        public IEnumerable<IInputContext> InputContexts => _inputContexts;

        [NotNull]
        public GlobalContext ReadAll([NotNull] Options options) {
            IEnumerable<AbstractDependencyReader> allReaders = options.InputFiles.SelectMany(i => i.CreateOrGetReaders(options, false)).OrderBy(r => r.FileName);
            foreach (var r in allReaders) {
                Dependency[] dependencies = r.ReadOrGetDependencies(0);
                if (dependencies.Any()) {
                    _inputContexts.Add(new InputContext(r.FileName, dependencies));
                } else {
                    Log.WriteWarning("No dependencies found in " + r.FileName);
                }
                // edges in input have changed - we need to re-reduce the graph!
                _reducedGraph = null;
            }
            return this;
        }

        [NotNull]
        public GlobalContext ReduceGraph([NotNull] Options options, bool alsoComputeViolations) {
            if (alsoComputeViolations) {
                int checkResult = ComputeViolations(options);
                if (checkResult != 0) {
                    Log.WriteWarning("Checking before reduction yielded return code " + checkResult);
                }
            }

            if (_reducedGraph == null) {
                _reducedGraph = DependencyGrapher.ReduceGraph(this, options);
            }
            return this;
        }

        [NotNull]
        public GlobalContext WriteDotFile([NotNull] Options options, [NotNull] string filename) {
            options.GraphingDone = true;
            using (var output = new StreamWriter(filename)) {
                Log.WriteInfo("Writing dot file " + filename);
                DependencyGrapher.WriteDotFile(_reducedGraph, output, options.StringLength);
            }
            return this;
        }

        [NotNull]
        public GlobalContext WriteMatrixFile([NotNull] Options options, char format, [NotNull] string filename) {
            using (var output = new StreamWriter(filename)) {
                Log.WriteInfo("Sorting graph and writing to " + filename);

                bool withNotOkCount = InputContexts.Any(c => c.RuleViolations.Any());

                DependencyGrapher.WriteMatrixFile(_reducedGraph, output, format, options.StringLength, withNotOkCount);
            }
            return this;
        }

        public int ComputeViolations([NotNull] Options options) {
            options.CheckingDone = true;
            int result = 0;

            var allCheckedGroups = new HashSet<DependencyRuleGroup>();

            foreach (var g in _inputContexts) {
                IEnumerable<DependencyRuleGroup> checkedGroups;
                int checkResult = g.CheckDependencies(this, options, out checkedGroups);
                if (checkResult != 0) {
                    if (result == 0) {
                        result = checkResult;
                    }
                } else {
                    allCheckedGroups.UnionWith(checkedGroups);
                }
            }

            foreach (var r in allCheckedGroups.SelectMany(g => g.AllRules).Select(r => r.Representation).Distinct().OrderBy(r => r.RuleFileName).ThenBy(r => r.LineNo)) {
                if (options.ShowUnusedQuestionableRules && r.IsQuestionableRule && !r.WasHit) {
                    Log.WriteInfo("Questionable rule " + r + " was never matched - maybe you can remove it!");
                } else if (options.ShowUnusedRules && !r.WasHit) {
                    Log.WriteInfo("Rule " + r + " was never matched - maybe you can remove it!");
                } else {
                    if (Log.IsChattyEnabled) {
                        Log.WriteInfo("Rule " + r + " was hit " + r.HitCount + " times.");
                    }
                }
            }

            return result;
        }

        [NotNull]
        public GlobalContext WriteViolations([CanBeNull] string xmlfileOrNullForLog) {
            if (xmlfileOrNullForLog == null) {
                foreach (var c in InputContexts) {
                    foreach (var v in c.RuleViolations) {
                        Log.WriteViolation(v);
                    }
                }
            } else {
                // Write to XML file
                XmlViolationsWriter.WriteXmlOutput(xmlfileOrNullForLog, InputContexts);
            }
            LogSummary(InputContexts);
            return this;
        }

        [NotNull]
        public GlobalContext TransformGraph([NotNull] string transformationOption) {
            IGraphTransformation<Dependency> transformation = CreateGraphTransformation(transformationOption);

            Log.WriteInfo(transformation.GetInfo());
            _reducedGraph = transformation.Run(_reducedGraph);

            return this;
        }

        [NotNull]
        private static IGraphTransformation<Dependency> CreateGraphTransformation(string arg) {
            string[] args = arg.Split(',', '/', '-').Select(s => s.Trim()).ToArray();
            switch (args[0].ToLowerInvariant()) {
                case "ha":
                    return new HidePureSources<Dependency>(args.Skip(1));
                case "hz":
                    return new HidePureSinks<Dependency>(args.Skip(1));
                case "ht":
                    return new HideTransitiveEdges<Dependency>(args.Skip(1));
                case "ho":
                    return new HideOuterGraph<Dependency>(args.Skip(1));
                case "uc":
                    return new UnhideCycles<Dependency>(args.Skip(1));
                case "ah":
                    return new AssociativeHull<Dependency>(args.Skip(1),
                        (e1, e2) => new Dependency(e1.UsingItem, e2.UsedItem, e2.FileName, e2.StartLine, e2.StartColumn, e2.EndLine, e2.EndColumn)
                        );
                default:
                    throw new ArgumentException("Graph transformation '" + args[0] + "' not implemented");
            }
        }

        public void Reset() {
            _inputContexts.Clear();

            _reducedGraph = null;
        }

        [NotNull]
        public GlobalContext WriteDipFile([NotNull]Options options, [NotNull]string filename) {
            Log.WriteInfo("Writing " + filename);
            DipWriter.Write(_reducedGraph, filename);
            options.GraphingDone = true;
            return this;
        }

        private static void LogSummary([NotNull]IEnumerable<IInputContext> contexts) {
            foreach (var context in contexts) {
                string msg = $"{context.Filename}: {context.ErrorCount} errors, {context.WarningCount} warnings";
                if (context.ErrorCount > 0) {
                    Log.WriteError(msg);
                } else if (context.WarningCount > 0) {
                    Log.WriteWarning(msg);
                }
            }
            Log.WriteInfo($"{contexts.Count(ctx => ctx.ErrorCount == 0 && ctx.WarningCount == 0)} input files are without violations.");
        }

        [CanBeNull]
        private DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel([NotNull]DirectoryInfo relativeRoot,
                [NotNull]string rulefilename, [NotNull]Options options, bool ignoreCase, string fileIncludeStack) {
            return GetOrCreateDependencyRuleSet_MayBeCalledInParallel(relativeRoot, rulefilename, options,
                            new Dictionary<string, string>(), new Dictionary<string, Macro>(), ignoreCase, fileIncludeStack);
        }

        public DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel(DirectoryInfo relativeRoot,
                string rulefilename, Options options, IDictionary<string, string> defines,
                IDictionary<string, Macro> macros, bool ignoreCase,
                string fileIncludeStack) {
            string fullCanonicalRuleFilename = new Uri(Path.Combine(relativeRoot.FullName, rulefilename)).LocalPath;
            DependencyRuleSet result;
            if (!_fullFilename2RulesetCache.TryGetValue(fullCanonicalRuleFilename, out result)) {
                try {
                    long start = Environment.TickCount;
                    result = new DependencyRuleSet(this, options, fullCanonicalRuleFilename, defines, macros, ignoreCase,
                        (string.IsNullOrEmpty(fileIncludeStack) ? "" : fileIncludeStack + " + ") + fullCanonicalRuleFilename);
                    Log.WriteDebug("Completed reading " + fullCanonicalRuleFilename + " in " + (Environment.TickCount - start) + " ms");

                    if (!_fullFilename2RulesetCache.ContainsKey(fullCanonicalRuleFilename)) {
                        // If the set is already in the cache, we drop the set we just read (it's the same anyway).
                        _fullFilename2RulesetCache.AddOrUpdate(fullCanonicalRuleFilename,
                            result, (filename, existingRuleSet) => result = existingRuleSet);
                    }
                } catch (FileNotFoundException) {
                    Log.WriteError("File " + fullCanonicalRuleFilename + " not found");
                    return null;
                }
            }
            return result;
        }

        public int Run(string[] args) {
            return _program.Run(args);
        }

        public DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel(Options options, string dependencyFilename,
                                                                                    string fileIncludeStack) {
            DependencyRuleSet ruleSetForAssembly = Load(dependencyFilename, options.Directories, options, options.IgnoreCase, fileIncludeStack);
            if (ruleSetForAssembly == null && !string.IsNullOrEmpty(options.DefaultRuleSetFile)) {
                ruleSetForAssembly = GetOrCreateDependencyRuleSet_MayBeCalledInParallel(
                                            new DirectoryInfo("."), options.DefaultRuleSetFile, options, options.IgnoreCase, fileIncludeStack);
            }
            return ruleSetForAssembly;
        }

        /// <summary>
        /// Read rule set from file.
        /// </summary>
        /// <returns>Read rule set; or <c>null</c> if not poeeible to read it.</returns>
        [CanBeNull]
        public DependencyRuleSet Load(string dependencyFilename, List<DirectoryOption> directories, Options options, bool ignoreCase, [NotNull] string fileIncludeStack) {
            foreach (var d in directories) {
                string fullName = d.GetFullNameFor(dependencyFilename);
                if (fullName != null) {
                    DependencyRuleSet result = GetOrCreateDependencyRuleSet_MayBeCalledInParallel(new DirectoryInfo("."), fullName, options, ignoreCase, fileIncludeStack);
                    if (result != null) {
                        return result;
                    }
                }
            }
            return null; // if nothing found
        }
    }
}