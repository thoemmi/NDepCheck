using JetBrains.Annotations;
using NDepCheck.Markers;

namespace NDepCheck.Transforming.PathFinding {
    public class AbstractPathMarker {
        public const int IS_START = 1 << 0;
        public const int IS_END = 1 << 1;
        public const int IS_MATCHED_BY_COUNT_MATCH = 1 << 2;
        public const int IS_LOOPBACK = 1 << 3;
        public const int INDEX_SHIFT = 4;

        protected static void MarkPathElement([NotNull] IWithMutableMarkerSet e, [NotNull] string indexedMarker, int i, 
            bool isStart, bool isEnd, bool isMatchedByCountMatch, bool isLoopBack) {
            e.SetMarker(indexedMarker, (i << INDEX_SHIFT)
                                       | (isStart ? IS_START : 0)
                                       | (isEnd ? IS_END : 0)
                                       | (isMatchedByCountMatch ? IS_MATCHED_BY_COUNT_MATCH : 0)
                                       | (isLoopBack ? IS_LOOPBACK : 0));
        }
    }
}