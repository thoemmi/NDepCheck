using System.Collections.Generic;

namespace NDepCheck.Markers {
    public interface IMutableMarkerSet : IMarkerSet {
        void IncrementMarker(string marker);
        void MergeWithMarkers(IReadOnlyDictionary<string, int> markerPatterns);
        void RemoveMarkers(string markerPattern, bool ignoreCase);
        void RemoveMarkers(IEnumerable<string> markerPatterns, bool ignoreCase);
        void ClearMarkers();
    }
}