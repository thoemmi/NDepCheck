using System.Collections.Generic;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public interface IMarkerSet {
        int GetValue(string marker, bool ignoreCase);
        int GetValue(IMatcher matcher);

        bool IsMatch(IEnumerable<CountPattern<IMatcher>.Eval> evals);
        IEnumerable<string> MatchingMarkers(IEnumerable<IMatcher> matchers);

        string AsFullString(int maxLength = 250);

        MutableMarkerSet CloneAsMutableMarkerSet(bool b);
    }
}