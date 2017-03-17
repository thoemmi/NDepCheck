﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        internal const string ABSTRACT_IT = "%";
        internal const string ABSTRACT_IT_AS_INNER = "!";

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
            _reservedNames.Add(ABSTRACT_IT.Trim());
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
        private readonly List<Projection> _orderedAbstractions = new List<Projection>();

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

        public DependencyRuleSet([NotNull] IGlobalContext globalContext, [NotNull] Options options,
            [NotNull] string ruleSource, [NotNull] IDictionary<string, string> defines,
            [NotNull] IDictionary<string, Macro> macros, bool ignoreCase, [NotNull] string fileIncludeStack)
            : this(ignoreCase, fileIncludeStack) {
            _defines = new SortedDictionary<string, string>(defines, new LengthComparer());
            _macros = new SortedDictionary<string, Macro>(macros, new LengthComparer());
            if (ruleSource.StartsWith("{")) {
                FullSourceName = "-x";
                using (var reader = new StringReader(ruleSource.Trim().TrimStart('{').TrimEnd('}'))) {
                    Read(globalContext, options, "-x", reader, ignoreCase, fileIncludeStack);
                }
            } else {
                FullSourceName = ruleSource;
                using (var reader = new StreamReader(ruleSource, Encoding.Default)) {
                    Read(globalContext, options, ruleSource, reader, ignoreCase, fileIncludeStack);
                }
            }
        }

        private void Read(IGlobalContext globalContext, Options options, string ruleSourceName, TextReader reader, bool ignoreCase, string fileIncludeStack) {
            bool success = ProcessText(globalContext, ruleSourceName, 0, reader, options, LEFT_PARAM, RIGHT_PARAM, ignoreCase, fileIncludeStack);
            if (!success) {
                throw new ApplicationException("Could not load rules from " + ruleSourceName + " (in " + fileIncludeStack + ")");
            }
        }

        [CanBeNull]
        public string FullSourceName {
            get;
        }

        [NotNull]
        public string FileIncludeStack {
            get;
        }

        #region Loading

        private bool ProcessText(IGlobalContext globalContext, string ruleSourceName, int startLineNo, TextReader tr,
            [CanBeNull] Options options, string leftParam, string rightParam, bool ignoreCase, string fileIncludeStack) {
            ItemType usingItemType = AbstractReaderFactory.GetDefaultDescriptor(ruleSourceName, options?.RuleFileExtension);
            ItemType usedItemType = AbstractReaderFactory.GetDefaultDescriptor(ruleSourceName, options?.RuleFileExtension);

            return ProcessText(globalContext, ruleSourceName, startLineNo, tr, options, leftParam, rightParam,
                               ignoreCase, fileIncludeStack, usingItemType, usedItemType);
        }

        private bool ProcessText(IGlobalContext globalContext, string ruleSourceName, int startLineNo, TextReader tr,
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
                            Log.WriteError($"{ruleSourceName}: @-line encountered while processing macro - this is not allowed", ruleSourceName, lineNo);
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
                            Log.WriteError($"{line}: $-line must contain " + MAYUSE, ruleSourceName, lineNo);
                        }
                        usingItemType = AbstractReaderFactory.GetItemType(ExpandDefines(typeLine.Substring(0, i).Trim()));
                        usedItemType = AbstractReaderFactory.GetItemType(ExpandDefines(typeLine.Substring(i + MAYUSE.Length).Trim()));
                    } else if (line.StartsWith("+")) {
                        string includeFilename = line.Substring(1).Trim();
                        DependencyRuleSet included = globalContext.GetOrCreateDependencyRuleSet_MayBeCalledInParallel(
                                new FileInfo(ruleSourceName).Directory,
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
                            Log.WriteError($"{line}: Could not load rule set from file {includeFilename}", ruleSourceName, lineNo);
                            textIsOk = false;
                        }
                    } else if (line.EndsWith("{")) {
                        if (currentGroup.Group != "") {
                            Log.WriteError($"{ruleSourceName}: Nested '... {{' not possible", ruleSourceName, lineNo);
                            textIsOk = false;
                        } else {
                            currentGroup = new DependencyRuleGroup(usingItemType, line.TrimEnd('{').TrimEnd(), ignoreCase);
                            _ruleGroups.Add(currentGroup);
                        }
                    } else if (line == "}") {
                        if (currentGroup.Group != "") {
                            currentGroup = _mainRuleGroup;
                        } else {
                            Log.WriteError($"{ruleSourceName}: '}}' without corresponding '... {{'", ruleSourceName, lineNo);
                            textIsOk = false;
                        }
                    } else if (ProcessMacroIfFound(globalContext, line, ignoreCase)) {
                        // macro is already processed as side effect in ProcessMacroIfFound()
                    } else if (line.StartsWith(ABSTRACT_IT) || line.StartsWith(ABSTRACT_IT_AS_INNER)) {
                        string rule = line.Substring(1).Trim();
                        bool ok = AddProjections(usingItemType, usedItemType, isInner: line.StartsWith(ABSTRACT_IT_AS_INNER),
                                                 ruleFileName: ruleSourceName, lineNo: lineNo, rule: rule, ignoreCase: ignoreCase);
                        if (!ok) {
                            textIsOk = false;
                        }
                    } else if (line.Contains(MAYUSE) || line.Contains(MUSTNOTUSE) ||
                               line.Contains(MAYUSE_WITH_WARNING) || line.Contains(MAYUSE_RECURSIVE)) {
                        string currentRawUsingPattern;
                        bool ok = currentGroup.AddDependencyRules(this, usingItemType, usedItemType, ruleSourceName, lineNo, line, ignoreCase, previousRawUsingPattern, out currentRawUsingPattern);
                        if (!ok) {
                            textIsOk = false;
                        } else {
                            previousRawUsingPattern = currentRawUsingPattern;
                        }
                    } else if (line.EndsWith(MACRO_DEFINE)) {
                        string macroName = line.Substring(0, line.Length - MACRO_DEFINE.Length).Trim();
                        if (!CheckDefinedName(macroName, ruleSourceName, lineNo)) {
                            textIsOk = false;
                        }
                        string macroText = "";
                        int macroStartLineNo = lineNo;
                        for (;;) {
                            line = tr.ReadLine();
                            lineNo++;
                            if (line == null) {
                                Log.WriteError($"{ruleSourceName}: Missing {MACRO_END} at end", ruleSourceName, lineNo);
                                textIsOk = false;
                                break;
                            }
                            line = line.Trim();
                            if (line == MACRO_END) {
                                var macro = new Macro(macroText, ruleSourceName, macroStartLineNo, usingItemType, usedItemType);
                                if (_macros.ContainsKey(macroName) && !_macros[macroName].Equals(macro)) {
                                    throw new ApplicationException("Macro '" + macroName + "' cannot be redefined differently at " + ruleSourceName + ":" + lineNo);
                                }
                                _macros[macroName] = macro;
                                break;
                            } else {
                                macroText += line + "\n";
                            }
                        }
                    } else if (line.Contains(DEFINE)) {
                        AddDefine(ruleSourceName, lineNo, line);
                    } else {
                        Log.WriteError(ruleSourceName + ": Cannot parse line " + lineNo + ": " + line, ruleSourceName, lineNo);
                        textIsOk = false;
                    }
                } catch (Exception ex) {
                    Log.WriteError($"{ruleSourceName}: {ex.Message}", ruleSourceName, lineNo);
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
        internal List<Projection> ExtractGraphAbstractions() {
            var result = new List<Projection>();
            ExtractGraphAbstractions(result, new List<DependencyRuleSet>());
            return result;
        }

        private void ExtractGraphAbstractions([NotNull]List<Projection> orderedGraphAbstractions, [NotNull]List<DependencyRuleSet> visited) {
            if (visited.Contains(this)) {
                return;
            }
            visited.Add(this);
            orderedGraphAbstractions.AddRange(_orderedAbstractions);
            foreach (var includedRuleSet in _includedRuleSets) {
                includedRuleSet.ExtractGraphAbstractions(orderedGraphAbstractions, visited);
            }
        }

        /// <summary>
        /// Add one or more <c>GraphAbstraction_</c>s from a single input
        /// line (with leading %).
        /// public for testability.
        /// </summary>
        public bool AddProjections([CanBeNull] ItemType sourceItemType, [CanBeNull]ItemType targetItemType, bool isInner,
                                         [NotNull] string ruleFileName, int lineNo, [NotNull] string rule, bool ignoreCase) {
            if (sourceItemType == null || targetItemType == null) {
                Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleFileName}, graph rules are ignored", ruleFileName, lineNo);
                return false;
            } else {
                int i = rule.IndexOf(MAYUSE, StringComparison.Ordinal);
                string pattern;
                string[] targetSegments;
                if (i >= 0) {
                    string rawPattern = rule.Substring(0, i).Trim();
                    pattern = ExpandDefines(rawPattern);

                    string rawTargetSegments = rule.Substring(i + MAYUSE.Length).Trim();
                    targetSegments = ExpandDefines(rawTargetSegments).Split(':').Select(s => s.Trim()).ToArray();
                } else {
                    pattern = ExpandDefines(rule.Trim());
                    targetSegments = null;
                }

                Projection ga = new Projection(sourceItemType, targetItemType, pattern, targetSegments, isInner, ignoreCase);

                _orderedAbstractions.Add(ga);
                if (Log.IsChattyEnabled) {
                    Log.WriteInfo("Reg.exps used for projecting " + pattern +
                                  (targetSegments == null ? "" : " to " + string.Join(":", targetSegments)) + " (" + ruleFileName + ":" + lineNo + ")");
                    Log.WriteInfo(ga.ToString());
                }
                return true;
            }
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
