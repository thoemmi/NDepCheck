// (c) HMMüller 2006...2015

using JetBrains.Annotations;

namespace NDepCheck {
    /// <remarks>
    /// This class knows how to "abstract" an item in a
    /// dependency (using item or used item) to a - usually
    /// much shorter - name used in the graph output. For 
    /// example, most of the time classes are abstracted
    /// to their namespace, or to a higher namespace,
    /// probably without a common project prefix; or
    /// to the assembly where they reside.
    /// </remarks>
    public class GraphAbstraction : Pattern {
        [NotNull]
        private readonly ItemType _itemType;
        private readonly bool _isInner;
        [NotNull]
        private readonly IMatcher[] _matchers;
        private int _matchCount;
 
        // GraphAbstractions are created (because of
        // extension rules) by the factory method
        // CreateGraphAbstractions().
        public GraphAbstraction([NotNull]ItemType itemType, [NotNull]string pattern, bool isInner, bool ignoreCase) {
            _itemType = itemType;
            _isInner = isInner;
            _matchers = CreateMatchers(itemType, pattern, 0, ignoreCase);
        }

        public int MatchCount => _matchCount;

        /// <summary>
        /// Return abstracted string for some item.
        /// </summary>
        /// <param name="item">Name of item to be abstracted.</param>
        /// <param name="isInner"><c>true</c> if GraphAbstraction_ has IsInner (i.e., was declared with !)</param>
        /// <returns>Abstracted name; or <c>null</c> if name does not 
        /// match abstraction</returns>
        ///// <param name="skipCache"></param>
        public string Match([NotNull]Item item, out bool isInner/*, Dictionary<Tuple<string, int>, GraphAbstraction> skipCache = null*/) {
            isInner = _isInner;
            
            string[] matchResult = Match(_itemType, _matchers, item);

            if (matchResult == null) {
                return null;
            } else {
                _matchCount++;
                return string.Join(":", matchResult);
            }
        }

        /////// <summary>
        /////// Show <c>GraphAbstraction_</c> as regular 
        /////// expression in verbose mode (the user needs
        /////// this to find problems when the graph output
        /////// is not as expected).
        /////// </summary>
        /////// <returns></returns>
        ////public override string ToString() {
        ////    return _rex.ToString();
        ////}
    }
}