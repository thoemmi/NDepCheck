using System.Collections.Generic;
using System.Linq;
using NDepCheck.Markers;

namespace NDepCheck.Matching {
    public class MarkerMatch : CountPattern<IMatcher> {
        private readonly IEnumerable<Eval> _evals;

        public MarkerMatch(string pattern, bool ignoreCase) {
            _evals = pattern.Split('&')
                            .Select(element => CreateEval(element, ignoreCase))
                            .ToArray();
        }

        public static Eval CreateEval(string pattern, bool ignoreCase) {
            return CreateEval(pattern, AbstractMarkerSet.MARKER_PATTERN, s => CreateMatcher(s, ignoreCase));
        }

        public static IMatcher CreateMatcher(string pattern, bool ignoreCase) {
            return Pattern.CreateMatcher(pattern.Trim(), 0, ignoreCase);
        }

        public bool IsMatch(IMarkerSet obj) {
            return obj.IsMatch(_evals);
        }
    }
}