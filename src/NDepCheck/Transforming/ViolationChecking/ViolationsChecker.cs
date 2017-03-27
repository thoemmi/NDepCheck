using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.ViolationChecking {
    public class ViolationsChecker : AbstractTransformer<DependencyRuleSet> {
        // Configuration options
        [NotNull, ItemNotNull]
        private readonly List<DirectoryInfo> _searchRootsForRuleFiles = new List<DirectoryInfo>();
        [NotNull]
        private string _ruleFileExtension = ".dep";
        [CanBeNull]
        private DependencyRuleSet _defaultRuleSet;

        // Transformer options
        private bool _showUnusedQuestionableRules;
        private bool _showUnusedRules;

        HashSet<DependencyRuleGroup> _allCheckedGroups;

        public override string GetHelp(bool detailedHelp) {
            return
@"  Compute dependency violations against defined rule sets.
    
Configuration options: [-e extension] [-s directory] [-f rulefile | -r rules]
        -e       extension for rule files
        -s       search directory for rule files (searched recursively)
        -f       default rule file
        -r       default rules

Transformer options: [-q] [-u]
        -q Show unused questionable rules
        -u Show all unused rules";
        }

        #region Configure

        internal const string MAY_USE = "--->";
        internal const string MAY_USE_RECURSIVE = "===>";
        internal const string MAY_USE_WITH_WARNING = "---?";
        internal const string MUST_NOT_USE = "---!";

        public override void Configure(GlobalContext globalContext, string configureOptions) {
            Options.Parse(configureOptions,
                new OptionAction('e', (args, j) => {
                    _ruleFileExtension = '.' + Options.ExtractOptionValue(args, ref j).TrimStart('.');
                    return j;
                }),
                new OptionAction('s', (args, j) => {
                    _searchRootsForRuleFiles.Add(new DirectoryInfo(Options.ExtractOptionValue(args, ref j)));
                    return j;
                }),
                new OptionAction('f', (args, j) => {
                    string fullSourceName = Path.GetFullPath(Options.ExtractOptionValue(args, ref j));
                    _defaultRuleSet = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????");
                    return j;
                }),
                new OptionAction('r', (args, j) => {
                    // A trick is used: The first line, which contains all options, should be ignored; and
                    // also the last } (which is from the surrounding options braces). Thus, 
                    // * we add // to the beginning - this comments out the first line;
                    // * and trim } at the end.
                    _defaultRuleSet = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader("//" + configureOptions.Trim().TrimEnd('}')), "-r", globalContext.IgnoreCase, "????");
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
        }

        protected override DependencyRuleSet CreateConfigurationFromText(GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack) {

            ItemType usingItemType = null;
            ItemType usedItemType = null;

            string ruleSourceName = fullConfigFileName;

            DependencyRuleGroup mainRuleGroup = null; // set with first $ line
            DependencyRuleGroup currentGroup = null; // set with first $ line

            string previousRawUsingPattern = null;

            var ruleGroups = new List<DependencyRuleGroup>();
            var children = new List<DependencyRuleSet>();
            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                onIncludedConfiguration: (e, n) => children.Add(e),
                onLineWithLineNo: (line, lineNo) => {
                    if (line.StartsWith("$")) {
                        if (currentGroup != null && currentGroup.Group != "") {
                            Log.WriteError("$ inside '{{ ... }}' not allowed", ruleSourceName, lineNo);
                            return false;
                        } else {
                            string typeLine = line.Substring(1).Trim();
                            int i = typeLine.IndexOf(MAY_USE, StringComparison.Ordinal);
                            if (i < 0) {
                                Log.WriteError($"$-line '{line}' must contain " + MAY_USE, ruleSourceName, lineNo);
                                throw new ApplicationException($"$-line '{line}' must contain " + MAY_USE);
                            }
                            usingItemType =
                                GlobalContext.GetItemType(ExpandDefines(typeLine.Substring(0, i).Trim(),
                                    globalContext));
                            usedItemType =
                                GlobalContext.GetItemType(
                                    ExpandDefines(typeLine.Substring(i + MAY_USE.Length).Trim(), globalContext));
                            if (mainRuleGroup == null) {
                                currentGroup = mainRuleGroup = new DependencyRuleGroup(usingItemType, "", ignoreCase);
                                ruleGroups.Add(currentGroup);
                                // TODO: Also for multiple $ lines?????????????????????????
                            }
                            return true;
                        }
                    } else if (line.EndsWith("{")) {
                        if (currentGroup == null || usingItemType == null) {
                            Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleSourceName}, dependency rules are ignored", ruleSourceName, lineNo);
                            return false;
                        } else if (currentGroup.Group != "") {
                            Log.WriteError("Nested '{{ ... {{' not possible", ruleSourceName, lineNo);
                            return false;
                        } else {
                            currentGroup = new DependencyRuleGroup(usingItemType, line.TrimEnd('{').TrimEnd(), ignoreCase);
                            ruleGroups.Add(currentGroup);
                            return true;
                        }
                    } else if (line == "}") {
                        if (currentGroup?.Group != "") {
                            currentGroup = mainRuleGroup;
                            return true;
                        } else {
                            Log.WriteError("'}}' without corresponding '... {{'", ruleSourceName,
                                lineNo);
                            return false;
                        }
                    } else if (line.Contains(MAY_USE) || line.Contains(MUST_NOT_USE) || line.Contains(MAY_USE_WITH_WARNING) ||
                               line.Contains(MAY_USE_RECURSIVE)) {
                        if (currentGroup == null || usingItemType == null || usedItemType == null) {
                            Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleSourceName}, dependency rules are ignored", ruleSourceName, lineNo);
                            return false;
                        } else {
                            string currentRawUsingPattern;
                            bool xok = currentGroup.AddDependencyRules(usingItemType, usedItemType, ruleSourceName,
                                lineNo, line, ignoreCase, previousRawUsingPattern, out currentRawUsingPattern);
                            if (!xok) {
                                return false;
                            } else {
                                previousRawUsingPattern = currentRawUsingPattern;
                                return true;
                            }
                        }
                    } else {
                        return false;
                    }
                });
            return new DependencyRuleSet(ruleGroups, children);
        }

        #endregion Configure

        #region Transform

        public override bool RunsPerInputContext => true;

        public override int Transform(GlobalContext context, string dependenciesFileName, IEnumerable<Dependency> dependencies, 
            string transformOptions, string dependencySourceForLogging, Dictionary<FromTo, Dependency> newDependenciesCollector) {
            if (dependencies.Any()) {
                // Transformation only done if there are any dependencies. This is especially useful for the
                // typical case that there are no inputcontext-less dependencies; and no default set is specified
                // (which would emit an error message "no dep file for input "" found" or the like).
                Options.Parse(transformOptions, new OptionAction('q', (args, j) => {
                    _showUnusedQuestionableRules = true;
                    return j;
                }), new OptionAction('u', (args, j) => {
                    _showUnusedRules = true;
                    return j;
                }));

                var fullRuleFileNames = new List<string>();
                foreach (var root in _searchRootsForRuleFiles) {
                    try {
                        fullRuleFileNames.AddRange(
                            root.GetFiles(Path.GetFileName(dependenciesFileName) + _ruleFileExtension,
                                SearchOption.AllDirectories).Select(fi => fi.FullName));
                    } catch (IOException ex) {
                        Log.WriteWarning($"Cannot access files in {root} ({ex.Message})");
                    }
                }
                if (!fullRuleFileNames.Any()) {
                    fullRuleFileNames = new List<string> { Path.GetFullPath(dependenciesFileName) + _ruleFileExtension };
                }

                DependencyRuleSet ruleSetForAssembly;
                if (fullRuleFileNames.Count > 1) {
                    string allFilenames = string.Join(", ", fullRuleFileNames.Select(fi => $"'{fi}'"));
                    throw new ApplicationException(
                        $"More than one dependency rule file found for input file {dependenciesFileName} in and below " +
                        $"{string.Join(", ", _searchRootsForRuleFiles)}: {allFilenames}");
                } else if (!fullRuleFileNames.Any()) {
                    ruleSetForAssembly = null;
                } else {
                    string fullRuleFileName = fullRuleFileNames[0];
                    ruleSetForAssembly = File.Exists(fullRuleFileName)
                        ? GetOrReadChildConfiguration(context, () => new StreamReader(fullRuleFileName),
                            fullRuleFileName, context.IgnoreCase, "...")
                        : null;
                }

                // Nothing found - we take the default set.
                if (ruleSetForAssembly == null) {
                    if (_defaultRuleSet == null) {
                        throw new ApplicationException(
                            $"No dependency rule file found for input file {dependenciesFileName} in and below " +
                            $"{string.Join(", ", _searchRootsForRuleFiles)}, and no default rules provided");
                    } else {
                        ruleSetForAssembly = _defaultRuleSet;
                    }
                }

                // TODO: !!!!!!!!!!!!!!!!!! How to reset all "unused counts"? 
                // (a) remember counts before and check after - but how to find all checked rules???
                // (b) reset counts in all rules (that are read in)
                // (c) keep a callback list of checked rules ...

                _allCheckedGroups = new HashSet<DependencyRuleGroup>();
                return CheckDependencies(context, dependencies, dependencySourceForLogging, ruleSetForAssembly);
            } else {
                return Program.OK_RESULT;
            }
        }

        private int CheckDependencies([NotNull] GlobalContext checkerContext,
                    [NotNull] IEnumerable<Dependency> dependencies,
                    string dependencySourceForLogging, DependencyRuleSet ruleSetForAssembly) {
            if (!dependencies.Any()) {
                return Program.OK_RESULT;
            }

            if (ruleSetForAssembly == null) {
                Log.WriteError("No rule set found for checking " + dependencySourceForLogging);
                return Program.NO_RULE_SET_FOUND_FOR_FILE;
            }

            DependencyRuleGroup[] checkedGroups = ruleSetForAssembly.GetAllDependencyGroups(checkerContext.IgnoreCase).ToArray();
            if (checkedGroups.Any()) {
                Log.WriteInfo("Checking " + dependencySourceForLogging);
                bool result = true;
                foreach (var g in checkedGroups) {
                    result &= g.Check(dependencies);
                }
                _allCheckedGroups.UnionWith(checkedGroups);

                return result ? Program.OK_RESULT : Program.DEPENDENCIES_NOT_OK;
            } else {
                Log.WriteInfo("No rule groups found for " + dependencySourceForLogging + " - no dependency checking is done");
                return Program.NO_RULE_GROUPS_FOUND;
            }
        }

        public override IEnumerable<Dependency> GetTestDependencies() {
            throw new NotImplementedException();
        }

        public override void FinishTransform(GlobalContext context) {
            foreach (var r in _allCheckedGroups.SelectMany(g => g.AllRules).Select(r => r.Representation).Distinct().OrderBy(r => r.RuleFileName).ThenBy(r => r.LineNo)) {
                if (_showUnusedQuestionableRules && r.IsQuestionableRule && !r.WasHit) {
                    Log.WriteInfo("Questionable rule " + r + " was never matched - maybe you can remove it!");
                } else if (_showUnusedRules && !r.WasHit) {
                    Log.WriteInfo("Rule " + r + " was never matched - maybe you can remove it!");
                } else {
                    if (Log.IsChattyEnabled) {
                        Log.WriteInfo("Rule " + r + " was hit " + r.HitCount + " times.");
                    }
                }
            }

            IEnumerable<InputContext> contexts = context.InputContexts;
            foreach (var context1 in contexts) {
                string msg = $"{context1.Filename}: {context1.BadDependenciesCount} errors, {context1.QuestionableDependenciesCount} warnings";
                if (context1.BadDependenciesCount > 0) {
                    Log.WriteError(msg);
                } else if (context1.QuestionableDependenciesCount > 0) {
                    Log.WriteWarning(msg);
                }
            }
            Log.WriteInfo($"{contexts.Count(ctx => ctx.BadDependenciesCount == 0 && ctx.QuestionableDependenciesCount == 0)} input files are without violations.");
        }

        #endregion Transform
    }
}
