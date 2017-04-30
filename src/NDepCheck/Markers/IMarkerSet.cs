using System.Collections.Generic;
using NDepCheck.Matching;

namespace NDepCheck.Markers {
    public interface IMarkerSet {
        IEnumerable<string> Markers { get; }
        bool IsMatch(IEnumerable<IMatcher> present, IEnumerable<IMatcher> absent);
    }
}