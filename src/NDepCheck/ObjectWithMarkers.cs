using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class ObjectWithMarkers {
        private HashSet<string> _markersOrNull;

        protected ObjectWithMarkers([CanBeNull] IEnumerable<string> markers) {
            IEnumerable<string> cleanMarkers = (markers ?? Enumerable.Empty<string>()).Select(s => s.Trim()).Where(s => s != "");
            _markersOrNull = cleanMarkers.Any() ? new HashSet<string>(cleanMarkers) : null;
        }

        public IEnumerable<string> Markers => _markersOrNull ?? Enumerable.Empty<string>();

        internal void AddMarkers(IEnumerable<string> markers) {
            if (_markersOrNull == null) {
                _markersOrNull = new HashSet<string>();
            }
            _markersOrNull.UnionWith(markers);
        }

        public void AddMarker(string marker) {
            if (_markersOrNull == null) {
                _markersOrNull = new HashSet<string>();
            }
            _markersOrNull.Add(marker);
        }

        public void RemoveMarker(string marker) {
            if (_markersOrNull != null) {
                _markersOrNull.Remove(marker);
                if (_markersOrNull.Count == 0) {
                    _markersOrNull = null;
                }
            }
        }

        public void ClearMarkers() {
            _markersOrNull = null;
        }

        public bool Matches(MarkerPattern pattern) {
            if (_markersOrNull == null) {
                return pattern.Present.Count == 0;
            } else {
                return pattern.Present.IsSubsetOf(_markersOrNull) && !pattern.Absent.Overlaps(_markersOrNull);
            }
        }
    }
}