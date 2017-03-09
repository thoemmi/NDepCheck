using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck {
    public class DependencyRuleSet {
        private const string LEFT_PARAM = "\\L";
        private const string MACRO_DEFINE = ":=";
        private const string MACRO_END = "=:";
        private const string RIGHT_PARAM = "\\R";

        internal const string MAYUSE = "--->";
        internal const string MAYUSE_RECURSIVE = "===>";
        internal const string MAYUSE_WITH_WARNING = "---?";
        internal const string MUSTNOTUSE = "---!";

        internal const string GRAPHIT = "%";
        internal const string GRAPHITINNER = "!";

        /// <summary>
        /// Constant for one-line defines.
        /// </summary>
        public const string DEFINE = ":=";

        private static readonly List<string> _reservedNames = new List<string>();

        static DependencyRuleSet() {
            _reservedNames.Add(DEFINE.Trim());
            _reservedNames.Add(MAYUSE.Trim());
            _reservedNames.Add(MUSTNOTUSE.Trim());
            _reservedNames.Add(MAYUSE_WITH_WARNING.Trim());
            _reservedNames.Add(GRAPHIT.Trim());
            _reservedNames.Add(MACRO_DEFINE.Trim());
            _reservedNames.Add(MACRO_END.Trim());
            _reservedNames.Add(LEFT_PARAM.Trim());
            _reservedNames.Add(RIGHT_PARAM.Trim());
        }

        private class LengthComparer : IComparer<string> {
            public int Compare(string s1, string s2) {
                return string.Compare(s2, s1, StringComparison.Ordinal);
            }
        }

        [NotNull]
        private readonly List<DependencyRuleGroup> _ruleGroups = new List<DependencyRuleGroup>();
        [NotNull]
        private readonly DependencyRuleGroup _mainRuleGroup;

        [NotNull]
        private readonly List<GraphAbstraction> _orderedGraphAbstractions = new List<GraphAbstraction>();

        [NotNull]
        private readonly List<DependencyRuleSet> _includedRuleSets = new List<DependencyRuleSet>();

        [NotNull]
        private readonly SortedDictionary<string, Macro> _macros = new SortedDictionary<string, Macro>(new LengthComparer());
        [NotNull]
        private readonly SortedDictionary<string, string> _defines = new SortedDictionary<string, string>(new LengthComparer());

        /// <summary>
        /// Constructor public only for test cases.
        /// </summary>
        public DependencyRuleSet(bool ignoreCase, string fileIncludeStack) {
            _mainRuleGroup = new DependencyRuleGroup(ItemType.SIMPLE, group: "", ignoreCase: ignoreCase);
            _ruleGroups.Add(_mainRuleGroup);
            FileIncludeStack = fileIncludeStack;
        }

        public DependencyRuleSet([NotNull] IGlobalContext globalContext, [NotNull] Options options, [NotNull] string fullRuleFilename,
                                 [NotNull] IDictionary<string, string> defines, [NotNull] IDictionary<string, Macro> macros, bool ignoreCase,
                                 [NotNull] string fileIncludeStack)
            : this(ignoreCase, fileIncludeStack) {
            FullSourceFilename = fullRuleFilename;
            _defines = new SortedDictionary<string, string>(defines, new LengthComparer());
            _macros = new SortedDictionary<string, Macro>(macros, new LengthComparer());
            if (!LoadRules(globalContext, fullRuleFilename, options, ignoreCase, fileIncludeStack)) {
                throw new ApplicationException("Could not load rules from " + fullRuleFilename + " (in " + fileIncludeStack + ")");
            }
        }

        [CanBeNull]
        public string FullSourceFilename { get; }

        [NotNull]
        public string FileIncludeStack { get; }

        #region Loading

        /// <summary>
        /// Load a rule file.
        /// </summary>
        private bool LoadRules(IGlobalContext globalContext, string fullRuleFilename, Options options, bool ignoreCase, string fileIncludeStack) {
            using (TextReader tr = new StreamReader(fullRuleFilename, Encoding.Default)) {
                return ProcessText(globalContext, fullRuleFilename, 0, tr, options, LEFT_PARAM, RIGHT_PARAM, ignoreCase, fileIncludeStack);
            }
        }

        private bool ProcessText(IGlobalContext globalContext, string fullRuleFilename, int startLineNo, TextReader tr,
            [CanBeNull] Options options, string leftParam, string rightParam, bool ignoreCase, string fileIncludeStack) {
            ItemType usingItemType = AbstractReaderFactory.GetDefaultDescriptor(fullRuleFilename, options?.RuleFileExtension);
            ItemType usedItemType = AbstractReaderFactory.GetDefaultDescriptor(fullRuleFilename, options?.RuleFileExtension);

            return ProcessText(globalContext, fullRuleFilename, startLineNo, tr, options, leftParam, rightParam, 
                               ignoreCase, fileIncludeStack, usingItemType, usedItemType);
        }

        private bool ProcessText(IGlobalContext globalContext, string fullRuleFilename, int startLineNo, TextReader tr,
            [CanBeNull] Options options, string leftParam, string rightParam, bool ignoreCase, string fileIncludeStack,
            ItemType usingItemType, ItemType usedItemType) {

            int lineNo = startLineNo;
            bool textIsOk = true;
            DependencyRuleGroup currentGroup = _mainRuleGroup;
            string previousRawUsingPattern = null;
            for (;;) {
                string line = tr.ReadLine();

                if (line == null) {
                    break;
                }

                int commentStart = line.IndexOf("//", StringComparison.InvariantCulture);
                if (commentStart >= 0) {
                    line = line.Substring(0, commentStart);
                }

                line = line.Trim().Replace(LEFT_PARAM, leftParam).Replace(RIGHT_PARAM, rightParam);
                lineNo++;

                try {
                    if (line == "" || line.StartsWith("#") || line.StartsWith("//")) {
                        // ignore;
                    } else if (line.StartsWith("@")) {
                        if (options == null) {
                            Log.WriteError($"{fullRuleFilename}: @-line encountered while processing macro - this is not allowed", fullRuleFilename, lineNo);
                            textIsOk = false;
                        } else {
                            string[] args = line.Substring(1).Trim().Split(' ', '\t');

                            int exitCode = globalContext.Run(args);

                            if (exitCode != 0) {
                                textIsOk = false;
                            }
                        }
                    } else if (line.StartsWith("$")) {
                        string typeLine = line.Substring(1).Trim();
                        int i = typeLine.IndexOf(MAYUSE, StringComparison.Ordinal);
                        if (i < 0) {
                            Log.WriteError($"{line}: $-line must contain " + MAYUSE, fullRuleFilename, lineNo);
                        }
                        usingItemType = AbstractReaderFactory.GetItemType(ExpandDefines(typeLine.Substring(0, i).Trim()));
                        usedItemType = AbstractReaderFactory.GetItemType(ExpandDefines(typeLine.Substring(i + MAYUSE.Length).Trim()));
                    } else if (line.StartsWith("+")) {
                        string includeFilename = line.Substring(1).Trim();
                        DependencyRuleSet included = globalContext.GetOrCreateDependencyRuleSet_MayBeCalledInParallel(
                                new FileInfo(fullRuleFilename).Directory,
                                includeFilename, options, _defines, _macros, ignoreCase,
                                fileIncludeStack: fileIncludeStack);
                        if (included != null) {
                            // Error message when == null has been output by Create.
                            _includedRuleSets.Add(included);

                            // We copy the defines down into the rule set so that the selection
                            // of the longest name works (_defines implements this by using
                            // a SortedDictionary with a LengthComparer).
                            foreach (var kvp in included._defines) {
                                _defines[kvp.Key] = kvp.Value;
                            }
                            foreach (var kvp in included._macros) {
                                _macros[kvp.Key] = kvp.Value;
                            }
                        } else {
                            Log.WriteError($"{line}: Could not load rule set from file {includeFilename}", fullRuleFilename, lineNo);
                            textIsOk = false;
                        }
                    } else if (line.EndsWith("{")) {
                        if (currentGroup.Group != "") {
                            Log.WriteError($"{fullRuleFilename}: Nested '... {{' not possible", fullRuleFilename, lineNo);
                            textIsOk = false;
                        } else {
                            currentGroup = new DependencyRuleGroup(usingItemType, line.TrimEnd('{').TrimEnd(), ignoreCase);
                            _ruleGroups.Add(currentGroup);
                        }
                    } else if (line == "}") {
                        if (currentGroup.Group != "") {
                            currentGroup = _mainRuleGroup;
                        } else {
                            Log.WriteError($"{fullRuleFilename}: '}}' without corresponding '... {{'", fullRuleFilename, lineNo);
                            textIsOk = false;
                        }
                    } else if (ProcessMacroIfFound(globalContext, line, ignoreCase)) {
                        // macro is already processed as side effect in ProcessMacroIfFound()
                    } else if (line.StartsWith(GRAPHIT)) {
                        bool ok = AddGraphAbstractions(usingItemType, usedItemType, isInner: false, ruleFileName: fullRuleFilename, lineNo: lineNo, line: line, ignoreCase: ignoreCase);
                        if (!ok) {
                            textIsOk = false;
                        }
                    } else if (line.StartsWith(GRAPHITINNER)) {
                        bool ok = AddGraphAbstractions(usingItemType, usedItemType, isInner: true, ruleFileName: fullRuleFilename, lineNo: lineNo, line: line, ignoreCase: ignoreCase);
                        if (!ok) {
                            textIsOk = false;
                        }
                    } else if (line.Contains(MAYUSE) || line.Contains(MUSTNOTUSE) ||
                               line.Contains(MAYUSE_WITH_WARNING) || line.Contains(MAYUSE_RECURSIVE)) {
                        string currentRawUsingPattern;
                        bool ok = currentGroup.AddDependencyRules(this, usingItemType, usedItemType, fullRuleFilename, lineNo, line, ignoreCase, previousRawUsingPattern, out currentRawUsingPattern);
                        if (!ok) {
                            textIsOk = false;
                        } else {
                            previousRawUsingPattern = currentRawUsingPattern;
                        }
                    } else if (line.EndsWith(MACRO_DEFINE)) {
                        string macroName = line.Substring(0, line.Length - MACRO_DEFINE.Length).Trim();
                        if (!CheckDefinedName(macroName, fullRuleFilename, lineNo)) {
                            textIsOk = false;
                        }
                        string macroText = "";
                        int macroStartLineNo = lineNo;
                        for (;;) {
                            line = tr.ReadLine();
                            lineNo++;
                            if (line == null) {
                                Log.WriteError($"{fullRuleFilename}: Missing {MACRO_END} at end", fullRuleFilename, lineNo);
                                textIsOk = false;
                                break;
                            }
                            line = line.Trim();
                            if (line == MACRO_END) {
                                var macro = new Macro(macroText, fullRuleFilename, macroStartLineNo, usingItemType, usedItemType);
                                if (_macros.ContainsKey(macroName) && !_macros[macroName].Equals(macro)) {
                                    throw new ApplicationException("Macro '" + macroName + "' cannot be redefined differently at " + fullRuleFilename + ":" + lineNo);
                                }
                                _macros[macroName] = macro;
                                break;
                            } else {
                                macroText += line + "\n";
                            }
                        }
                    } else if (line.Contains(DEFINE)) {
                        AddDefine(fullRuleFilename, lineNo, line);
                    } else {
                        Log.WriteError(fullRuleFilename + ": Cannot parse line " + lineNo + ": " + line, fullRuleFilename, lineNo);
                        textIsOk = false;
                    }
                } catch (Exception ex) {
                    Log.WriteError($"{fullRuleFilename}: {ex.Message}", fullRuleFilename, lineNo);
                    textIsOk = false;
                }
            }
            return textIsOk;
        }

        private bool CheckDefinedName([NotNull] string macroName, [NotNull] string ruleFileName, int lineNo) {
            if (macroName.Contains(" ")) {
                Log.WriteError($"{ruleFileName}, line {lineNo}: Macro name must not contain white space: {macroName}", ruleFileName, lineNo);
                return false;
            } else {
                string compactedName = CompactedName(macroName);
                foreach (string reservedName in _reservedNames) {
                    if (compactedName == CompactedName(reservedName)) {
                        Log.WriteError(
                                $"{ruleFileName}, line {lineNo}: Macro name {macroName} is too similar to predefined name {reservedName}", ruleFileName, lineNo);
                        return false;
                    }
                }
                foreach (string definedMacroName in _macros.Keys) {
                    if (macroName != definedMacroName
                        && compactedName == CompactedName(definedMacroName)) {
                        Log.WriteError(
                                $"{ruleFileName}, line {lineNo}: Macro name {macroName} is too similar to already defined name {definedMacroName}", ruleFileName, lineNo);
                        return false;
                    }
                }
                return true;
            }
        }

        private static string CompactedName([NotNull] IEnumerable<char> name) {
            // Replace sequences of equal chars with a single char here
            // Reason: ===> can be confused with ====> - so I allow only
            // one of these to be defined.
            string result = "";
            char previousC = ' ';
            foreach (char c in name) {
                if (c != previousC) {
                    result += c;
                    previousC = c;
                }
            }
            return result.ToUpperInvariant();
        }

        private bool ProcessMacroIfFound([NotNull] IGlobalContext globalContext, [NotNull] string line, bool ignoreCase) {
            string foundMacroName;
            foreach (string macroName in _macros.Keys) {
                if (line.Contains(macroName) && !line.StartsWith(macroName) && !line.Contains(MACRO_DEFINE)) {
                    foundMacroName = macroName;
                    goto PROCESS_MACRO;
                }
            }
            return false;

            PROCESS_MACRO:
            Macro macro = _macros[foundMacroName];
            int macroPos = line.IndexOf(foundMacroName, StringComparison.Ordinal);
            string leftParam = line.Substring(0, macroPos).Trim();
            string rightParam = line.Substring(macroPos + foundMacroName.Length).Trim();
            ProcessText(globalContext, macro.RuleFileName, macro.StartLineNo, new StringReader(macro.MacroText), 
                null, leftParam, rightParam, ignoreCase, "???PROCESSMACRO???", macro.UsingItemType, macro.UsedItemType);
            return true;
        }

        #endregion Loading


        /// <summary>
        /// Add one-line macro definition.
        /// </summary>
        /// <param name="ruleFileName"></param>
        /// <param name="lineNo"></param>
        /// <param name="line"></param>
        private void AddDefine([NotNull] string ruleFileName, int lineNo, [NotNull] string line) {
            int i = line.IndexOf(DEFINE, StringComparison.Ordinal);
            string key = line.Substring(0, i).Trim();
            string value = line.Substring(i + DEFINE.Length).Trim();
            if (key != key.ToUpper()) {
                throw new ApplicationException("'" + key + "' at " + ruleFileName + ":" + lineNo + " is not uppercase-only");
            }
            string define = ExpandDefines(value);
            if (_defines.ContainsKey(key) && _defines[key] != define) {
                throw new ApplicationException("'" + key + "' cannot be redefined as '" + define + "' at " + ruleFileName + ":" + lineNo);
            }
            _defines[key] = define;
        }

        /// <summary>
        /// Expand first found macro in s.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        internal string ExpandDefines([CanBeNull] string s) {
            if (string.IsNullOrEmpty(s)) {
                s = "**";
            }
            // Debug.WriteLine("--------");
            foreach (string key in _defines.Keys) {
                // Debug.WriteLine(key);
                if (s.Contains(key)) {
                    return s.Replace(key, _defines[key]);
                }
            }
            return s;
        }

        [NotNull]
        internal List<GraphAbstraction> ExtractGraphAbstractions() {
            var result = new List<GraphAbstraction>();
            ExtractGraphAbstractions(result, new List<DependencyRuleSet>());
            return result;
        }

        private void ExtractGraphAbstractions([NotNull]List<GraphAbstraction> orderedGraphAbstractions, [NotNull]List<DependencyRuleSet> visited) {
            if (visited.Contains(this)) {
                return;
            }
            visited.Add(this);
            orderedGraphAbstractions.AddRange(_orderedGraphAbstractions);
            foreach (var includedRuleSet in _includedRuleSets) {
                includedRuleSet.ExtractGraphAbstractions(orderedGraphAbstractions, visited);
            }
        }

        /// <summary>
        /// Add one or more <c>GraphAbstraction_</c>s from a single input
        /// line (with leading %).
        /// public for testability.
        /// </summary>
        public bool AddGraphAbstractions([CanBeNull] ItemType usingItemType, [CanBeNull]ItemType usedItemType, bool isInner,
                                         [NotNull] string ruleFileName, int lineNo, [NotNull] string line, bool ignoreCase) {
            if (usingItemType == null || usedItemType == null) {
                Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleFileName}, graph rules are ignored", ruleFileName, lineNo);
                return false;
            } else {
                line = ExpandDefines(line.Substring(GRAPHIT.Length).Trim());
                bool ok = CreateGraphAbstraction(usingItemType, isInner, ruleFileName, lineNo, line, ignoreCase);
                if (!usingItemType.Equals(usedItemType)) {
                    ok &= CreateGraphAbstraction(usedItemType, isInner, ruleFileName, lineNo, line, ignoreCase);
                }
                return ok;
            }
        }

        private bool CreateGraphAbstraction([NotNull] ItemType usingItemType, bool isInner, [NotNull] string ruleFileName, int lineNo,
                                            [NotNull] string line, bool ignoreCase) {
            GraphAbstraction ga = new GraphAbstraction(usingItemType, line, isInner, ignoreCase);
            _orderedGraphAbstractions.Add(ga);
            if (Log.IsChattyEnabled) {
                Log.WriteInfo("Reg.exps used for drawing " + line + " (" + ruleFileName + ":" + lineNo + ")");
                Log.WriteInfo(ga.ToString());
            }
            return true;
        }

        internal IEnumerable<DependencyRuleGroup> ExtractDependencyGroups(bool ignoreCase) {
            var result = new Dictionary<string, DependencyRuleGroup>();
            CombineGroupsFromChildren(result, new List<DependencyRuleSet>(), ignoreCase);
            return result.Values;
        }

        private void CombineGroupsFromChildren([NotNull] Dictionary<string, DependencyRuleGroup> result, [NotNull] List<DependencyRuleSet> visited, bool ignoreCase) {
            if (visited.Contains(this)) {
                return;
            }
            visited.Add(this);
            foreach (var g in _ruleGroups) {
                if (result.ContainsKey(g.Group)) {
                    result[g.Group] = result[g.Group].Combine(g, ignoreCase);
                } else {
                    result[g.Group] = g;
                }
            }
            foreach (var includedRuleSet in _includedRuleSets) {
                includedRuleSet.CombineGroupsFromChildren(result, visited, ignoreCase);
            }
        }
    }
}
