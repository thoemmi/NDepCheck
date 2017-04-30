using System.Collections.Generic;

namespace NDepCheck.Markers {
    public interface IMutableMarkerSet : IMarkerSet {
        bool AddMarker(string marker);
        bool UnionWithMarkers(IEnumerable<string> markerPatterns);
        bool RemoveMarkers(string markerPattern, bool ignoreCase);
        bool RemoveMarkers(IEnumerable<string> markerPatterns, bool ignoreCase);
        bool ClearMarkers();
    }
}