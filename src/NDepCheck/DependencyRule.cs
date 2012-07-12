// (c) HMMüller 2006...2010

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NDepCheck {
    /// <remarks>Class <c>DependencyRule</c> knows enough 
    /// about an allowed or forbidden dependency so that it
    /// can find out whether a <c>Dependency</c> matches it.
    /// Internally, the class stores (after an idea of
    /// Ralf Kretzschmar) the dependency as a single regular
    /// expression, which allows back-references
    /// (like \1) between the using and the used
    /// item.</remarks>
    internal class DependencyRule : Pattern {
        private readonly DependencyRuleRepresentation _rep;
        private readonly IRuleMatch _ruleMatch;
        private int _hitCount;

        // Dependency rules are created from lines with
        // a specific extension algorithm (see CreateDependencyRules()
        // below. Hence, the constructor is private.
        private DependencyRule(IRuleMatch ruleMatch, DependencyRuleRepresentation rep) {
            _ruleMatch = ruleMatch;
            _rep = rep;
        }

        public int HitCount {
            get { return _hitCount; }
        }

        public bool IsSameAs(DependencyRule rule) {
            return _ruleMatch.Equals(rule._ruleMatch);
        }

        /// <summary>
        /// Factory method for Dependency objects. Each
        /// of the two patterns can be either a "wildcard pattern",
        /// a regular expression prefix (starting with ^), or
        /// a regular expression (starting with ^and ending with
        /// $).
        /// </summary>
        /// <param name="usingItemPattern">Pattern for the "user" side
        ///   of a dependency.</param>
        /// <param name="usedItemPattern">Pattern for the "used" side
        ///   of a dependency.</param>
        /// <param name="rep">Visible representation of this rule.</param>
        public static List<DependencyRule> CreateDependencyRules(string usingItemPattern, string usedItemPattern, DependencyRuleRepresentation rep) {
            var result = new List<DependencyRule>();
            if (SameNamespaceRule.Accepts(usingItemPattern, usedItemPattern)) {
                result.Add(new DependencyRule(new SameNamespaceRule(), rep));
            } else if (PrefixAnyRule.Accepts(usingItemPattern, usedItemPattern)) {
                result.Add(new DependencyRule(new PrefixAnyRule(usingItemPattern), rep));
            } else if (PrefixPrefixRule.Accepts(usingItemPattern, usedItemPattern)) {
                result.Add(new DependencyRule(new PrefixPrefixRule(usingItemPattern, usedItemPattern), rep));
            } else if (PrefixClassRule.Accepts(usingItemPattern, usedItemPattern)) {
                result.Add(new DependencyRule(new PrefixClassRule(usingItemPattern, usedItemPattern), rep));
            } else if (ClassClassRule.Accepts(usingItemPattern, usedItemPattern)) {
                result.Add(new DependencyRule(new ClassClassRule(usingItemPattern, usedItemPattern), rep));
            } else if (GeneralAnyRule.Accepts(usedItemPattern)) {
                List<string> expandedUsingItemRegexs = Expand(usingItemPattern);
                result.AddRange(
                    expandedUsingItemRegexs.Select(er => new DependencyRule(new GeneralAnyRule(er, usingItemPattern), rep)));
            } else if (AnyGeneralRule.Accepts(usingItemPattern)) {
                List<string> expandedUsedItemRegexs = Expand(usedItemPattern);
                result.AddRange(
                    expandedUsedItemRegexs.Select(er => new DependencyRule(new AnyGeneralRule(er, usedItemPattern), rep)));
            } else if (GeneralClassWithoutBackrefRule.Accepts(usingItemPattern, usedItemPattern)) {
                result.Add(new DependencyRule(new GeneralClassWithoutBackrefRule(usingItemPattern, usedItemPattern), rep));
            } else if (GeneralPrefixRule.Accepts(usingItemPattern, usedItemPattern)) {
                List<string> expandedUsingItemRegexs = Expand(usingItemPattern);
                result.AddRange(
                    expandedUsingItemRegexs.Select(
                        er => new DependencyRule(new GeneralPrefixRule(er, usingItemPattern, usedItemPattern), rep)));
            } else if (GeneralClassRule.Accepts(usedItemPattern)) {
                List<string> expandedUsingItemRegexs = Expand(usingItemPattern);
                result.AddRange(
                    expandedUsingItemRegexs.Select(
                        er => new DependencyRule(new GeneralClassRule(er, usingItemPattern, usedItemPattern), rep)));
            } else {
                List<string> expandedUsingItemRegexs = Expand(usingItemPattern);
                List<string> expandedUsedItemRegexs = Expand(usedItemPattern);
                result.AddRange(from er in expandedUsingItemRegexs
                                from ed in expandedUsedItemRegexs
                                select new DependencyRule(new GeneralRule(er, usingItemPattern, ed, usedItemPattern), rep));
            }
            return result;
        }

        /// <summary>
        /// Check whether a concrete using item and used item
        /// match this dependency.
        /// </summary>
        public bool Matches(Dependency d, bool debug) {
            if (debug) {
                Program.WriteDebug("Checking " + d + " against " + this);
            }
            if (_ruleMatch.Matches(d)) {
                _hitCount++;
                _rep.MarkHit();
                return true;
            } else {
                return false;
            }
        }

        /// <summary>
        /// In verbose mode, print out regular expression.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return _rep + " matching " + _ruleMatch;
        }

        #region Nested type: IRuleMatch

        private interface IRuleMatch {
            bool Matches(Dependency d);
        }

        #endregion

        #region Nested type: AbstractRuleMatch

        private abstract class AbstractRuleMatch : IRuleMatch {
            private static readonly Regex _fixedPrefixPattern = new Regex("^[" + INNER_LETTER_BASE + @"<>.]*", RegexOptions.Compiled);

            public abstract bool Matches(Dependency d);

            protected static bool IsClassTail(string s) {
                return s == "" || s.StartsWith("::") || s.StartsWith("/");
            }

            protected static bool IsPrefixPattern(string pattern) {
                string prefix = _fixedPrefixPattern.Match(pattern).Value;
                return prefix.EndsWith(".")
                       && prefix + "**" == pattern;
            }

            protected static string GetPrefix(string pattern) {
                return _fixedPrefixPattern.Match(pattern).Value;
            }

            protected static bool IsClassPattern(string pattern) {
                return !pattern.Contains("/")
                       && !pattern.Contains("::");
            }
        }

        #endregion

        #region Nested type: ClassClassRule

        private sealed class ClassClassRule : AbstractRuleMatch {
            private static readonly Regex _fixedClassPattern = new Regex("^[" + INNER_LETTER_BASE + @"/<>.]*$", RegexOptions.Compiled);

            private readonly string _usedFixedPrefix;
            private readonly string _usingFixedPrefix;

            public ClassClassRule(string usingItemPattern, string usedItemPattern) {
                _usingFixedPrefix = usingItemPattern;
                _usedFixedPrefix = usedItemPattern;
            }

            public override bool Equals(object obj) {
                return obj is ClassClassRule
                    && ((ClassClassRule)obj)._usedFixedPrefix == _usedFixedPrefix
                    && ((ClassClassRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _usedFixedPrefix.GetHashCode() ^ _usingFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                return d.UsingItem.StartsWith(_usingFixedPrefix)
                       && IsClassTail(d.UsingItem.Substring(_usingFixedPrefix.Length))
                       && d.UsedItem.StartsWith(_usedFixedPrefix)
                       && IsClassTail(d.UsedItem.Substring(_usedFixedPrefix.Length));
            }

            public override string ToString() {
                return "ClassClassRule: {" + _usingFixedPrefix + "~~~ ---> " + _usedFixedPrefix + "~~~]";
            }

            public static bool Accepts(string usingItemPattern, string usedItemPattern) {
                return _fixedClassPattern.IsMatch(usingItemPattern)
                       && _fixedClassPattern.IsMatch(usedItemPattern);
            }
        }

        #endregion

        #region Nested type: GeneralAnyRule

        private class GeneralAnyRule : AbstractRuleMatch {
            private readonly string _rex;
            private readonly string _usingFixedPrefix;

            public GeneralAnyRule(string usingItemRegex, string usingItemPattern) {
                _rex = usingItemRegex.Trim();
                _usingFixedPrefix = GetPrefix(usingItemPattern);
            }

            public override bool Equals(object obj) {
                return obj is GeneralAnyRule
                    && ((GeneralAnyRule)obj)._rex.Equals(_rex)
                    && ((GeneralAnyRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _rex.GetHashCode() ^ _usingFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                if (d.UsingItem.StartsWith(_usingFixedPrefix)) {
                    return Regex.IsMatch(d.UsingItem, _rex);
                } else {
                    return false;
                }
            }

            private string DebugUsing {
                get {
                    return DebugContract(_rex);
                }
            }

            public override string ToString() {
                return "AnyRule: {" + _usingFixedPrefix + "~~~} " + _rex + " = " + DebugUsing + " ---> ~~~";
            }

            public static bool Accepts(string usedItemPattern) {
                return usedItemPattern.Trim() == "**";
            }
        }

        #endregion

        #region Nested type: AnyGeneralRule

        private class AnyGeneralRule : AbstractRuleMatch {
            private readonly string _rex;
            private readonly string _usedFixedPrefix;

            public AnyGeneralRule(string usedItemRegex, string usedItemPattern) {
                _rex = usedItemRegex.Trim();
                _usedFixedPrefix = GetPrefix(usedItemPattern);
            }

            public override bool Equals(object obj) {
                return obj is AnyGeneralRule
                    && ((AnyGeneralRule)obj)._rex.Equals(_rex)
                    && ((AnyGeneralRule)obj)._usedFixedPrefix == _usedFixedPrefix;
            }

            public override int GetHashCode() {
                return _rex.GetHashCode() ^ _usedFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                if (d.UsedItem.StartsWith(_usedFixedPrefix)) {
                    return Regex.IsMatch(d.UsedItem, _rex);
                } else {
                    return false;
                }
            }

            private string DebugUsed {
                get {
                    return DebugContract(_rex);
                }
            }

            public override string ToString() {
                return "AnyGeneralRule: ~~~ --->  {" + _usedFixedPrefix + "~~~} " + _rex + " = " + DebugUsed;
            }

            public static bool Accepts(string usingItemPattern) {
                return usingItemPattern.Trim() == "**";
            }
        }

        #endregion

        #region Nested type: GeneralClassRule

        private class GeneralClassRule : AbstractRuleMatch {
            private const string SEPARATOR = "===>>";

            private readonly string _rex;
            private readonly string _usedFixedPrefix;
            private readonly string _usingFixedPrefix;

            public GeneralClassRule(string usingItemRegex, string usingItemPattern, string usedItemPattern) {
                string pattern = usingItemRegex.Trim() + SEPARATOR + "^" + ExpandAsterisks(usedItemPattern);
                // The pattern now looks as follows:
                //     ^...$===>>^...
                // Thus, we must remove the internal $ and ^:
                _rex = pattern.Replace("$" + SEPARATOR + "^", SEPARATOR);

                _usingFixedPrefix = GetPrefix(usingItemPattern);
                _usedFixedPrefix = GetPrefix(usedItemPattern);
            }

            public override bool Equals(object obj) {
                return obj is GeneralClassRule
                    && ((GeneralClassRule)obj)._rex.Equals(_rex)
                    && ((GeneralClassRule)obj)._usedFixedPrefix == _usedFixedPrefix
                    && ((GeneralClassRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _rex.GetHashCode() ^ _usingFixedPrefix.GetHashCode() ^ _usedFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                if (d.UsedItem.StartsWith(_usedFixedPrefix) && d.UsingItem.StartsWith(_usingFixedPrefix)) {
                    string check = d.UsingItem + SEPARATOR + d.UsedItem;
                    Match match = Regex.Match(check, _rex);
                    string rest = check.Substring(match.Value.Length);
                    return IsClassTail(rest);
                } else {
                    return false;
                }
            }

            private string DebugUsingUsed {
                get {
                    return DebugContract(_rex);
                }
            }

            public override string ToString() {
                return "GeneralClassRule: {" + _usingFixedPrefix + " / " + _usedFixedPrefix + "}" + _rex + " = " + DebugUsingUsed;
            }


            public static bool Accepts(string usedItemPattern) {
                return IsClassPattern(usedItemPattern);
            }
        }

        #endregion

        #region Nested type: GeneralClassWithoutBackrefRule

        private class GeneralClassWithoutBackrefRule : AbstractRuleMatch {
            private readonly string _usedRegex;
            private readonly string _usingRegex;

            public GeneralClassWithoutBackrefRule(string usingItemRegex, string usedItemRegex) {
                _usingRegex = "^" + ExpandAsterisks(usingItemRegex);
                _usedRegex = "^" + ExpandAsterisks(usedItemRegex);
            }

            public override bool Equals(object obj) {
                return obj is GeneralClassWithoutBackrefRule
                    && ((GeneralClassWithoutBackrefRule)obj)._usingRegex.Equals(_usingRegex)
                    && ((GeneralClassWithoutBackrefRule)obj)._usedRegex == _usedRegex;
            }

            public override int GetHashCode() {
                return _usingRegex.GetHashCode() ^ _usedRegex.GetHashCode();
            }

            public static bool Accepts(string usingItemPattern, string usedItemPattern) {
                return IsClassPatternWithoutBackref(usingItemPattern)
                       && IsClassPatternWithoutBackref(usedItemPattern);
            }

            private static bool IsClassPatternWithoutBackref(string pattern) {
                return !pattern.Contains("/")
                       && !pattern.Contains("\\") // keine \1, \2, aber auch manche sonstige Operatoren nicht.
                       && !pattern.Contains("{") // keine named groups und sonstige Schweinereien ...
                       && !pattern.Contains("::");
            }

            public override bool Matches(Dependency d) {
                Match usingMatch = Regex.Match(d.UsingItem, _usingRegex);
                if (usingMatch.Success) {
                    string usingRest = d.UsingItem.Substring(usingMatch.Value.Length);
                    if (IsClassTail(usingRest)) {
                        Match usedMatch = Regex.Match(d.UsedItem, _usedRegex);
                        string usedRest = d.UsedItem.Substring(usedMatch.Value.Length);
                        return IsClassTail(usedRest);
                    } else {
                        return false;
                    }
                } else {
                    return false;
                }
            }

            private string DebugUsing {
                get {
                    return DebugContract(_usingRegex);
                }
            }

            private string DebugUsed {
                get {
                    return DebugContract(_usedRegex);
                }
            }

            public override string ToString() {
                return "GeneralClassWithoutBackrefRule: {" + _usingRegex + " = " + DebugUsing + " / " + _usedRegex + " = " + DebugUsed + "}";
            }
        }

        #endregion

        #region Nested type: GeneralPrefixRule

        private class GeneralPrefixRule : AbstractRuleMatch {
            private const string SEPARATOR = "===>>";

            private readonly string _rex;
            private readonly string _usedFixedPrefix;
            private readonly string _usingFixedPrefix;

            public GeneralPrefixRule(string usingItemRegex, string usingItemPattern, string usedItemPattern) {
                string pattern = usingItemRegex.Trim() + SEPARATOR + "^" + ExpandAsterisks(usedItemPattern);
                // The pattern now looks as follows:
                //     ^...$===>>^...
                // Thus, we must remove the internal $ and ^:
                _rex = pattern.Replace("$" + SEPARATOR + "^", SEPARATOR);

                _usingFixedPrefix = GetPrefix(usingItemPattern);
                _usedFixedPrefix = GetPrefix(usedItemPattern);
            }

            public override bool Equals(object obj) {
                return obj is GeneralPrefixRule
                    && ((GeneralPrefixRule)obj)._rex.Equals(_rex)
                    && ((GeneralPrefixRule)obj)._usedFixedPrefix == _usedFixedPrefix
                    && ((GeneralPrefixRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _rex.GetHashCode() ^ _usingFixedPrefix.GetHashCode() ^ _usedFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                if (d.UsedItem.StartsWith(_usedFixedPrefix) && d.UsingItem.StartsWith(_usingFixedPrefix)) {
                    return Regex.IsMatch(d.UsingItem + SEPARATOR + d.UsedItem, _rex);
                } else {
                    return false;
                }
            }

            private string DebugUsingUsed {
                get {
                    return DebugContract(_rex);
                }
            }

            public override string ToString() {
                return "GeneralPrefixRule: {" + _usingFixedPrefix + " / " + _usedFixedPrefix + "}" + _rex + " = " + DebugUsingUsed;
            }


            public static bool Accepts(string usingItemPattern, string usedItemPattern) {
                return IsClassPattern(usingItemPattern)
                       && IsPrefixPattern(usedItemPattern);
            }
        }

        #endregion

        #region Nested type: GeneralRule

        private class GeneralRule : AbstractRuleMatch {
            private const string SEPARATOR = "===>>";

            private readonly string _rex;
            private readonly string _usedFixedPrefix;
            private readonly string _usingFixedPrefix;

            public GeneralRule(string usingItemRegex, string usingItemPattern, string usedItemRegex, string usedItemPattern) {
                string pattern = usingItemRegex.Trim() + SEPARATOR + usedItemRegex.Trim();
                // The pattern now looks as follows:
                //     ^...$===>>^...$
                // Thus, we must remove the internal $ and ^:
                _rex = pattern.Replace("$" + SEPARATOR + "^", SEPARATOR);

                _usingFixedPrefix = GetPrefix(usingItemPattern);
                _usedFixedPrefix = GetPrefix(usedItemPattern);
            }

            public override bool Equals(object obj) {
                return obj is GeneralRule
                    && ((GeneralRule)obj)._rex.Equals(_rex)
                    && ((GeneralRule)obj)._usedFixedPrefix == _usedFixedPrefix
                    && ((GeneralRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _rex.GetHashCode() ^ _usingFixedPrefix.GetHashCode() ^ _usedFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                if (d.UsedItem.StartsWith(_usedFixedPrefix) && d.UsingItem.StartsWith(_usingFixedPrefix)) {
                    return Regex.IsMatch(d.UsingItem + SEPARATOR + d.UsedItem, _rex);
                } else {
                    return false;
                }
            }

            private string DebugUsingUsed {
                get {
                    return DebugContract(_rex);
                }
            }

            public override string ToString() {
                return "GeneralRule: {" + _usingFixedPrefix + " / " + _usedFixedPrefix + "}" + _rex + " = " +  DebugUsingUsed;
            }
        }

        #endregion

        #region Nested type: PrefixAnyRule

        private class PrefixAnyRule : AbstractRuleMatch {
            private readonly string _usingFixedPrefix;

            public PrefixAnyRule(string usingItemPattern) {
                _usingFixedPrefix = GetPrefix(usingItemPattern);
            }

            public override bool Equals(object obj) {
                return obj is PrefixAnyRule
                    && ((PrefixAnyRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _usingFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                return d.UsingItem.StartsWith(_usingFixedPrefix);
            }

            public override string ToString() {
                return "PrefixAnyRule: {" + _usingFixedPrefix + "~~~} ---> **";
            }

            public static bool Accepts(string usingItemPattern, string usedItemPattern) {
                bool isPrefixPattern = IsPrefixPattern(usingItemPattern);
                return
                    isPrefixPattern
                    && usedItemPattern == "**";
            }
        }

        #endregion

        #region Nested type: PrefixClassRule

        private class PrefixClassRule : AbstractRuleMatch {
            private static readonly Regex _fixedClassPattern = new Regex("^[" + INNER_LETTER_BASE + @"/<>.]*$", RegexOptions.Compiled);

            private readonly string _usedFixedPrefix;
            private readonly string _usingFixedPrefix;

            public PrefixClassRule(string usingItemPattern, string usedItemPattern) {
                _usingFixedPrefix = GetPrefix(usingItemPattern);
                _usedFixedPrefix = usedItemPattern;
            }

            public override bool Equals(object obj) {
                return obj is PrefixClassRule
                    && ((PrefixClassRule)obj)._usedFixedPrefix == _usedFixedPrefix
                    && ((PrefixClassRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _usingFixedPrefix.GetHashCode() ^ _usedFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                return d.UsingItem.StartsWith(_usingFixedPrefix)
                       && d.UsedItem.StartsWith(_usedFixedPrefix)
                       && IsClassTail(d.UsedItem.Substring(_usedFixedPrefix.Length));
            }

            public override string ToString() {
                return "PrefixClassRule: {" + _usingFixedPrefix + "~~~ ---> " + _usedFixedPrefix + "~~~]";
            }

            public static bool Accepts(string usingItemPattern, string usedItemPattern) {
                string usingPrefix = GetPrefix(usingItemPattern);
                return
                    usingPrefix.EndsWith(".")
                    && usingPrefix + "**" == usingItemPattern
                    && _fixedClassPattern.IsMatch(usedItemPattern);
            }
        }

        #endregion

        #region Nested type: PrefixPrefixRule

        private class PrefixPrefixRule : AbstractRuleMatch {
            private readonly string _usedFixedPrefix;
            private readonly string _usingFixedPrefix;

            public PrefixPrefixRule(string usingItemPattern, string usedItemPattern) {
                _usingFixedPrefix = GetPrefix(usingItemPattern);
                _usedFixedPrefix = GetPrefix(usedItemPattern);
            }

            public override bool Equals(object obj) {
                return obj is PrefixPrefixRule
                    && ((PrefixPrefixRule)obj)._usedFixedPrefix == _usedFixedPrefix
                    && ((PrefixPrefixRule)obj)._usingFixedPrefix == _usingFixedPrefix;
            }

            public override int GetHashCode() {
                return _usingFixedPrefix.GetHashCode() ^ _usedFixedPrefix.GetHashCode();
            }

            public override bool Matches(Dependency d) {
                return d.UsedItem.StartsWith(_usedFixedPrefix) && d.UsingItem.StartsWith(_usingFixedPrefix);
            }

            public override string ToString() {
                return "PrefixRule: {" + _usingFixedPrefix + "~~~ ---> " + _usedFixedPrefix + "~~~]";
            }

            public static bool Accepts(string usingItemPattern, string usedItemPattern) {
                string usingPrefix = GetPrefix(usingItemPattern);
                string usedPrefix = GetPrefix(usedItemPattern);
                return
                    usingPrefix.EndsWith(".")
                    && usingPrefix + "**" == usingItemPattern
                    && usedPrefix.EndsWith(".")
                    && usedPrefix + "**" == usedItemPattern;
            }
        }

        #endregion

        #region Nested type: SameNamespaceRule

        private class SameNamespaceRule : AbstractRuleMatch {
            public override bool Equals(object obj) {
                return obj is SameNamespaceRule;
            }

            public override int GetHashCode() {
                return 17;
            }

            public override bool Matches(Dependency d) {
                return d.UsedNamespace == d.UsingNamespace;
            }

            public override string ToString() {
                return @"SameNamespaceRule: (**).* ---> \1.*";
            }

            public static bool Accepts(string usingItemPattern, string usedItemPattern) {
                return usingItemPattern == "(**).*" && usedItemPattern == @"\1.*";
            }
        }

        #endregion
    }

    internal class DependencyRuleRepresentation {
        private readonly bool _isQuestionableRule;
        private readonly string _line;
        private readonly uint _lineNo;
        private readonly string _ruleFileName;
        private int _hitCount;

        internal DependencyRuleRepresentation(string ruleFileName, uint lineNo, string line, bool isQuestionableRule) {
            _ruleFileName = ruleFileName;
            _lineNo = lineNo;
            _line = line;
            _isQuestionableRule = isQuestionableRule;
            _hitCount = 0;
        }

        /// <summary>
        /// Was a rule represented by this representation ever matched with a true result?
        /// </summary>
        public bool WasHit {
            get { return _hitCount > 0; }
        }

        public int HitCount {
            get { return _hitCount; }
        }

        public bool IsQuestionableRule {
            get { return _isQuestionableRule; }
        }

        internal void MarkHit() {
            _hitCount++;
        }

        public override string ToString() {
            return _line + " (at " + _ruleFileName + ":" + _lineNo + ")";
        }
    }
}