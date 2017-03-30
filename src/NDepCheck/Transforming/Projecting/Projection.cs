// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Projecting {
    /// <remarks>
    /// This class knows how to "abstract" an item in a
    /// dependency (using item or used item) to a - usually
    /// much shorter - name used in the graph output. For 
    /// example, most of the time classes are abstracted
    /// to their namespace, or to a higher namespace,
    /// probably without a common project prefix; or
    /// to the assembly where they reside.
    /// </remarks>
    public class Projection : Pattern, IProjectionSetElement {
        [NotNull]
        private readonly ItemType _sourceItemType;

        [NotNull]
        private readonly ItemType _targetItemType;

        [NotNull]
        private readonly string[] _targetSegments;

        public bool ForLeftSide { get; }
        public bool ForRightSide { get; }

        private readonly bool _isInner;

        [NotNull]
        private readonly IMatcher[] _matchers;

        private int _matchCount;

        // GraphAbstractions are created (because of
        // extension rules) by the factory method
        // CreateGraphAbstractions().
        public Projection([NotNull]ItemType sourceItemType, [NotNull]ItemType targetItemType, [NotNull]string pattern, [CanBeNull]string[] targetSegments, bool isInner, bool ignoreCase, bool forLeftSide, bool forRightSide) {
            if (targetSegments != null) {
                if (targetItemType.Length != targetSegments.Length) {
                    Log.WriteError($"Targettype {targetItemType.Name} has {targetItemType.Length} segments, but {targetSegments.Length} are defined in projection: {string.Join(",", targetSegments)}");
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
            ForLeftSide = forLeftSide;
            ForRightSide = forRightSide;
            _matchers = CreateMatchers(sourceItemType, pattern, 0, ignoreCase);
        }

        public int MatchCount => _matchCount;

        /// <summary>
        /// Return abstracted string for some item.
        /// </summary>
        /// <param name="item">Name of item to be abstracted.</param>
        /// <param name="left">Item is on left side of dependency</param>
        /// <returns>Abstracted name; or <c>null</c> if name does not 
        /// match abstraction</returns>
        public Item Match([NotNull] Item item, bool left) {
            if (left && !ForLeftSide || !left && !ForRightSide) {
                return null;
            } else {
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
                    return Item.New(_targetItemType, _isInner, targets.Select(t => ExpandHexChars(t)).ToArray());
                }
            }
        }

        public static string ExpandHexChars(string s) {
            return Regex.Replace(s, "%[0-9a-fA-F][0-9a-fA-F]", m => "" + (char)int.Parse(m.Value.Substring(1), NumberStyles.HexNumber));
        }

        public IEnumerable<Projection> AllProjections {
            get { yield return this; }
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