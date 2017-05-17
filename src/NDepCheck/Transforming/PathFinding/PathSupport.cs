using JetBrains.Annotations;
using NDepCheck.Markers;

namespace NDepCheck.Transforming.PathFinding {
    /// <summary>
    /// Currently, NDepCheck does not have a concept of "paths". Rather, such information (e.g. to recognize
    /// and output cycles; or paths going from some item type to another) is encoded into marker on the
    /// dependencies. Each path is assumed to have its own marker; typically, such markers consist of
    /// a prefix and a runner count, e.g. "C0", "C1", ..., "C9", "C10", "C11", ..., for cycles.
    /// There is no concept of paths of length 0; each path must have at least one dependency.
    /// 
    /// Only the dependencies of paths are marked (with one exception - see below), as follows:
    /// * All dependencies of a path with marker M contain a marker value for M greater than 0.
    /// * The order of the markers values designates the order of the dependencies on the path.
    /// * The final 4 bits of each path value are bits used for the following purposes:
    /// ** last bit (1 &lt;&lt; 0): 1 if the dependency is the first one on the path ("start")
    /// **          (1 &lt;&lt; 1): 1 if the dependency is the last one on the path ("end"); on a 
    ///                             path of length 1, both this and the last bit are set.
    /// **          (1 &lt;&lt; 2): 1 if the dependency or its UsedItem is matched by a "count match"
    /// **          (1 &lt;&lt; 3): 1 if the dependency is a loopback to some item that has occurred
    ///                             previously on the path. In this case, the second ("end") flag is
    ///                             also set.
    /// 
    /// Items are marked as follows:
    /// * The start item of each path is marked with the path marker and value 
    /// ** 5 if it matched by a "count match" (see ___PathMarker)
    /// ** 1 otherwise.
    /// * All other items are marked with the path marker, where the value is the reached count,
    ///   if a "count match" was provided. Note that this means that many items on paths are
    ///   <i>not marked</i>, as their "reached count" may be zero, which is equivalent to 
    ///   not being marked with the corresponding marker.
    /// 
    /// </summary>

    public static class PathSupport {
        public const int IS_START = 1 << 0;
        public const int IS_END = 1 << 1;
        public const int IS_MATCHED_BY_COUNT_MATCH = 1 << 2;
        public const int IS_LOOPBACK = 1 << 3;
        public const int INDEX_SHIFT = 4;

        public static void MarkPathElement<T>([NotNull] this T e, [NotNull] string indexedMarker, int i,
            bool isStart, bool isEnd, bool isMatchedByCountMatch, bool isLoopBack) where T : class, IWithMutableMarkerSet {
            e.SetMarker(indexedMarker, (i << INDEX_SHIFT)
                                       | (isStart ? IS_START : 0)
                                       | (isEnd ? IS_END : 0)
                                       | (isMatchedByCountMatch ? IS_MATCHED_BY_COUNT_MATCH : 0)
                                       | (isLoopBack ? IS_LOOPBACK : 0));
        }

        public static bool HasPathFlag(this int value, int flag) {
            return (value & flag) != 0;
        }
    }
}