// (c) HMMüller 2006...2010

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NDepCheck {
    /// <remarks>
    /// This class knows how to "abstract" an item in a
    /// dependency (using item or used item) to a - usually
    /// much shorter - name used in the graph output. For 
    /// example, most of the time classes are abstracted
    /// to their namespace, or to a higher namespace,
    /// probably without a common project prefix.
    /// </remarks>
    internal class GraphAbstraction : Pattern {
        private readonly Regex _rex;
        // GraphAbstractions are created (because of
        // extension rules) by factory method
        // CreateGraphAbstractions().
        private GraphAbstraction(string pattern) {
            _rex = new Regex(pattern);
        }

        /// <summary>
        /// Create <c>GraphAbstraction</c>s from a pattern
        /// (part of graph line without the %).
        /// </summary>
        public static List<GraphAbstraction> CreateGraphAbstractions(string pattern) {
            List<string> expandedUsedItemPattern = Expand(pattern);
            return expandedUsedItemPattern.Select(ed => new GraphAbstraction(ed)).ToList();
        }

        /// <summary>
        /// Return abstracted string for some item.
        /// </summary>
        /// <param name="itemName">Name of item to be abstracted.</param>
        /// <returns>Abstracted name; or <c>null</c> if name does not 
        /// match abstraction</returns>
        public string Match(string itemName) {
            Match m = _rex.Match(itemName);
            if (m.Success) {
                if (m.Groups.Count < 2) {
                    throw new ApplicationException("Graph definition " + _rex + " does not find a matching group for input '" +
                                                   itemName + "'");
                }
                return m.Groups[1].Value;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Show <c>GraphAbstraction</c> as regular 
        /// expression in verbose mode (the user needs
        /// this to find problems when the graph output
        /// is not as expected).
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return _rex.ToString();
        }
    }
}