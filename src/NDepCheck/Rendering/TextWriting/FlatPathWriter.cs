using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;
using NDepCheck.Transforming.PathFinding;

namespace NDepCheck.Rendering.TextWriting {
    public class FlatPathWriter : IRenderer {
        public static readonly Option PathMarkerOption = new Option("pm", "path-marker", "pattern", "path marker prefix to be used to extract paths", @default: "no item matches", multiple: true);
        public static readonly Option ShowItemMarkersOption = new Option("sm", "show-markers", "", "Shows markers on items", @default: false);

        private static readonly Option[] _allOptions = { PathMarkerOption, ShowItemMarkersOption };

        public string GetHelp(bool detailedHelp, string filter) {
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

        public WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, string argsAsString, WriteTarget baseTarget) {
            return GlobalContext.CreateFullFileName(baseTarget, ".txt");
        }

        public void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            int? dependenciesCount, string argsAsString, [NotNull] WriteTarget target, bool ignoreCase) {
            var markerMatchers = new List<IMatcher>();
            bool showItemMarkers = false;

            Option.Parse(globalContext, argsAsString,
                PathMarkerOption.Action((args, j) => {
                    markerMatchers.Add(MarkerMatch.CreateMatcher(Option.ExtractRequiredOptionValue(args, ref j, "Missing item match"), globalContext.IgnoreCase));
                    return j;
                }),
                ShowItemMarkersOption.Action((args, j) => {
                    showItemMarkers = true;
                    return j;
                }));

            int pathCount;
            using (ITargetWriter tw = GetMasterFileName(globalContext, argsAsString, target).CreateWriter()) {
                tw.WriteLine($"// Written {DateTime.Now} by {typeof(FlatPathWriter).Name} in NDepCheck {Program.VERSION}");
                tw.WriteLine();
                pathCount = WritePaths(dependencies, ignoreCase, markerMatchers, tw, showItemMarkers);
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
                kvp.Value.Sort((d1, d2) => d1.MarkerSet.GetValue(kvp.Key, ignoreCase) - d2.MarkerSet.GetValue(kvp.Key, ignoreCase));
            }

            foreach (var kvp in paths) {
                string marker = kvp.Key;
                List<Dependency> path = kvp.Value;
                tw.WriteLine($"-- {marker}");

                Item firstItem = path[0].UsingItem;
                tw.WriteLine(firstItem.ItemAsString(showItemMarkers, isEnd: false, endOfCycle: false,
                    matchedByCountMatch: (firstItem.MarkerSet.GetValue(marker, ignoreCase) & PathSupport.IS_MATCHED_BY_COUNT_MATCH) != 0));
                foreach (var d in path) {
                    int dependencyValue = d.MarkerSet.GetValue(marker, ignoreCase);

                    tw.WriteLine(d.UsedItem.ItemAsString(showItemMarkers,
                        isEnd : dependencyValue.HasPathFlag(PathSupport.IS_END),
                        endOfCycle : dependencyValue.HasPathFlag(PathSupport.IS_LOOPBACK),
                        matchedByCountMatch : dependencyValue.HasPathFlag(PathSupport.IS_MATCHED_BY_COUNT_MATCH)
                    ));
                }
                tw.WriteLine();
            }
            return paths.Count;
        }

        public void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream stream, string option) {
            using (var sw = new TargetStreamWriter(stream)) {
                string[] options = (option ?? "").Split(' ');
                WritePaths(dependencies, globalContext.IgnoreCase, new [] { MarkerMatch.CreateMatcher(options[0], globalContext.IgnoreCase) }, sw, showItemMarkers: options.Contains("-sm"));
            }
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
            return RendererSupport.CreateSomeTestItems();
        }
    }
}
