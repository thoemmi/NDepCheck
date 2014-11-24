﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NDepCheck {
    public class DependencyRuleSet {
        private readonly CheckerContext _checkerContext;
        private const string LEFT_PARAM = "\\L";
        private const string MACRO_DEFINE = ":=";
        private const string MACRO_END = "=:";
        private const string RIGHT_PARAM = "\\R";

        internal const string MAYUSE = "--->";
        internal const string MAYUSE_WITH_WARNING = "---?";
        internal const string MUSTNOTUSE = "---!";

        internal const string GRAPHIT = "%";

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

        class LengthComparer : IComparer<string> {
            public int Compare(string s1, string s2) {
                return s2.CompareTo(s1);
            }
        }

        private readonly List<DependencyRuleGroup> _ruleGroups = new List<DependencyRuleGroup>();
        private readonly DependencyRuleGroup _mainRuleGroup;

        private readonly List<GraphAbstraction> _graphAbstractions = new List<GraphAbstraction>();

        private readonly List<DependencyRuleSet> _includedRuleSets = new List<DependencyRuleSet>();

        private readonly SortedDictionary<string, Macro> _macros = new SortedDictionary<string, Macro>(new LengthComparer());
        private readonly SortedDictionary<string, string> _defines = new SortedDictionary<string, string>(new LengthComparer());
        private bool _containsAssemblyRule;

        /// <summary>
        /// Constructor for test cases.
        /// </summary>
        public DependencyRuleSet(CheckerContext checkerContext) {
            _checkerContext = checkerContext;
            _mainRuleGroup = new DependencyRuleGroup("");
            _ruleGroups.Add(_mainRuleGroup);
        }

        public DependencyRuleSet(CheckerContext checkerContext, string fullRuleFilename,
                    IDictionary<string, string> defines,
                    IDictionary<string, Macro> macros)
            : this(checkerContext) {
            _defines = new SortedDictionary<string, string>(defines, new LengthComparer());
            _macros = new SortedDictionary<string, Macro>(macros, new LengthComparer());
            if (!LoadRules(fullRuleFilename)) {
                throw new ApplicationException("Could not load rules from " + fullRuleFilename);
            }
        }

        public bool ContainsAssemblyRule {
            get { return _containsAssemblyRule || _includedRuleSets.Any(rs => rs.ContainsAssemblyRule); }
        }

        #region Loading

        /// <summary>
        /// Load a rule file.
        /// </summary>
        private bool LoadRules(string fullRuleFilename) {
            using (TextReader tr = new StreamReader(fullRuleFilename, Encoding.Default)) {
                return ProcessText(fullRuleFilename, 0, tr, LEFT_PARAM, RIGHT_PARAM);
            }
        }

        private bool ProcessText(string fullRuleFilename, uint startLineNo, TextReader tr, string leftParam,
                                 string rightParam) {
            uint lineNo = startLineNo;
            bool textIsOk = true;
            DependencyRuleGroup currentGroup = _mainRuleGroup;
            for (; ; ) {
                string line = tr.ReadLine();

                if (line == null) {
                    break;
                }

                line = line.Trim().Replace(LEFT_PARAM, leftParam).Replace(RIGHT_PARAM, rightParam);
                lineNo++;

                if (line == "" || line.StartsWith("#") || line.StartsWith("//")) {
                    // ignore;
                } else if (line.StartsWith("+")) {
                    string includeFilename = line.Substring(1).Trim();
                    DependencyRuleSet included = _checkerContext.Create(new FileInfo(fullRuleFilename).Directory,
                                                        includeFilename,
                                                        _defines,
                                                        _macros);
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
                        Log.WriteError(String.Format("{0}: Nested '... {{' not possible", fullRuleFilename), fullRuleFilename, lineNo);
                    } else {
                        if (line.StartsWith(DependencyReader.ASSEMBLY_PREFIX)) {
                            _containsAssemblyRule = true;
                        }

                        currentGroup = new DependencyRuleGroup(line.TrimEnd('{').TrimEnd());
                        _ruleGroups.Add(currentGroup);
                    }
                } else if (line == "}") {
                    if (currentGroup.Group != "") {
                        currentGroup = _mainRuleGroup;
                    } else {
                        Log.WriteError(String.Format("{0}: '}}' without corresponding '... {{'", fullRuleFilename), fullRuleFilename, lineNo);
                    }
                } else if (ProcessMacroIfFound(line)) {
                    // macro is already processed as side effect in ProcessMacroIfFound()
                } else if (line.Contains(MAYUSE) || line.Contains(MUSTNOTUSE) ||
                           line.Contains(MAYUSE_WITH_WARNING)) {
                    bool isAssemblyRule = line.StartsWith(DependencyReader.ASSEMBLY_PREFIX);
                    _containsAssemblyRule |= isAssemblyRule;

                    currentGroup.AddDependencyRules(this, fullRuleFilename, lineNo, line, isAssemblyRule);
                } else if (line.StartsWith(GRAPHIT)) {
                    AddGraphAbstractions(fullRuleFilename, lineNo, line);
                } else if (line.EndsWith(MACRO_DEFINE)) {
                    string macroName = line.Substring(0, line.Length - MACRO_DEFINE.Length).Trim();
                    if (!CheckDefinedName(macroName, fullRuleFilename, lineNo)) {
                        textIsOk = false;
                    }
                    string macroText = "";
                    uint macroStartLineNo = lineNo;
                    for (; ; ) {
                        line = tr.ReadLine();
                        lineNo++;
                        if (line == null) {
                            Log.WriteError(String.Format("{0}: Missing {1} at end", fullRuleFilename, MACRO_END), fullRuleFilename, lineNo);
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
            }
            return textIsOk;
        }

        private bool CheckDefinedName(string macroName, string ruleFileName, uint lineNo) {
            if (macroName.Contains(" ")) {
                Log.WriteError(String.Format("{0}, line {1}: Macro name must not contain white space: {2}", ruleFileName, lineNo, macroName), ruleFileName, lineNo);
                return false;
            } else {
                string compactedName = CompactedName(macroName);
                foreach (string reservedName in _reservedNames) {
                    if (compactedName == CompactedName(reservedName)) {
                        Log.WriteError(
                            String.Format("{0}, line {1}: Macro name {2} is too similar to predefined name {3}", ruleFileName, lineNo, macroName, reservedName), ruleFileName, lineNo);
                        return false;
                    }
                }
                foreach (string definedMacroName in _macros.Keys) {
                    if (macroName != definedMacroName
                        && compactedName == CompactedName(definedMacroName)) {
                            Log.WriteError(
                            String.Format("{0}, line {1}: Macro name {2} is too similar to already defined name {3}", ruleFileName, lineNo, macroName, definedMacroName), ruleFileName, lineNo);
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

        private bool ProcessMacroIfFound(string line) {
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
            int macroPos = line.IndexOf(foundMacroName);
            string leftParam = line.Substring(0, macroPos).Trim();
            string rightParam = line.Substring(macroPos + foundMacroName.Length).Trim();
            ProcessText(macro.RuleFileName, macro.StartLineNo, new StringReader(macro.MacroText), leftParam, rightParam);
            return true;
        }

        #endregion Loading

        #region Nested type: Macro

        public class Macro {
            public readonly string MacroText;
            public readonly string RuleFileName;
            public readonly uint StartLineNo;

            internal Macro(string macroText, string ruleFileName, uint startlineNo) {
                MacroText = macroText;
                RuleFileName = ruleFileName;
                StartLineNo = startlineNo;
            }

            public override bool Equals(object obj) {
                var other = obj as Macro;
                if (other == null) {
                    return false;
                } else {
                    return other.MacroText == MacroText;
                }
            }

            public override int GetHashCode() {
                return MacroText.GetHashCode();
            }
        }

        #endregion
        
        /// <summary>
        /// Add one-line macro definition.
        /// </summary>
        /// <param name="ruleFileName"></param>
        /// <param name="lineNo"></param>
        /// <param name="line"></param>
        private void AddDefine(string ruleFileName, uint lineNo, string line) {
            int i = line.IndexOf(DEFINE);
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

        internal void ExtractGraphAbstractions(List<GraphAbstraction> graphAbstractions) {
            ExtractGraphAbstractions(graphAbstractions, new List<DependencyRuleSet>());
        }

        private void ExtractGraphAbstractions(List<GraphAbstraction> graphAbstractions, List<DependencyRuleSet> visited) {
            if (visited.Contains(this)) {
                return;
            }
            visited.Add(this);
            graphAbstractions.AddRange(_graphAbstractions);
            foreach (var includedRuleSet in _includedRuleSets) {
                includedRuleSet.ExtractGraphAbstractions(graphAbstractions, visited);
            }
        }



        /// <summary>
        /// Add one or more <c>GraphAbstraction</c>s from a single input
        /// line (with leading %).
        /// public for testability.
        /// </summary>
        public void AddGraphAbstractions(string ruleFileName, uint lineNo, string line) {
            line = ExpandDefines(line.Substring(GRAPHIT.Length).Trim());
            List<GraphAbstraction> a = GraphAbstraction.CreateGraphAbstractions(line);
            _graphAbstractions.AddRange(a);
            if (Log.IsVerboseEnabled) {
                Log.WriteInfo("Reg.exps used for drawing " + line + " (" + ruleFileName + ":" + lineNo + ")");
                foreach (GraphAbstraction ga in a) {
                    Log.WriteInfo(ga.ToString());
                }
            }
        }

        internal IEnumerable<DependencyRuleGroup> ExtractDependencyGroups() {
            var result = new Dictionary<string, DependencyRuleGroup>();
            CombineGroupsFromChildren(result, new List<DependencyRuleSet>());
            return result.Values;
        }

        private void CombineGroupsFromChildren(Dictionary<string, DependencyRuleGroup> result, List<DependencyRuleSet> visited) {
            if (visited.Contains(this)) {
                return;
            }
            visited.Add(this);
            foreach (var g in _ruleGroups) {
                if (result.ContainsKey(g.Group)) {
                    result[g.Group] = result[g.Group].Combine(g);
                } else {
                    result[g.Group] = g;
                }
            }
            foreach (var includedRuleSet in _includedRuleSets) {
                includedRuleSet.CombineGroupsFromChildren(result, visited);
            }
        }
    }
}
