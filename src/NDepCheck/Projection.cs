// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Linq;
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
    public class Projection : Pattern {
        [NotNull]
        private readonly ItemType _sourceItemType;

        [NotNull]
        private readonly ItemType _targetItemType;

        [NotNull]
        private readonly string[] _targetSegments;

        private readonly bool _isInner;

        [NotNull]
        private readonly IMatcher[] _matchers;

        private int _matchCount;

        // GraphAbstractions are created (because of
        // extension rules) by the factory method
        // CreateGraphAbstractions().
        public Projection([NotNull]ItemType sourceItemType, [NotNull]ItemType targetItemType, [NotNull]string pattern, [CanBeNull]string[] targetSegments, bool isInner, bool ignoreCase) {
            if (targetSegments != null) {
                if (targetItemType.Length != targetSegments.Length) {
                    throw new ArgumentException("targetSegments length != targetItemType.Length", nameof(targetSegments));
                }
                if (targetSegments.Any(s => s == null)) {
                    throw new ArgumentException("targetSegments contains null", nameof(targetSegments));
                }
            } else {
                targetSegments = Enumerable.Range(1, targetItemType.Length).Select(i => "\\" + i).ToArray();
            }
            _sourceItemType = sourceItemType;
            _targetItemType = targetItemType;
            _targetSegments = targetSegments;
            _isInner = isInner;
            _matchers = CreateMatchers(sourceItemType, pattern, 0, ignoreCase);
        }

        public int MatchCount => _matchCount;

        /// <summary>
        /// Return abstracted string for some item.
        /// </summary>
        /// <param name="item">Name of item to be abstracted.</param>
        /// <returns>Abstracted name; or <c>null</c> if name does not 
        /// match abstraction</returns>
        ///// <param name="skipCache"></param>
        public Item Match([NotNull]Item item) {
            string[] matchResultGroups = Match(_sourceItemType, _matchers, item);

            if (matchResultGroups == null) {
                return null;
            } else {
                _matchCount++;
                IEnumerable<string> targets = _targetSegments;
                for (int i = 0; i < matchResultGroups.Length; i++) {
                    int matchResultIndex = i;
                    targets = targets.Select(s => s.Replace("\\" + (matchResultIndex + 1), matchResultGroups[matchResultIndex]));
                }
                return Item.New(_targetItemType, _isInner, targets.ToArray());
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