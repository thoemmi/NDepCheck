using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public abstract class AbstractMarkerSet : IMarkerSet {
        protected readonly bool _ignoreCase;
        // TODO: Replace with sharing implementation of string sets to save space and maybe time
        [CanBeNull]
        protected abstract /*IReadOnlySet<string>*/ ISet<string> MarkersOrNull { get; }

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
                    if (MarkersOrNull.All(s => m.Matches(s) == null)) {
                        return false;
                    }
                }
                foreach (var m in absent) {
                    if (MarkersOrNull.Any(s => m.Matches(s) != null)) {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}