using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Markers {
    public class ReadOnlyMarkerSet : AbstractMarkerSet {
        [CanBeNull]
        private readonly HashSet<string> _markersOrNull;

        protected override ISet<string> MarkersOrNull => _markersOrNull;

        public ReadOnlyMarkerSet(bool ignoreCase, [CanBeNull] IEnumerable<string> markers) : base(ignoreCase) {
            _markersOrNull = CreateSet(ignoreCase, markers);
        }
    }
}