using System.Collections.Generic;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public interface IMarkerSet {
        int GetValue(string marker, bool ignoreCase);

        int GetValue(IMatcher matcher);

        bool IsMatch(IEnumerable<CountPattern<IMatcher>.Eval> evals);

        string AsFullString();

        IEnumerable<string> MatchingMarkerStrings(IMatcher matcher);

        MutableMarkerSet CloneAsMutableMarkerSet(bool b);
    }
}