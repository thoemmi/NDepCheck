// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Linq;
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
    public class Projection : IProjectionSetElement {
        [NotNull]
        private readonly ItemType _targetItemType;

        [NotNull]
        private readonly string[] _targetSegments;

        private readonly bool _forLeftSide;
        private readonly bool _forRightSide;

        public string Source { get; }

        [NotNull]
        internal readonly ItemMatch ItemMatch;

        private int _matchCount;

        public Projection([CanBeNull] ItemType sourceItemTypeOrNull, [NotNull]ItemType targetItemType, [NotNull]string pattern,
            [CanBeNull]string[] targetSegments, bool ignoreCase, bool forLeftSide, bool forRightSide, string source = null) {
            if (targetSegments != null) {
                if (targetSegments.Length > targetItemType.Length) {
                    Log.WriteError($"Targettype {targetItemType.Name} has {targetItemType.Length} segments, but {targetSegments.Length} are defined in projection: {string.Join(",", targetSegments)}");
                    throw new ArgumentException("targetSegments length != targetItemType.Length", nameof(targetSegments));
                } else if (targetSegments.Length < targetItemType.Length) {
                    targetSegments = targetSegments.Concat(Enumerable.Range(1, targetItemType.Length - targetSegments.Length).Select(i => ""))
                                                   .ToArray();
                }
                if (targetSegments.Any(s => s == null)) {
                    throw new ArgumentException("targetSegments contains null", nameof(targetSegments));
                }
            } else {
                targetSegments = Enumerable.Range(1, targetItemType.Length).Select(i => "\\" + i).ToArray();
            }
            _targetItemType = targetItemType;
            _targetSegments = targetSegments;
            Source = source;
            _forLeftSide = forLeftSide;
            _forRightSide = forRightSide;
            ItemMatch = new ItemMatch(sourceItemTypeOrNull, pattern, ignoreCase);
        }

        /// <summary>
        /// Return projected string for some item.
        /// </summary>
        /// <param name="item">Iitem to be projected.</param>
        /// <param name="left">Item is on left side of dependency</param>
        /// <returns>Projected item; or <c>null</c> if item does not 
        /// match projection</returns>
        public Item Match([NotNull] Item item, bool left) {
            if (left && !_forLeftSide || !left && !_forRightSide) {
                return null;
            } else {
                string[] matchResultGroups = ItemMatch.Matches(item);

                if (matchResultGroups == null) {
                    return null;
                } else {
                    IEnumerable<string> targets = _targetSegments;
                    for (int i = 0; i < matchResultGroups.Length; i++) {
                        int matchResultIndex = i;
                        targets = targets.Select(s => s.Replace("\\" + (matchResultIndex + 1), matchResultGroups[matchResultIndex]));
                    }
                    targets = targets.Select(s => s.Replace("\\>", item.Order ?? ""));
                    _matchCount++;
                    return Item.New(_targetItemType, targets.Select(t => GlobalContext.ExpandHexChars(t)).ToArray()).SetOrder(item.Order);
                }
            }
        }

        public Projection[] AllProjections => new[] { this };

        public int MatchCount => _matchCount;
    }
}