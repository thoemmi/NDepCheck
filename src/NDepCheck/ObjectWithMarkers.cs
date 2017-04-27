using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Transforming;
using NDepCheck.Transforming.CycleChecking;
using NDepCheck.Transforming.DependencyCreating;
using NDepCheck.Transforming.Modifying;
using NDepCheck.Transforming.Projecting;

namespace NDepCheck {
    public abstract class ObjectWithMarkers {
        private readonly bool _ignoreCase;
        [CanBeNull]
        private HashSet<string> _markersOrNull;

        protected ObjectWithMarkers(bool ignoreCase, [CanBeNull] IEnumerable<string> markers) {
            _ignoreCase = ignoreCase;
            IEnumerable<string> cleanMarkers = (markers ?? Enumerable.Empty<string>()).Select(s => s.Trim()).Where(s => s != "").Select(s => s.Contains("/") ? s : String.Intern(s));
            _markersOrNull = cleanMarkers.Any() ? CreateHashSet(cleanMarkers) : null;
        }

        private HashSet<string> CreateHashSet(IEnumerable<string> cleanMarkers) {
            return new HashSet<string>(cleanMarkers, GetComparer());
        }

        public IEnumerable<string> Markers => _markersOrNull ?? Enumerable.Empty<string>();

        protected abstract void MarkersHaveChanged();

        internal ObjectWithMarkers UnionWithMarkers([CanBeNull] IEnumerable<string> markers) {
            if (markers != null && markers.Any()) {
                if (_markersOrNull == null) {
                    _markersOrNull = CreateHashSet(markers);
                } else {
                    _markersOrNull.UnionWith(markers);
                }
                MarkersHaveChanged();
            }
            return this;
        }

        public ObjectWithMarkers AddMarker([NotNull] string marker) {
            if (_markersOrNull == null) {
                _markersOrNull = CreateHashSet(new[] { marker });
                MarkersHaveChanged();
            } else {
                if (_markersOrNull.Add(marker)) {
                    MarkersHaveChanged();
                }
            }
            return this;
        }

        public ObjectWithMarkers RemoveMarker(string marker) {
            if (_markersOrNull != null) {
                if (_markersOrNull.Remove(marker)) {
                    if (_markersOrNull.Count == 0) {
                        _markersOrNull = null;
                    }
                    MarkersHaveChanged();
                }
            }
            return this;
        }

        public ObjectWithMarkers RemoveMarkers([CanBeNull] IEnumerable<string> markers) {
            if (markers != null && _markersOrNull != null) {
                _markersOrNull.ExceptWith(markers);
                if (_markersOrNull.Count == 0) {
                    _markersOrNull = null;
                }
                MarkersHaveChanged();
            }
            return this;
        }

        public ObjectWithMarkers ClearMarkers() {
            if (_markersOrNull != null) {
                MarkersHaveChanged();
                _markersOrNull = null;
            }
            return this;
        }

        public bool IsMatch(MarkerPattern pattern) {
            if (_markersOrNull == null) {
                return pattern.Present.Count == 0;
            } else {
                return pattern.Present.IsSubsetOf(_markersOrNull) && !pattern.Absent.Overlaps(_markersOrNull);
            }
        }

        public static StringComparer GetComparer(bool ignoreCase) {
            return ignoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture;
        }

        protected StringComparison GetComparison() {
            return _ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
        }

        protected StringComparer GetComparer() {
            return _ignoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture;
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
                markersToAdd.Add(fromPart + "_" + toPart);
            }
        }

        private static string FirstReadableName(List<ItemMatch> itemMatches) {
            return itemMatches
                .SelectMany(m => m.ItemPattern.Matchers)
                .Select(m => Regex.Replace(m.ToString(), @"[^\p{L}\p{N}_]", "")) // Replace anything not letter, number or _ with nothing
                .FirstOrDefault(s => !String.IsNullOrWhiteSpace(s));
        }

        public static readonly string MARKER_HELP = $@"
Markers
=======

It is possible to add arbitrary 'marker strings' (or markers) items and dependencies.
This is useful
* to add additional information to read-in dependencies (e.g., dependencies read in
  from .Net assemblies have markers like 'inherit' or 'call')
* to add additional information in dep-checking algorithms that can be used later
  (e.g., {typeof(FindCycleDeps).Name}  adds a marker to each dependency on a cycle; later,
  {typeof(ModifyDeps).Name}  can use that marker to delete or modify the dependency, e.g. set the 
  questionable count).

Markers on items and dependencies are persistent information that is written into Dip files.

If dependencies are aggregated into a simpler graph, e.g. by {typeof(ProjectItems).Name} or
by {typeof(AddTransitiveDeps).Name}, the markers of the source dependencies are combined into
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