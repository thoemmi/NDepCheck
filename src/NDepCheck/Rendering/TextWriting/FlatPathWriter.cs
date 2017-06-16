using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;
using NDepCheck.Transforming.PathFinding;

namespace NDepCheck.Rendering.TextWriting {
    public class FlatPathWriter : RendererWithOptions<FlatPathWriter.Options> {
        public class Options {
            [NotNull, ItemNotNull]
            public List<IMatcher> MarkerMatchers = new List<IMatcher>();
            public bool ShowItemMarkers;
        }

        public static readonly Option PathMarkerOption = new Option("pm", "path-marker", "pattern", "path marker prefix to be used to extract paths", @default: "no item matches", multiple: true);
        public static readonly Option ShowItemMarkersOption = new Option("sm", "show-markers", "", "Shows markers on items", @default: false);

        private static readonly Option[] _allOptions = { PathMarkerOption, ShowItemMarkersOption };

        public override string GetHelp(bool detailedHelp, string filter) {
            var result =
    $@"  Writes complete paths to a .txt files. The paths are defined by specfic
  markers together with their values as follows:

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
            if (detailedHelp) {
                result += @"
    All items and dependencies that have an identical marker are assumed to be a 
    path in ascending order of the marker value.

    If the integer marker value has certain bits set, the items or dependencies
    are special:
    - If the lowest bit is set (i.e., the value is odd), this is a 'start element'.
    - If the second bit is set, this is an 'end element'.
    - ___MORE INFO MISSING___

    FindCycleDeps and PathMarker are two transformers that create such markers.";
            }
            return result;
        }

        public override WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, Options options, WriteTarget baseTarget) {
            return GlobalContext.CreateFullFileName(baseTarget, ".txt");
        }


        protected override Options CreateRenderOptions(GlobalContext globalContext, string options) {
            var result = new Options();

            Option.Parse(globalContext, options,
                PathMarkerOption.Action((args, j) => {
                    result.MarkerMatchers.Add(MarkerMatch.CreateMatcher(Option.ExtractRequiredOptionValue(args, ref j, "Missing item match"), globalContext.IgnoreCase));
                    return j;
                }),
                ShowItemMarkersOption.Action((args, j) => {
                    result.ShowItemMarkers = true;
                    return j;
                }));
            return result;
        }

        public override void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            Options options, [NotNull] WriteTarget target, bool ignoreCase) {
            int pathCount;
            using (ITargetWriter tw = GetMasterFileName(globalContext, options, target).CreateWriter()) {
                tw.WriteLine($"// Written {DateTime.Now} by {typeof(FlatPathWriter).Name} in NDepCheck {Program.VERSION}");
                tw.WriteLine();
                pathCount = WritePaths(dependencies, ignoreCase, options.MarkerMatchers, tw, options.ShowItemMarkers);
            }
            Log.WriteInfo($"... written {pathCount} paths");
        }

        private static int WritePaths([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, bool ignoreCase, IEnumerable<IMatcher> markerMatchers,
                                        ITargetWriter tw, bool showItemMarkers) {
            var paths = new SortedDictionary<string, List<Dependency>>();

            foreach (var d in dependencies) {
                foreach (var marker in d.MarkerSet.MatchingMarkers(markerMatchers)) {
                    List<Dependency> path;
                    if (!paths.TryGetValue(marker, out path)) {
                        paths.Add(marker, path = new List<Dependency>());
                    }
                    path.Add(d);
                }
            }

            foreach (var kvp in paths) {
                string marker = kvp.Key;
                tw.WriteLine($"-- {marker}");

                // If a path was backprojected from a projected (i.e., condensed) path, then
                // multiple dependencies might have the same marker value. For printing the path,
                // the items on the left and right of such a group of dependencies are considered to
                // be one item and printed below each other, with an indent from the second onwards.
                var groupedPath = kvp.Value
                    .Select(d => new { d.UsingItem, MarkerValue = d.MarkerSet.GetValue(kvp.Key, ignoreCase), d.UsedItem })
                    .GroupBy(d => d.MarkerValue)
                    .OrderBy(g1 => g1.Key)
                    .ToArray();

                string itemSeparator = Environment.NewLine + "  & ";
                tw.WriteLine(string.Join(itemSeparator, new HashSet<string>(groupedPath[0]
                    .Select(d => CreateString(showItemMarkers, d.UsingItem, 0, d.UsingItem.MarkerSet.GetValue(marker, ignoreCase)))
                    .OrderBy(s => s))));
                var usedItems = new HashSet<Item>(groupedPath[0].Select(d => d.UsedItem));
                var previousKey = groupedPath[0].Key;
                foreach (var g in groupedPath.Skip(1)) {
                    // Also, after backprojection, a path might be "torn", e.g. a -> b1; b2 -> c.
                    // We want to see both b1 and b2 in the written result, hence, we add the b2 after a ">".
                    IEnumerable<string> orderedUsedItems = GetOrderedUniqueItems(showItemMarkers, usedItems, previousKey);
                    IEnumerable<string> additionalUsingItems =
                        GetOrderedUniqueItems(showItemMarkers, g.Select(d => d.UsingItem).Except(usedItems), previousKey);
                    tw.WriteLine(string.Join(itemSeparator, orderedUsedItems) +
                        (additionalUsingItems.Any()
                        ? Environment.NewLine + "  > " + string.Join(itemSeparator, additionalUsingItems)
                        : ""));
                    usedItems = new HashSet<Item>(g.Select(d => d.UsedItem));
                    previousKey = g.Key;
                }
                {
                    IEnumerable<string> orderedUsedItems = GetOrderedUniqueItems(showItemMarkers, usedItems, previousKey);
                    tw.WriteLine(string.Join(itemSeparator, orderedUsedItems));
                }
                tw.WriteLine();
            }
            return paths.Count;
        }

        private static IEnumerable<string> GetOrderedUniqueItems(bool showItemMarkers, IEnumerable<Item> items, int previousKey) {
            IEnumerable<string> orderedUsedItems =
                new HashSet<string>(items.Select(d => CreateString(showItemMarkers, d, previousKey, previousKey))).OrderBy(
                    s => s);
            return orderedUsedItems;
        }

        private static string CreateString(bool showItemMarkers, Item item, int markerValue, int markerValueForMatchCount) {
            return item.ItemAsString(showItemMarkers,
                isEnd: (markerValue & PathSupport.IS_END) != 0,
                endOfCycle: (markerValue & PathSupport.IS_LOOPBACK) != 0,
                matchedByCountMatch: (markerValueForMatchCount & PathSupport.IS_MATCHED_BY_COUNT_MATCH) != 0);
        }

        public override void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream stream, string option) {
            using (var sw = new TargetStreamWriter(stream)) {
                string[] options = (option ?? "").Split(' ');
                WritePaths(dependencies, globalContext.IgnoreCase, new[] { MarkerMatch.CreateMatcher(options[0], globalContext.IgnoreCase) }, sw, showItemMarkers: options.Contains("-sm"));
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph renderingGraph) {
            return RendererSupport.CreateSomeTestItems(renderingGraph);
        }
    }
}
