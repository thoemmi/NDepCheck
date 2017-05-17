using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public abstract class AbstractMarkerSet : IMarkerSet {
        public const string MARKER_PATTERN = @"^[\p{L}\p{N}_./\\]+$";

        private static readonly Dictionary<string, int> _empty = new Dictionary<string, int>(StringComparer.InvariantCulture);
        private static readonly Dictionary<string, int> _emptyIgnoreCase = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

        protected readonly bool _ignoreCase;
        // TODO: Replace with sharing implementation of string sets to save space and maybe time
        [CanBeNull]
        protected abstract IReadOnlyDictionary<string, int> MarkersOrNull {
            get;
        }

        protected static void CheckMarkerFormat(string marker) {
            if (!Regex.IsMatch(marker, MARKER_PATTERN)) {
                throw new ArgumentException($"Invalid marker '{marker}'");
            }
        }

        // For performance, this is delegated to subclasses which can check an internal set more quickly
        [Pure]
        protected abstract int MarkerValue(string marker);

        protected AbstractMarkerSet(bool ignoreCase) {
            _ignoreCase = ignoreCase;
        }

        protected static Dictionary<string, int> CreateMarkerSetWithClonedDictionary(bool ignoreCase,
            [NotNull] IReadOnlyDictionary<string, int> markers) {
            return markers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, GetComparer(ignoreCase));
        }

        public static Dictionary<string, int> CreateMarkerSetWithClonedDictionary(bool ignoreCase, [CanBeNull] IEnumerable<string> markers) {
            char[] split = { '=' };
            Dictionary<string, int> cleanMarkers =
                (markers ?? Enumerable.Empty<string>()).Select(s => s.Trim())
                .Where(s => s != "")
                .Select(s => {
                    string[] parts = s.Split(split, 2);
                    int count;
                    if (parts.Length <= 1 || !int.TryParse(parts[1], out count)) {
                        count = 1;
                    }
                    string marker = parts[0];
                    CheckMarkerFormat(marker);
                    return new {
                        marker, count
                    };
                })
                .GroupBy(mc => mc.marker)
                .ToDictionary(mcs => mcs.Key.Contains("/") ? mcs.Key : string.Intern(mcs.Key), mcs => mcs.Sum(mc => mc.count));
            return cleanMarkers.Any() ? CreateMarkerSetWithClonedDictionary(ignoreCase, cleanMarkers) : null;
        }

        public IReadOnlyDictionary<string, int> Markers => MarkersOrNull ?? Empty(_ignoreCase);

        protected static IReadOnlyDictionary<string, int> Empty(bool ignoreCase) => ignoreCase ? _emptyIgnoreCase : _empty;

        public static StringComparer GetComparer(bool ignoreCase) {
            return ignoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture;
        }

        protected StringComparison GetComparison() {
            return _ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
        }

        protected StringComparer GetComparer() {
            return GetComparer(_ignoreCase);
        }

        public bool IsMatch(IEnumerable<CountPattern<IMatcher>.Eval> evals) {
            return evals.All(e => e.Predicate(GetValue(e.LeftOrNullForConstant), GetValue(e.RightOrNullForConstant)));
        }

        public int GetValue(IMatcher m) {
            int value;
            if (m == null) {
                value = 0;
            } else {
                EqualsMatcher em = m as EqualsMatcher;
                value = em != null
                    ? MarkerValue(em.MatchString)
                    : Markers.Where(kvp => m.Matches(kvp.Key, null) != null).Sum(kvp => kvp.Value);
            }
            return value;
        }

        public int GetValue(string marker, bool ignoreCase) {
            return GetValue(new EqualsMatcher(marker, ignoreCase));
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

        public string AsFullString(int maxLength = 250) {
            IEnumerable<KeyValuePair<string, int>> nonZeroMarkers = Markers.Where(kvp => kvp.Value != 0);
            if (nonZeroMarkers.Any()) {
                var sb = new StringBuilder();
                string sep = "'";
                foreach (var kvp in nonZeroMarkers.OrderBy(kvp => kvp.Key)) {
                    sb.Append(sep);
                    if (sb.Length > 100 && sb.Length > maxLength) {
                        sb.Append("...");
                        break;
                    }
                    sb.Append(kvp.Key + (kvp.Value == 1 ? "" : "=" + kvp.Value));
                    sep = "+";
                }
                return sb.ToString();
            } else {
                return "";
            }
        }

        public IEnumerable<string> MatchingMarkers(IEnumerable<IMatcher> matchers) {
            return Markers.Where(kvp => kvp.Value != 0 && matchers.Any(matcher => matcher.Matches(kvp.Key, null) != null))
                          .Select(kvp => kvp.Key);
        }

        public MutableMarkerSet CloneAsMutableMarkerSet(bool b) {
            return new MutableMarkerSet(_ignoreCase, Markers);
        }
    }
}