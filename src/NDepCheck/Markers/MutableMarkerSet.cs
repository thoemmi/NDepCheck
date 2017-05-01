using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public class MutableMarkerSet : AbstractMarkerSet, IMutableMarkerSet {
        [CanBeNull]
        private HashSet<string> _markersOrNull;

        protected override IEnumerable<string> MarkersOrNull => _markersOrNull;

        public MutableMarkerSet(bool ignoreCase, [CanBeNull] IEnumerable<string> markers) : base(ignoreCase) {
            _markersOrNull = CreateSet(ignoreCase, markers);
        }

        protected override bool MarkersContains(string marker) {
            return _markersOrNull != null && _markersOrNull.Contains(marker);
        }

        public bool UnionWithMarkers([CanBeNull] IEnumerable<string> markers) {
            if (markers != null && markers.Any()) {
                if (_markersOrNull == null) {
                    _markersOrNull = CreateSet(_ignoreCase, markers);
                } else {
                    _markersOrNull.UnionWith(markers);
                }
                return true;
            } else {
                return false;
            }
        }

        public bool AddMarker([NotNull] string marker) {
            if (_markersOrNull == null) {
                _markersOrNull = CreateSet(_ignoreCase, new[] { marker });
                return true;
            } else {
                return _markersOrNull.Add(marker);
            }
        }

        public bool RemoveMarkers(string markerPattern, bool ignoreCase) {
            return RemoveMarkers(new[] { markerPattern }, ignoreCase);
        }

        public bool RemoveMarkers([CanBeNull] IEnumerable<string> markerPatterns, bool ignoreCase) {
            if (markerPatterns != null && _markersOrNull != null) {
                IEnumerable<IMatcher> matchers =
                    markerPatterns.Select(p => MarkerMatch.CreateMatcher(p, ignoreCase)).ToArray();
                _markersOrNull.RemoveWhere(m => matchers.Any(ma => ma.Matches(m, null) != null));
                return NormalizeMarkersOrNullAndSignalChange();
            } else {
                return false;
            }
        }

        private bool NormalizeMarkersOrNullAndSignalChange() {
            if (_markersOrNull != null && _markersOrNull.Count == 0) {
                _markersOrNull = null;
            }
            return true;
        }

        public bool ClearMarkers() {
            if (_markersOrNull != null) {
                _markersOrNull = null;
                return true;
            } else {
                return false;
            }
        }

        public static IEnumerable<string> ConcatOrUnionWithMarkers(IEnumerable<string> left, IEnumerable<string> right, bool ignoreCase) {
            var result = new HashSet<string>(left);
            result.UnionWith(right);
            // a, b, c/d, c/e + a, k, e/f, e/g = a, b, k; c/d, c/f, c/g
            foreach (var l in left.Where(l => l.Contains("/"))) {
                string leftTail = l.Substring(l.IndexOf("/", StringComparison.InvariantCulture) + 1);
                foreach (var r in right.Where(r => r.Contains("/"))) {
                    string[] rightParts = r.Split(new[] { '/' }, 2);
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

        public static void AddComputedMarkerIfNoMarkers(List<string> markersToAdd, List<ItemMatch> fromItemMatches, List<ItemMatch> toItemMatches, string defaultName) {
            if (!markersToAdd.Any()) {
                var fromPart = FirstReadableName(fromItemMatches) ?? defaultName;
                var toPart = FirstReadableName(toItemMatches) ?? defaultName;
                string marker = fromPart + "_" + toPart;
                markersToAdd.Add(marker);
                Log.WriteInfo($"... adding marker '{marker}'");
            }
        }

        private static string FirstReadableName(List<ItemMatch> itemMatches) {
            return itemMatches
                .SelectMany(m => m.ItemPattern.Matchers)
                .Select(m => Regex.Replace(m.ToString(), @"[^\p{L}\p{N}_]", "")) // Replace anything not letter, number or _ with nothing
                .FirstOrDefault(s => !String.IsNullOrWhiteSpace(s));
        }

        public static readonly string MARKER_HELP = @"
Markers
=======

It is possible to add arbitrary 'marker strings' (or markers) items and dependencies.
This is useful
* to add additional information to read-in dependencies (e.g., dependencies read in
  from .Net assemblies have markers like 'inherit' or 'call')
* to add additional information in dep-checking algorithms that can be used later
  (e.g., FindCycleDeps adds a marker to each dependency on a cycle; later,
  ModifyDeps can use that marker to delete or modify the dependency, e.g. set the 
  questionable count).

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

where a singlepattern checks for the presence or absence of a marker:

    marker      marker is present
    ~marker     marker is absent
";
    }
}