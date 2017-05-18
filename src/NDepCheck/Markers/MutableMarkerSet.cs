using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public class MutableMarkerSet : AbstractMarkerSet, IMutableMarkerSet {
        [CanBeNull]
        private Dictionary<string, int> _markersOrNull;

        protected override IReadOnlyDictionary<string, int> MarkersOrNull => _markersOrNull;

        public MutableMarkerSet(bool ignoreCase, [CanBeNull] IEnumerable<string> markers) : base(ignoreCase) {
            _markersOrNull = CreateMarkerSetWithClonedDictionary(ignoreCase, markers);
        }

        public MutableMarkerSet(bool ignoreCase, [CanBeNull] IMarkerSet markersOrNull) : base(ignoreCase) {
            _markersOrNull = CreateMarkerSetWithClonedDictionary(ignoreCase,
                markersOrNull == null ? Empty(ignoreCase) : ((AbstractMarkerSet)markersOrNull).Markers);
        }

        public MutableMarkerSet(bool ignoreCase, [CanBeNull] IReadOnlyDictionary<string, int> markersOrNull) : base(ignoreCase) {
            _markersOrNull = CreateMarkerSetWithClonedDictionary(ignoreCase, markersOrNull ?? Empty(ignoreCase));
        }

        protected override int MarkerValue(string marker) {
            return _markersOrNull.Get(marker);
        }

        public void MergeWithMarkers([CanBeNull] IMarkerSet markerSet) {
            if (markerSet != null) {
                MergeWithMarkers(((AbstractMarkerSet)markerSet).Markers);
            }
        }

        public void MergeWithMarkers([CanBeNull] IReadOnlyDictionary<string, int> markers) {
            if (markers != null && markers.Any()) {
                if (_markersOrNull == null) {
                    _markersOrNull = CreateMarkerSetWithClonedDictionary(_ignoreCase, markers);
                } else {
                    if (_markersOrNull != markers) { // This can happen on a cycle
                        _markersOrNull.UnionWith(markers);
                    }
                }
            }
        }

        public void IncrementMarker([NotNull] string marker) {
            CheckMarkerFormat(marker);
            if (_markersOrNull == null) {
                _markersOrNull = new Dictionary<string, int> { { marker, 1 } };
            } else {
                _markersOrNull[marker] = _markersOrNull.Get(marker) + 1;
            }
        }

        public void SetMarker([NotNull] string marker, int value) {
            CheckMarkerFormat(marker);
            if (_markersOrNull == null) {
                _markersOrNull = new Dictionary<string, int> { { marker, value } };
            } else {
                _markersOrNull[marker] = value;
            }
        }

        public void RemoveMarkers(string markerPattern, bool ignoreCase) {
            RemoveMarkers(new[] { markerPattern }, ignoreCase);
        }

        public void RemoveMarkers([CanBeNull] IEnumerable<string> markerPatterns, bool ignoreCase) {
            if (markerPatterns != null && _markersOrNull != null) {
                IEnumerable<IMatcher> matchers = markerPatterns.Select(p => MarkerMatch.CreateMatcher(p, ignoreCase)).ToArray();
                string[] keys = _markersOrNull.Keys.Where(m => matchers.Any(ma => ma.Matches(m, null) != null)).ToArray();
                foreach (var k in keys) {
                    _markersOrNull[k] = 0;
                }
            }
        }

        public void ClearMarkers() {
            _markersOrNull = null;
        }

        public static Dictionary<string, int> ConcatOrUnionWithMarkers(AbstractMarkerSet leftMarkers,
                                                              AbstractMarkerSet rightMarkers, bool ignoreCase) {
            IReadOnlyDictionary<string, int> left = leftMarkers.Markers;
            IReadOnlyDictionary<string, int> right = rightMarkers.Markers;
            Dictionary<string, int> result = left.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, GetComparer(ignoreCase));
            if (result != right) {
                result.UnionWith(right);
            }

            // a, b, c/d, c/e + a, k, e/f, e/g = a, b, k; c/d, c/f, c/g
            foreach (var l in left.Keys.Where(l => l.Contains("/"))) {
                string leftTail = l.Substring(l.IndexOf("/", StringComparison.InvariantCulture) + 1);
                foreach (var r in right.Keys.Where(r => r.Contains("/"))) {
                    string[] rightParts = r.Split(new[] { '/' }, 2);
                    if (GetComparer(ignoreCase).Compare(leftTail, rightParts[0]) == 0) {
                        // l & r are partners!
                        result.Remove(l);
                        result.Remove(r);
                        result.Increment(left + "/" + rightParts[1], left[l] * right[r]);
                    }
                }
            }
            return result;
        }

        public static void AddComputedMarkerIfNoMarkers(List<string> markersToAdd, List<ItemMatch> fromItemMatches, List<ItemMatch> toItemMatches, string defaultName) {
            if (!markersToAdd.Any()) {
                string marker = CreateReadableDefaultMarker(fromItemMatches, toItemMatches, defaultName);
                markersToAdd.Add(marker);
                Log.WriteInfo($"... adding marker '{marker}'");
            }
        }

        public static readonly string MARKER_HELP = @"
Markers
=======

It is possible to add arbitrary 'marker strings' (or markers) items and dependencies.
Each marker is associated with an integer number.
This is useful
* to add additional information to read-in dependencies (e.g., dependencies read in
  from .Net assemblies have markers like 'inherit' or 'call');
* to add additional information in dep-checking algorithms that can be used later
  (e.g., FindCycleDeps adds a marker to each dependency on a cycle; later,
  ModifyDeps can use that marker to delete or modify the dependency, e.g. set the 
  questionable count);
* to add orders to items or dependencies;
* to add reachability counts to items (see e.g. PathWriter).

Markers on items and dependencies are persistent information that is written into Dip files.

If dependencies are aggregated into a simpler graph, e.g. by ProjectItems or
by AddTransitiveDeps, the markers of the source dependencies are combined into
a new marker set on the resulting dependency. This combination handles markers 
differently, based on whether they contain a dash ('/' - 'path markers') or not:
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
are the results of combining d/e + e/g and d/e + e/h.

Marker patterns
===============

A marker pattern is used in dependency matches and item matches.
It has the following format:

    ' singlepattern {{ & singlepattern }}

where a singlepattern checks the sum of all markers matching a 
marker pattern via one of the following patterns:

    pattern=0 or pattern==0 or ~pattern
    pattern<0
    pattern>0 or pattern
    pattern<=0
    pattern>=0
    pattern<>0 or pattern!=0
";
    }
}