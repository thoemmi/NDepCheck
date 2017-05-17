using System.Collections.Generic;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public interface IMarkerSet {
        int GetValue(string marker, bool ignoreCase);

        int GetValue(IMatcher matcher);

        bool IsMatch(IEnumerable<CountPattern<IMatcher>.Eval> evals);

        string AsFullString();

        IEnumerable<string> MatchingMarkers(IEnumerable<IMatcher> matchers);

        MutableMarkerSet CloneAsMutableMarkerSet(bool b);
    }
}