using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.ViolationChecking {
    public class CheckDeps : AbstractTransformerPerContainerUriWithFileConfiguration<DependencyRuleSet> {
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


        HashSet<DependencyRuleGroup> _allCheckedGroups;

        public override string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Compute dependency violations against defined rule sets.
    
Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        #region Configure

        internal const string MAY_USE_RECURSIVE = "===>";
        internal const string MAY_USE = "--->";

        internal const string MAY_USE_TAIL = "->";
        internal const string MAY_USE_WITH_WARNING_TAIL = "-?";
        internal const string MUST_NOT_USE_TAIL = "-!";

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
                        () => new StringReader(string.Join(Environment.NewLine, args.Skip(j + 1))),
                        DefaultRulesOption.ShortName, globalContext.IgnoreCase, "????", forceReload: true);
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
        }

        protected override DependencyRuleSet CreateConfigurationFromText([NotNull] GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration,
            Dictionary<string, string> configValueCollector) {

            ItemType usingItemType = null;
            ItemType usedItemType = null;

            string ruleSourceName = fullConfigFileName;

            string previousRawUsingPattern = "";

            var ruleGroups = new List<DependencyRuleGroup>();
            var children = new List<DependencyRuleSet>();

            DependencyRuleGroup mainRuleGroup = new DependencyRuleGroup("", globalContext.IgnoreCase, null, null, "global");
            DependencyRuleGroup currentGroup = mainRuleGroup;
            ruleGroups.Add(currentGroup);

            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                forceReloadConfiguration,
                onIncludedConfiguration: (e, n) => children.Add(e),
                onLineWithLineNo: (line, lineNo) => {
                    if (line.StartsWith("$")) {
                        if (currentGroup != null && currentGroup.GroupMarker != "") {
                            return "$ inside '{{ ... }}' not allowed";
                        } else {
                            string typeLine = line.Substring(1).Trim();
                            int i = typeLine.IndexOf(MAY_USE, StringComparison.Ordinal);
                            if (i < 0) {
                                Log.WriteError($"$-line '{line}' must contain " + MAY_USE, ruleSourceName, lineNo);
                                throw new ApplicationException($"$-line '{line}' must contain " + MAY_USE_TAIL);
                            }
                            usingItemType = globalContext.GetItemType(typeLine.Substring(0, i).Trim());
                            usedItemType = globalContext.GetItemType(typeLine.Substring(i + MAY_USE.Length).Trim());
                            return null;
                        }
                    } else if (line.EndsWith("{")) {
                        if (currentGroup == null || usingItemType == null) {
                            return $"Itemtypes not defined - $ line is missing in {ruleSourceName}, dependency rules are ignored";
                        } else if (currentGroup.GroupMarker != "") {
                            return "Nested '{{ ... {{' not possible";
                        } else {
                            string groupPattern = line.TrimEnd('{').Trim();
                            currentGroup = new DependencyRuleGroup(groupPattern, globalContext.IgnoreCase, usingItemType, usedItemType, ruleSourceName + "_" + lineNo);
                            ruleGroups.Add(currentGroup);
                            return null;
                        }
                    } else if (line == "}") {
                        if (currentGroup != null && currentGroup.GroupMarker != "") {
                            currentGroup = mainRuleGroup;
                            return null;
                        } else {
                            return "'}}' without corresponding '... {{'";
                        }
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
                }, configValueCollector: configValueCollector);
            return new DependencyRuleSet(ruleGroups, children);
        }

        #endregion Configure

        #region Transform

        private bool _showUnusedQuestionableRules;
        private bool _showUnusedRules;
        private bool _addMarker;
        private int _allFilesCt, _okFilesCt;

        public override void BeforeAllTransforms([NotNull] GlobalContext globalContext, string transformOptions) {
            _showUnusedQuestionableRules = _showUnusedRules = _addMarker = false;

            Option.Parse(globalContext, transformOptions,
                ShowUnusedQuestionableRulesOption.Action((args, j) => {
                    _showUnusedQuestionableRules = true;
                    return j;
                }), ShowAllUnusedRulesOption.Action((args, j) => {
                    _showUnusedRules = true;
                    return j;
                }), AddMarkersForBadGroups.Action((args, j) => {
                    _addMarker = true;
                    return j;
                }));

            _allFilesCt = _okFilesCt = 0;
            _allCheckedGroups = new HashSet<DependencyRuleGroup>();
        }

        public override int TransformContainer([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            [CanBeNull] string containerName, [NotNull] List<Dependency> transformedDependencies) {

            transformedDependencies.AddRange(dependencies);

            var fullRuleFileNames = new List<string>();
            foreach (var root in _searchRootsForRuleFiles) {
                try {
                    fullRuleFileNames.AddRange(
                        root.GetFiles(Path.GetFileName(containerName) + _ruleFileExtension,
                            SearchOption.AllDirectories).Select(fi => fi.FullName));
                } catch (IOException ex) {
                    Log.WriteWarning($"Cannot access files in {root} ({ex.Message})");
                }
            }

            fullRuleFileNames = fullRuleFileNames.Distinct().ToList();

            if (!fullRuleFileNames.Any() && containerName != null) {
                fullRuleFileNames = new List<string> { Path.GetFullPath(containerName) + _ruleFileExtension };
            }

            DependencyRuleSet ruleSetForAssembly;
            if (fullRuleFileNames.Count > 1) {
                string allFilenames = string.Join(", ", fullRuleFileNames.Select(fi => $"'{fi}'"));
                throw new ApplicationException(
                    $"More than one dependency rule file found for input file {containerName} in and below " +
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
                        $"No dependency rule file found for input file {containerName} in and below " +
                        $"{string.Join(", ", _searchRootsForRuleFiles)}, and no default rules provided");
                } else {
                    ruleSetForAssembly = _defaultRuleSet;
                }
            }

            // TODO: !!!!!!!!!!!!!!!!!! How to reset all "unused counts"? 
            // (a) remember counts before and check after - but how to find all checked rules???
            // (b) reset counts in all rules (that are read in)
            // (c) keep a callback list of checked rules ...

            return CheckDependencies(globalContext, dependencies, containerName, ruleSetForAssembly);
        }

        private int CheckDependencies([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
                                      [CanBeNull] string containerName, [CanBeNull] DependencyRuleSet ruleSetForAssembly) {
            if (!dependencies.Any()) {
                return Program.OK_RESULT;
            }

            if (ruleSetForAssembly == null) {
                Log.WriteError("No rule set found for checking " + containerName);
                return Program.NO_RULE_SET_FOUND_FOR_FILE;
            }

            DependencyRuleGroup[] checkedGroups = ruleSetForAssembly.GetAllDependencyGroupsWithRules(globalContext.IgnoreCase).ToArray();
            if (checkedGroups.Any()) {
                Log.WriteInfo("Checking " + containerName);
                int badCount = 0;
                int questionableCount = 0;
                foreach (var group in checkedGroups) {
                    group.Check(dependencies, _addMarker, ref badCount, ref questionableCount);
                }
                _allCheckedGroups.UnionWith(checkedGroups);

                if (Log.IsVerboseEnabled) {
                    string msg = 
                        $"{containerName}: {badCount} bad dependencies, {questionableCount} questionable dependecies";
                    if (badCount > 0) {
                        Log.WriteError(msg);
                    } else if (questionableCount > 0) {
                        Log.WriteWarning(msg);
                    }
                }

                _allFilesCt++;
                if (badCount > 0) {
                    return Program.DEPENDENCIES_NOT_OK;
                } else {
                    _okFilesCt++;
                    return Program.OK_RESULT;
                }
            } else {
                Log.WriteInfo("No rule groups found for " + containerName + " - no dependency checking is done");
                return Program.NO_RULE_GROUPS_FOUND;
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies() {
            throw new NotImplementedException();
        }

        public override void AfterAllTransforms([NotNull] GlobalContext globalContext) {
            foreach (var r in _allCheckedGroups.SelectMany(g => g.AllRules)
                                               .Select(r => r.Source)
                                               .Distinct()
                                               .OrderBy(r => r.RuleSourceName)
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

            if (_allFilesCt == 1) {
                if (_okFilesCt == 1) {
                    Log.WriteInfo("Input file is without violations.");
                }
            } else {
                Log.WriteInfo(_okFilesCt == 1
                    ? "One input file is without violations."
                    : $"{_okFilesCt} input files are without violations.");
            }
        }

        #endregion Transform
    }
}
