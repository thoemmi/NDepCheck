using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public abstract class AbstractMarkerSet : IMarkerSet {
        protected readonly bool _ignoreCase;
        // TODO: Replace with sharing implementation of string sets to save space and maybe time
        [CanBeNull]
        protected abstract /*IReadOnlySet<string>*/ IEnumerable<string> MarkersOrNull { get; }

        // For performance, this is delegated to subclasses which can check an internal set more quickly
        [Pure]
        protected abstract bool MarkersContains(string marker);

        protected AbstractMarkerSet(bool ignoreCase) {
            _ignoreCase = ignoreCase;
        }

        protected static HashSet<string> CreateSet(bool ignoreCase, [CanBeNull] IEnumerable<string> markers) {
            IEnumerable<string> cleanMarkers =
                (markers ?? Enumerable.Empty<string>()).Select(s => s.Trim())
                .Where(s => s != "")
                .Select(s => s.Contains("/") ? s : String.Intern(s));
            return cleanMarkers.Any() ? new HashSet<string>(cleanMarkers, GetComparer(ignoreCase)) : null;
        }

        public IEnumerable<string> Markers => MarkersOrNull ?? Enumerable.Empty<string>();

        public static StringComparer GetComparer(bool ignoreCase) {
            return ignoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture;
        }

        protected StringComparison GetComparison() {
            return _ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
        }

        protected StringComparer GetComparer() {
            return GetComparer(_ignoreCase);
        }

        public bool IsMatch(IEnumerable<IMatcher> present, IEnumerable<IMatcher> absent) {
            if (MarkersOrNull == null) {
                return !present.Any();
            } else {
                foreach (var m in present) {
                    // For performance, EqualsMatcher is handled separately - no All loop needed!
                    EqualsMatcher em = m as EqualsMatcher;
                    if (em != null) {
                        if (!MarkersContains(em.MatchString)) {
                            return false;
                        }
                    } else {
                        if (MarkersOrNull.All(s => m.Matches(s, null) == null)) {
                            return false;
                        }
                    }
                }
                foreach (var m in absent) {
                    // For performance, EqualsMatcher is handled separately - no Any loop needed!
                    EqualsMatcher em = m as EqualsMatcher;
                    if (em != null) {
                        if (MarkersContains(em.MatchString)) {
                            return false;
                        }
                    } else {
                        if (MarkersOrNull.Any(s => m.Matches(s, null) != null)) {
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        public static string CreateReadableDefaultMarker(IEnumerable<ItemMatch> fromItemMatches, IEnumerable<ItemMatch> toItemMatches, string defaultName) {
            string fromPart = FirstReadableName(fromItemMatches);
            string toPart = FirstReadableName(toItemMatches);
            string marker = fromPart == null
                ? (toPart != null ? "." + toPart : ToMarker(defaultName))
                : (toPart == null ? "." + fromPart : "." + fromPart + "_" + toPart);
            return marker;
        }

        private static string FirstReadableName(IEnumerable<ItemMatch> itemMatches) {
            return itemMatches
                .SelectMany(m => m.ItemPattern.Matchers)
                .Select(m => ToMarker(m.ToString()))
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }

        private static string ToMarker(string m) {
            // Replace anything not letter, number or one of _ . / \\ with nothing
            return Regex.Replace(m, @"[^\p{L}\p{N}_./\\]", "");
        }
    }
}