namespace NDepCheck.Markers {
    public interface IWithMarkerSet {
        IMarkerSet MarkerSet { get; }
    }

    public interface IWithMutableMarkerSet : IWithMarkerSet {
        void IncrementMarker(string marker);
        void RemoveMarkers(string markerPattern, bool ignoreCase);
    }
}