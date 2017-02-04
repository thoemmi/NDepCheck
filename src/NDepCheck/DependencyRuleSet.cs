using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NDepCheck {
    public class DependencyRuleSet {
        private const string LEFT_PARAM = "\\L";
        private const string MACRO_DEFINE = ":=";
        private const string MACRO_END = "=:";
        private const string RIGHT_PARAM = "\\R";

        internal const string MAYUSE = "--->";
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

        private readonly List<DependencyRuleGroup> _ruleGroups = new List<DependencyRuleGroup>();
        private readonly DependencyRuleGroup _mainRuleGroup;

        private readonly List<GraphAbstraction> _orderedGraphAbstractions = new List<GraphAbstraction>();

        private readonly List<DependencyRuleSet> _includedRuleSets = new List<DependencyRuleSet>();

        private readonly SortedDictionary<string, Macro> _macros = new SortedDictionary<string, Macro>(new LengthComparer());
        private readonly SortedDictionary<string, string> _defines = new SortedDictionary<string, string>(new LengthComparer());

        /// <summary>
        /// Constructor public only for test cases.
        /// </summary>
        public DependencyRuleSet(bool ignoreCase) {
            _mainRuleGroup = new DependencyRuleGroup(null, "", ignoreCase);
            _ruleGroups.Add(_mainRuleGroup);
        }

        public DependencyRuleSet(IGlobalContext globalContext, Options options, string fullRuleFilename, 
                                 IDictionary<string, string> defines, IDictionary<string, Macro> macros, bool ignoreCase)
            : this(ignoreCase) {
            _defines = new SortedDictionary<string, string>(defines, new LengthComparer());
            _macros = new SortedDictionary<string, Macro>(macros, new LengthComparer());
            if (!LoadRules(globalContext, fullRuleFilename, options, ignoreCase)) {
                throw new ApplicationException("Could not load rules from " + fullRuleFilename);
            }
        }

        #region Loading

        /// <summary>
        /// Load a rule file.
        /// </summary>
        private bool LoadRules(IGlobalContext globalContext, string fullRuleFilename, Options options, bool ignoreCase) {
            using (TextReader tr = new StreamReader(fullRuleFilename, Encoding.Default)) {
                return ProcessText(globalContext, fullRuleFilename, 0, tr, options, LEFT_PARAM, RIGHT_PARAM, ignoreCase);
            }
        }

        private bool ProcessText(IGlobalContext globalContext, string fullRuleFilename, int startLineNo, TextReader tr, Options options, string leftParam, string rightParam, bool ignoreCase) {
            int lineNo = startLineNo;
            bool textIsOk = true;
            DependencyRuleGroup currentGroup = _mainRuleGroup;
            ItemType usingItemType = AbstractReaderFactory.GetDefaultDescriptor(fullRuleFilename);
            ItemType usedItemType = AbstractReaderFactory.GetDefaultDescriptor(fullRuleFilename);
            for (; ; ) {
                string line = tr.ReadLine();

                if (line == null) {
                    break;
                }

                line = line.Trim().Replace(LEFT_PARAM, leftParam).Replace(RIGHT_PARAM, rightParam);
                lineNo++;

                try {
                    if (line == "" || line.StartsWith("#") || line.StartsWith("//")) {
                        // ignore;
                    } else if (line.StartsWith("@")) {
                        if (options == null) {
                            Log.WriteError(
                                    $"{fullRuleFilename}: @-line encountered while processing macro - this is not allowed", fullRuleFilename, lineNo);
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
                        DependencyRuleSet included = globalContext.
                            GetOrCreateDependencyRuleSet_MayBeCalledInParallel(new FileInfo(fullRuleFilename).Directory,
                                includeFilename, options, _defines, _macros, ignoreCase);
                        if (included != null) {
                            // Error message when == null has been output by Create.
                            _includedRuleSets.Add(included);

                            // We copy the defines down into the ruleset so that the selection
                            // of the longest name works (_defines implements this by using
                            // a SortedDictionary with a LengthComparer).
                            foreach (var kvp in included._defines) {
                                _defines[kvp.Key] = kvp.Value;
                            }
                            foreach (var kvp in included._macros) {
                                _macros[kvp.Key] = kvp.Value;
                            }
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
                        AddGraphAbstractions(usingItemType, usedItemType, false, fullRuleFilename, lineNo, line, ignoreCase);
                    } else if (line.StartsWith(GRAPHITINNER)) {
                        AddGraphAbstractions(usingItemType, usedItemType, true, fullRuleFilename, lineNo, line, ignoreCase);
                    } else if (line.Contains(MAYUSE) || line.Contains(MUSTNOTUSE) ||
                               line.Contains(MAYUSE_WITH_WARNING)) {
                        currentGroup.AddDependencyRules(this, usingItemType, usedItemType, fullRuleFilename, lineNo, line, ignoreCase);
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
                                var macro = new Macro(macroText, fullRuleFilename, macroStartLineNo);
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

        private bool CheckDefinedName(string macroName, string ruleFileName, int lineNo) {
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

        private static string CompactedName(IEnumerable<char> name) {
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

        private bool ProcessMacroIfFound(IGlobalContext globalContext, string line, bool ignoreCase) {
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
            ProcessText(globalContext, macro.RuleFileName, macro.StartLineNo, new StringReader(macro.MacroText), null, leftParam, rightParam, ignoreCase);
            return true;
        }

        #endregion Loading


        /// <summary>
        /// Add one-line macro definition.
        /// </summary>
        /// <param name="ruleFileName"></param>
        /// <param name="lineNo"></param>
        /// <param name="line"></param>
        private void AddDefine(string ruleFileName, int lineNo, string line) {
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
        internal string ExpandDefines(string s) {
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

        internal List<GraphAbstraction> ExtractGraphAbstractions() {
            var result = new List<GraphAbstraction>();
            ExtractGraphAbstractions(result, new List<DependencyRuleSet>());
            return result;
        }

        private void ExtractGraphAbstractions(List<GraphAbstraction> orderedGraphAbstractions, List<DependencyRuleSet> visited) {
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
        public void AddGraphAbstractions(ItemType usingItemType, ItemType usedItemType, bool isInner, string ruleFileName, int lineNo, string line, bool ignoreCase) {
            if (usingItemType == null || usedItemType == null) {
                Log.WriteError("Itemtypes not defined - $ line is missing in this file, graph rules are ignored", ruleFileName, lineNo);
            } else {
                line = ExpandDefines(line.Substring(GRAPHIT.Length).Trim());
                CreateGraphAbstraction(usingItemType, isInner, ruleFileName, lineNo, line, ignoreCase);
                if (usingItemType != usedItemType) {
                    CreateGraphAbstraction(usedItemType, isInner, ruleFileName, lineNo, line, ignoreCase);
                }
            }
        }

        private void CreateGraphAbstraction(ItemType usingItemType, bool isInner, string ruleFileName, int lineNo, string line, bool ignoreCase) {
            GraphAbstraction ga = new GraphAbstraction(usingItemType, line, isInner, ignoreCase);
            _orderedGraphAbstractions.Add(ga);
            if (Log.IsChattyEnabled) {
                Log.WriteInfo("Reg.exps used for drawing " + line + " (" + ruleFileName + ":" + lineNo + ")");
                Log.WriteInfo(ga.ToString());
            }
        }

        internal IEnumerable<DependencyRuleGroup> ExtractDependencyGroups(bool ignoreCase) {
            var result = new Dictionary<string, DependencyRuleGroup>();
            CombineGroupsFromChildren(result, new List<DependencyRuleSet>(), ignoreCase);
            return result.Values;
        }

        private void CombineGroupsFromChildren(Dictionary<string, DependencyRuleGroup> result, List<DependencyRuleSet> visited, bool ignoreCase) {
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
