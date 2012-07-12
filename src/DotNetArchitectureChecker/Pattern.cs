// (c) HMMüller 2006...2010

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DotNetArchitectureChecker {
    /// <remarks>
    /// Parent class for pattern objects (currently,
    /// <see>DependencyRule</see> and <see>GraphAbstraction</see>).
    /// This class provides the helper methods to produce
    /// regular expressions from wildcard patterns; and to
    /// extend regular expression in four ways (not at all,
    /// inner classes, methods, and methods of inner classes).
    /// </remarks>
    public abstract class Pattern {

        // The question mark at the end seems to be necessary when a mal-formed UTF8 file (emitted
        // from  under Vista) is read.
        protected const string LETTER = @"\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}µ_?";

        protected const string INNER_LETTER_BASE = LETTER + @"\p{Nd}\p{Pc}\p{Mn}\p{Mc}\p{Cf}";

        // <> is for generic inner classes like XTPlus.Framework.Common.BidirectionalCollections.BiList/<>c__DisplayClass2
        // {}=- is for <PrivateImplementationDetails>{935F4626-4085-4FB6-8006-FFE32E60A3DA}/__StaticArrayInitTypeSize=12
        private const string INNER_LETTER_AFTER_LT = INNER_LETTER_BASE + "={}\\-\\$.<>";
        private const string INNER_LETTER = ">" + INNER_LETTER_BASE + "={}\\-\\$";
        private const string FIRST_LETTER = "{" + INNER_LETTER_BASE + "\\$";

        protected const string ASTERISK_ESCAPE = "@#@"; // needed so that we can do a few replacements with *
        protected const string ASTERISK_ABBREV = "...";

        protected const string IDENT_ESCAPED = "(?:<[" + INNER_LETTER_AFTER_LT + "]" + ASTERISK_ESCAPE + "|[" + FIRST_LETTER + "][" + INNER_LETTER + "]" + ASTERISK_ESCAPE + ")";
        protected const string IDENT_PREFIX_ESCAPED = IDENT_ESCAPED + "?";
        protected const string IDENT_INFIX_ESCAPED = "[" + INNER_LETTER + "]" + ASTERISK_ESCAPE;

        protected const string PATH_ESCAPED = IDENT_ESCAPED + "(?:[.]" + IDENT_ESCAPED + ")" + ASTERISK_ESCAPE;
        protected const string INNER_PATH_ESCAPED = IDENT_ESCAPED + "(?:[/]" + IDENT_ESCAPED + ")" + ASTERISK_ESCAPE;

        protected static readonly string IDENT_NONESCAPED = IDENT_ESCAPED.Replace(ASTERISK_ESCAPE, "*");
        protected const string METHODNAME_NONESCAPED = "[<>." + LETTER + "_\\$][<>" + INNER_LETTER_BASE + ".\\$\\-,]" + "*";
        protected static readonly string OPTIONAL_NESTED_CLASSES = "(?:/" + IDENT_NONESCAPED + ")*";

        // leading . is for ::.ctor
        // inner . and $ are e.g. for antlr.CommonHiddenStreamToken::IToken.getColumn$PST060001A3
        // <> is for generic delegate methods like ::<>9__CachedAnonymousMethodDelegate1
        // - is for ::$$method0x600004a-1
        // , is for ::System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<TKey,TValue>>.get_Current

        /// <summary>
        /// Create the possible expansions for a pattern.
        /// </summary>
        /// <returns>1, 2, or 4 regular expressions
        /// to be used for matching a class name, a
        /// method on a class, a nested class name, and/or 
        /// a method on a nested class.</returns>
        protected static List<string> Expand(string pattern) {
            if (pattern.StartsWith("^")) {
                if (pattern.EndsWith("$")) {
                    return new List<string> { pattern };
                } else {
                    return ExpandWithSuffixes(pattern);
                }
            } else {
                return ExpandWithSuffixes(ExpandAsterisks(pattern));
            }
        }

        protected static string ExpandAsterisks(string pattern) {
            pattern = pattern.Replace(".", "[.]");
            while (pattern.Contains("**")) {
                int indexOfPath = pattern.IndexOf("**");
                int indexOfSlash = pattern.IndexOf('/');
                if (indexOfSlash >= 0 && indexOfSlash < indexOfPath) {
                    // ** is to the left of a / -> it is expanded to IDENT(/IDENT)*
                    pattern = Interpolate2(pattern, indexOfPath, INNER_PATH_ESCAPED);
                } else {
                    // ** is not to the left of a / -> it is expanded to IDENT(.IDENT)*
                    pattern = Interpolate2(pattern, indexOfPath, PATH_ESCAPED);
                }
            }
            while (pattern.Contains("*")) {
                int indexOfIdentPart = pattern.IndexOf("*");
                if (indexOfIdentPart > 0 && Regex.IsMatch(pattern[indexOfIdentPart - 1].ToString(), "[" + INNER_LETTER + "]")) {
                    pattern = Interpolate1(pattern, indexOfIdentPart, IDENT_INFIX_ESCAPED);
                } else if (indexOfIdentPart < pattern.Length - 1 && Regex.IsMatch(pattern[indexOfIdentPart + 1].ToString(), "[" + INNER_LETTER + "]")) {
                    pattern = Interpolate1(pattern, indexOfIdentPart, IDENT_PREFIX_ESCAPED);
                } else {
                    pattern = Interpolate1(pattern, indexOfIdentPart, IDENT_ESCAPED);
                }
            }

            pattern = pattern.Replace(ASTERISK_ESCAPE, "*").Replace(ASTERISK_ABBREV, "*");

            return pattern;
        }

        private static string Interpolate1(string pattern, int indexOfIdentPart, string value) {
            return pattern.Substring(0, indexOfIdentPart) + value + pattern.Substring(indexOfIdentPart + 1);
        }

        private static string Interpolate2(string pattern, int indexOfPath, string value) {
            return pattern.Substring(0, indexOfPath) + value + pattern.Substring(indexOfPath + 2);
        }

        /// <summary>
        /// Add suffixes to a regular expression prefix so that
        /// they can be used to match a class name, a
        /// method on a class, a nested class name, and/or 
        /// a method on a nested class.
        /// </summary>
        private static List<string> ExpandWithSuffixes(string pattern) {
            List<string> expanded = new List<string>();
            pattern = "^" + pattern;
            if (!pattern.Contains("::")) {
                // No :: --> add all possible methods.
                if (!pattern.Contains("/")) {
                    // If there is no slash, the class pattern part (before the ::) also includes
                    // all nested classes of the given class. Saying "only in top-level class"
                    // is currently not possible.
                    pattern = pattern + OPTIONAL_NESTED_CLASSES;
                }
                expanded.Add(pattern + "::" + METHODNAME_NONESCAPED + "$");
            } else {
                // :: --> method pattern.
                if (!pattern.Contains("/")) {
                    // If there is no slash, the class pattern part (before the ::) also includes
                    // all nested classes of the given class. Saying "only in top-level class"
                    // is currently not possible.
                    pattern = pattern.Replace("::", OPTIONAL_NESTED_CLASSES + "::");
                }
            }
            expanded.Add(pattern + "$");
            return expanded;
        }

        protected static string DebugContract(string regex) {
            return regex.Replace(FIRST_LETTER, "F")
                .Replace(INNER_LETTER, "I")
                .Replace(INNER_LETTER_AFTER_LT, "J");
        }
    }
}
