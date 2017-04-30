using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.ViolationChecking {
    public class CheckDeps : AbstractTransformerWithConfigurationPerInputfile<DependencyRuleSet> {
        public static readonly Option RuleFileExtensionOption = new Option("re", "rule-extension", "extension", "extension for rule files", @default: ".dep");
        public static readonly Option RuleRootDirectoryOption = new Option("rr", "rule-rootdirectory", "directory", "search directory for rule files (searched recursively)", @default: "no search for rule files", multiple: true);
        public static readonly Option DefaultRuleFileOption = new Option("rf", "rule-defaultfile", "filename", "default rule file", @default: "no default rule file");
        public static readonly Option DefaultRulesOption = new Option("rd", "rule-defaults", "rules", "default rules", @default: "no default rules");

        private static readonly Option[] _configOptions = { RuleFileExtensionOption, RuleRootDirectoryOption, DefaultRuleFileOption, DefaultRulesOption };

        public static readonly Option ShowUnusedQuestionableRulesOption = new Option("sq", "show-unused-questionable", "", "Show unused questionable rules", @default: false);
        public static readonly Option ShowAllUnusedRulesOption = new Option("su", "show-unused-rules", "", "Show all unused rules", @default: false);
        public static readonly Option AddMarkersForBadGroups = new Option("ag", "add-group-marker", "", "Add a marker for each group that marks the dependency as bad", @default: false);

        private static readonly Option[] _transformOptions = { ShowUnusedQuestionableRulesOption, ShowAllUnusedRulesOption, AddMarkersForBadGroups };

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

        public override string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Compute dependency violations against defined rule sets.
    
Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        #region Configure

        internal const string MAY_USE = "--->";
        internal const string MAY_USE_RECURSIVE = "===>";
        internal const string MAY_USE_WITH_WARNING = "---?";
        internal const string MUST_NOT_USE = "---!";

        public override void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            base.Configure(globalContext, configureOptions, forceReload);

            Option.Parse(globalContext, configureOptions,
                RuleFileExtensionOption.Action((args, j) => {
                    _ruleFileExtension = '.' + Option.ExtractRequiredOptionValue(args, ref j, "missing extension").TrimStart('.');
                    return j;
                }),
                RuleRootDirectoryOption.Action((args, j) => {
                    _searchRootsForRuleFiles.Add(new DirectoryInfo(Option.ExtractRequiredOptionValue(args, ref j, "missing rule-search root directory")));
                    return j;
                }),
                DefaultRuleFileOption.Action((args, j) => {
                    string fullSourceName = Path.GetFullPath(Option.ExtractRequiredOptionValue(args, ref j, "missing default rules filename"));
                    _defaultRuleSet = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????", forceReload);
                    return j;
                }),
                DefaultRulesOption.Action((args, j) => {
                    _defaultRuleSet = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader(string.Join("\r\n", args.Skip(j + 1))),
                        DefaultRulesOption.ShortName, globalContext.IgnoreCase, "????", forceReload: true);
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
        }

        protected override DependencyRuleSet CreateConfigurationFromText(GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration,
            Dictionary<string, string> configValueCollector) {

            ItemType usingItemType = null;
            ItemType usedItemType = null;

            string ruleSourceName = fullConfigFileName;

            DependencyRuleGroup mainRuleGroup = null; // set with first $ line
            DependencyRuleGroup currentGroup = null; // set with first $ line

            string previousRawUsingPattern = null;

            var ruleGroups = new List<DependencyRuleGroup>();
            var children = new List<DependencyRuleSet>();
            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                forceReloadConfiguration,
                onIncludedConfiguration: (e, n) => children.Add(e),
                onLineWithLineNo: (line, lineNo) => {
                    if (line.StartsWith("$")) {
                        if (currentGroup != null && currentGroup.Group != "") {
                            return "$ inside '{{ ... }}' not allowed";
                        } else {
                            string typeLine = line.Substring(1).Trim();
                            int i = typeLine.IndexOf(MAY_USE, StringComparison.Ordinal);
                            if (i < 0) {
                                Log.WriteError($"$-line '{line}' must contain " + MAY_USE, ruleSourceName, lineNo);
                                throw new ApplicationException($"$-line '{line}' must contain " + MAY_USE);
                            }
                            usingItemType =
                                globalContext.GetItemType(typeLine.Substring(0, i).Trim());
                            usedItemType =
                                globalContext.GetItemType(typeLine.Substring(i + MAY_USE.Length).Trim());
                            if (mainRuleGroup == null) {
                                currentGroup = mainRuleGroup = new DependencyRuleGroup(usingItemType, "", ignoreCase);
                                ruleGroups.Add(currentGroup);
                                // TODO: Also for multiple $ lines?????????????????????????
                            }
                            return null;
                        }
                    } else if (line.EndsWith("{")) {
                        if (currentGroup == null || usingItemType == null) {
                            return $"Itemtypes not defined - $ line is missing in {ruleSourceName}, dependency rules are ignored";
                        } else if (currentGroup.Group != "") {
                            return "Nested '{{ ... {{' not possible";
                        } else {
                            currentGroup = new DependencyRuleGroup(usingItemType, line.TrimEnd('{').TrimEnd(), ignoreCase);
                            ruleGroups.Add(currentGroup);
                            return null;
                        }
                    } else if (line == "}") {
                        if (currentGroup?.Group != "") {
                            currentGroup = mainRuleGroup;
                            return null;
                        } else {
                            return "'}}' without corresponding '... {{'";
                        }
                    } else if (line.Contains(MAY_USE) || line.Contains(MUST_NOT_USE) || line.Contains(MAY_USE_WITH_WARNING) ||
                               line.Contains(MAY_USE_RECURSIVE)) {
                        if (currentGroup == null || usingItemType == null || usedItemType == null) {
                            return $"Itemtypes not defined - $ line is missing in {ruleSourceName}, dependency rules are ignored";
                        } else {
                            string currentRawUsingPattern;
                            bool ok = currentGroup.AddDependencyRules(usingItemType, usedItemType, ruleSourceName,
                                lineNo, line, ignoreCase, previousRawUsingPattern, out currentRawUsingPattern);
                            if (!ok) {
                                return "Could not add dependency rule";
                            } else {
                                previousRawUsingPattern = currentRawUsingPattern;
                                return null;
                            }
                        }
                    } else {
                        return "Could not parse dependency rule";
                    }
                }, configValueCollector: configValueCollector);
            return new DependencyRuleSet(ruleGroups, children);
        }

        #endregion Configure

        #region Transform

        public override bool RunsPerInputContext => true;

        public override int Transform(GlobalContext globalContext, [CanBeNull] string dependenciesFileName, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {
            _allCheckedGroups = new HashSet<DependencyRuleGroup>();            
            if (dependencies.Any()) {
                transformedDependencies.AddRange(dependencies);
                bool addMarker = false;

                // Transformation only done if there are any dependencies. This is especially useful for the
                // typical case that there are no inputcontext-less dependencies; and no default set is specified
                // (which would emit an error message "no dep file for input "" found" or the like).
                Option.Parse(globalContext, transformOptions,
                    ShowUnusedQuestionableRulesOption.Action((args, j) => {
                        _showUnusedQuestionableRules = true;
                        return j;
                    }), ShowAllUnusedRulesOption.Action((args, j) => {
                        _showUnusedRules = true;
                        return j;
                    }), AddMarkersForBadGroups.Action((args, j) => {
                        addMarker = true;
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

                fullRuleFileNames = fullRuleFileNames.Distinct().ToList();

                if (!fullRuleFileNames.Any() && dependenciesFileName != null) {
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
                        ? GetOrReadChildConfiguration(globalContext, () => new StreamReader(fullRuleFileName),
                            fullRuleFileName, globalContext.IgnoreCase, "...", forceReload: false)
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

                return CheckDependencies(globalContext, dependencies, dependencySourceForLogging, ruleSetForAssembly, addMarker);
            } else {
                return Program.OK_RESULT;
            }
        }

        private int CheckDependencies([NotNull] GlobalContext globalContext, [NotNull] IEnumerable<Dependency> dependencies,
                    string dependencySourceForLogging, DependencyRuleSet ruleSetForAssembly, bool addMarker) {
            if (!dependencies.Any()) {
                return Program.OK_RESULT;
            }

            if (ruleSetForAssembly == null) {
                Log.WriteError("No rule set found for checking " + dependencySourceForLogging);
                return Program.NO_RULE_SET_FOUND_FOR_FILE;
            }

            DependencyRuleGroup[] checkedGroups = ruleSetForAssembly.GetAllDependencyGroups(globalContext.IgnoreCase).ToArray();
            if (checkedGroups.Any()) {
                Log.WriteInfo("Checking " + dependencySourceForLogging);
                bool result = true;
                foreach (var group in checkedGroups) {
                    result &= group.Check(dependencies, addMarker);
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

        public override void AfterAllTransforms(GlobalContext globalContext) {
            foreach (
                var r in
                _allCheckedGroups.SelectMany(g => g.AllRules)
                    .Select(r => r.Representation)
                    .Distinct()
                    .OrderBy(r => r.RuleFileName)
                    .ThenBy(r => r.LineNo)) {
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

            IEnumerable<InputContext> contexts = globalContext.InputContexts;
            foreach (var ic in contexts) {
                WriteCounts(ic.Filename, ic.BadDependenciesCount, ic.QuestionableDependenciesCount);
            }
            WriteCounts("Dependencies not assigned to file", globalContext.BadDependenciesCountWithoutInputContext,
                globalContext.QuestionableDependenciesCountWithoutInputContext);
            int okFilesCt =
                contexts.Count(ctx => ctx.BadDependenciesCount == 0 && ctx.QuestionableDependenciesCount == 0);
            int allFileCt = contexts.Count();
            if (allFileCt == 1) {
                if (okFilesCt == 1) {
                    Log.WriteInfo("Input file is without violations.");
                }
            } else {
                Log.WriteInfo(okFilesCt == 1
                    ? "One input file is without violations."
                    : $"{okFilesCt} input files are without violations.");
            }
        }

        private static void WriteCounts(string input, int badDependenciesCount, int questionableDependenciesCount) {
            string msg = $"{input}: {badDependenciesCount} bad dependencies, {questionableDependenciesCount} questionable dependecies";
            if (badDependenciesCount > 0) {
                Log.WriteError(msg);
            } else if (questionableDependenciesCount > 0) {
                Log.WriteWarning(msg);
            }
        }

        #endregion Transform
    }
}
