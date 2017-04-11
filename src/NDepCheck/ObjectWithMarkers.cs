using System;
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

        internal void UnionWithMarkers(IEnumerable<string> markers) {
            if (_markersOrNull == null) {
                _markersOrNull = new HashSet<string>(markers);
            } else {
                _markersOrNull.UnionWith(markers);
            }
        }

        public void AddMarker(string marker) {
            if (_markersOrNull == null) {
                _markersOrNull = new HashSet<string> { marker };
            } else {
                _markersOrNull.Add(marker);
            }
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

        public static StringComparer GetComparer(bool ignoreCase) {
            return ignoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture;
        }

        public static IEnumerable<string> ConcatOrUnionWithMarkers(IEnumerable<string> left, IEnumerable<string> right, bool ignoreCase) {
            var result = new HashSet<string>(left);
            result.UnionWith(right);
            // a, b, c/d, c/e + a, k, e/f, e/g = a, b, k; c/d, c/f, c/g
            foreach (var l in left.Where(l => l.Contains("/"))) {
                string leftTail = l.Substring(l.IndexOf("/", StringComparison.InvariantCulture) + 1);
                foreach (var r in right.Where(r => r.Contains("/"))) {
                    string[] rightParts = r.Split(new[] {'/'}, 2);
                    if (GetComparer(ignoreCase).Compare(leftTail, rightParts[0]) == 0) {
                        // l & r are partners!
                        result.Remove(l);
                        result.Remove(r);
                        result.Add(left + "/" + rightParts[1]);
                    }
                }
            }
            return result;
        }

        public const string HELP = @"
It is possible to add arbitrary 'marker strings' (or markers) items and dependencies.
This is useful
* to add additional information to read-in dependencies (e.g., dependencies read in
  from .Net assemblies have markers like 'inherit' or 'call')
* to add additional information in dep-checking algorithms that can be used later
  (e.g., FindCycleDeps adds a marker to each dependency on a cycle; later,
  ModifyDeps can use that marker to delete or modify the dependency, e.g. set the 
  questionable count).

Markers on items are transient information that is not written to Dip files.
Markers on dependencies are persistent information that is written into Dip files.

If dependencies are aggregated into a simpler graph, e.g. by ProjectDeps or
by AddAssociativeDeps, the markers of the source dependencies are combined into
a new marker set on the resulting dependency. This combination handles markers 
differently, based on whether they contain a dash ('/'):
* If a 'left' dependency (where 'left' is defined by the plugin) contains
  a marker whose last part delimited by / is the same as a marker's first
  part in a 'right' dependency, the new edge gets a marker that is the
  concatenation of both, with the common part present only once.
  E.g., a/b/c and c/d are combined to a/b/c/d.
* Markers without /, and markers with / which do not find a 'partner',
  are simply copied into the result edge.
For example, the two marker sets
  a, b/c, d/e  and  f, e/g, e/h, g/h
are combined to
  a, b/c, f, g/h, d/g, d/h
The first four are without / or did not find partners. The last two
are the results of combining d/e + e/g and d/e + e/h.";
    }
}