using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Markers {
    public class ReadOnlyMarkerSet : AbstractMarkerSet {
        [CanBeNull]
        private readonly Dictionary<string,int> _markersOrNull;

        protected override IReadOnlyDictionary<string,int> MarkersOrNull => _markersOrNull;

        public ReadOnlyMarkerSet(bool ignoreCase, [CanBeNull] IEnumerable<string> markers) : base(ignoreCase) {
            _markersOrNull = CreateMarkerSetWithClonedDictionary(ignoreCase, markers);
        }

        public ReadOnlyMarkerSet(bool ignoreCase, [NotNull] IReadOnlyDictionary<string, int> markers) : base(ignoreCase) {
            _markersOrNull = CreateMarkerSetWithClonedDictionary(ignoreCase, markers);
        }

        protected override int MarkerValue(string marker) {
            return _markersOrNull.Get(marker);
        }
    }
}