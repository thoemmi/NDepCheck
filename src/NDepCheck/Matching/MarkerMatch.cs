using System.Collections.Generic;
using System.Linq;
using NDepCheck.Markers;

namespace NDepCheck.Matching {
    public class MarkerMatch : Pattern {
        private readonly IEnumerable<IMatcher> _present;
        private readonly IEnumerable<IMatcher> _absent;

        public MarkerMatch(string s, bool ignoreCase) {
            var present = new List<IMatcher>();
            var absent = new List<IMatcher>();
            string[] elements = s.Split('&');
            foreach (var e in elements) {
                string element = e.Trim();
                if (element == "~" || element == "") {
                    // ignore
                } else if (element.StartsWith("~")) {
                    absent.Add(CreateMatcher(element.Substring(1).Trim(), 0, ignoreCase));
                } else {
                    present.Add(CreateMatcher(element, ignoreCase));
                }
            }
            _present = present;
            _absent = absent;
        }

        public static IMatcher CreateMatcher(string pattern, bool ignoreCase) {
            return CreateMatcher(pattern.Trim(), 0, ignoreCase);
        }

        public MarkerMatch(IEnumerable<string> present, IEnumerable<string> absent, bool ignoreCase) {
            _present = present.Select(s => CreateMatcher(s, 0, ignoreCase));
            _absent = absent.Select(s => CreateMatcher(s, 0, ignoreCase));
        }

        public bool IsMatch(IMarkerSet obj) {
            return obj.IsMatch(_present, _absent);
        }

        public bool MatchesAlike(MarkerMatch other) {
            return MatchAlike(_present, other._present)
                && MatchAlike(other._present, _present)
                && MatchAlike(_absent, other._absent)
                && MatchAlike(other._absent, _absent);
        }

        private static bool MatchAlike(IEnumerable<IMatcher> left, IEnumerable<IMatcher> right) {
            return left.All(p => right.Any(q => p.MatchesAlike(q)));
        }
    }
}