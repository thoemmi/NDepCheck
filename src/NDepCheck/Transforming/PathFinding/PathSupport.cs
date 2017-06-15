using JetBrains.Annotations;
using NDepCheck.Markers;

namespace NDepCheck.Transforming.PathFinding {

    public static class PathSupport {
        public const int IS_START = 1 << 0;
        public const int IS_END = 1 << 1;
        public const int IS_MATCHED_BY_COUNT_MATCH = 1 << 2;
        public const int IS_LOOPBACK = 1 << 3;
        public const int INDEX_SHIFT = 4;

        public static void MarkPathElement<T>([NotNull] this T e, [NotNull] string marker, 
            int posInPath, bool isStart, bool isEnd, bool isMatchedByCountSymbol, bool isLoopBack) 
                where T : class, IWithMutableMarkerSet {
            e.SetMarker(marker, (posInPath << INDEX_SHIFT)
                                       | (isStart ? IS_START : 0)
                                       | (isEnd ? IS_END : 0)
                                       | (isMatchedByCountSymbol ? IS_MATCHED_BY_COUNT_MATCH : 0)
                                       | (isLoopBack ? IS_LOOPBACK : 0));
        }

        public static bool HasPathFlag(this int value, int flag) {
            return (value & flag) != 0;
        }

        public const string HELP = @"
NDepCheck does not have a built-in concept of ""paths"". Rather, such information 
(e.g. to recognize and output cycles; or paths going from some item type to
another) is encoded into markers on the dependencies. Each path is assumed to
have its own marker; typically, such markers consist of a prefix and a running
count, e.g. ""C00"", ""C01"", ..., ""C09"", ""C10"", ""C11"", ..., for cycles.
There is no concept of paths of length 0; each path must have at least one
dependency.

The dependencies of paths are marked as follows:
* All dependencies of a path with marker M contain a marker value for M greater 
  than 0.
* The order of the markers values designates the order of the dependencies on 
  the path.
* The final 4 bits of each path value are bits used for the following purposes:
** (1 << 0) = 1, i.e. the last bit: 1 if the dependency is the first one on the path 
                 (""start"")
** (1 << 1) = 2: 1 if the dependency is the last one on the path (""end"");
                 on a path of length 1, both this and the start bit are set.
** (1 << 2) = 4: 1 if the dependency or its used item is matched by a ""count match"".
** (1 << 3) = 8: 1 if the dependency is a loopback to some item that has occurred
                 previously on the path. In this case, the second (""end"") flag is
                 also set.

Items on paths are marked as follows:
* The start item of each path is marked with the path marker; the value for 
   the marker is either 5 or 1:
** 5 if it is matched by a ""count match"" (5 is 4 for ""count match"" + 1 for ""start"")
** 1 otherwise (for ""start"").
* If a count symbol (#) was provided, all other items on a path are marked with the 
  path marker, where the value is the number of ""counted objects"" that lie before
  this item on all found paths; this is called the ""reach count"".
  Note that this means that many items on paths are not(!) marked, as their 
  reach count may be zero, which is equivalent to not being marked with the 
  corresponding marker.

Remark for the curious: The numbering of edges (the second item under ""dependencies""
above) implies that each edge can occur on a path only once. Thus, this marking
scheme only supports ""simple paths"", but not paths that traverse an edge more than
once. Of course, plugins can device other, more complex marking schemes that can be
used to mark arbitrarily looping paths.";
    }
}