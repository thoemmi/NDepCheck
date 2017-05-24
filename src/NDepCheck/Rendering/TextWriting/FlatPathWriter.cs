﻿using System;
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

        public void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, string argsAsString, [NotNull] WriteTarget target, bool ignoreCase) {
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

                string itemSeparator = System.Environment.NewLine + "  & ";
                tw.WriteLine(string.Join(itemSeparator, new HashSet<string>(groupedPath[0]
                    .Select(d => CreateString(showItemMarkers, d.UsingItem, 0, d.UsingItem.MarkerSet.GetValue(marker, ignoreCase)))
                    .OrderBy(s => s))));
                var usedItems = new HashSet<Item>(groupedPath[0].Select(d => d.UsedItem));
                var previousKey = groupedPath[0].Key;
                foreach (var g in groupedPath.Skip(1)) {
                    //usedItems.IntersectWith(g.Select(d => d.UsingItem));
                    tw.WriteLine(string.Join(itemSeparator, new HashSet<string>(usedItems
                        .Select(d => CreateString(showItemMarkers, d, previousKey, previousKey))
                        .OrderBy(s => s))));
                    usedItems = new HashSet<Item>(g.Select(d => d.UsedItem));
                    previousKey = g.Key;
                }
                tw.WriteLine(string.Join(itemSeparator, new HashSet<string>(usedItems
                    .Select(i => CreateString(showItemMarkers, i, previousKey, previousKey))
                    .OrderBy(s => s))));
                tw.WriteLine();
            }
            return paths.Count;
        }

        private static string CreateString(bool showItemMarkers, Item item, int markerValue, int markerValueForMatchCount) {
            return item.ItemAsString(showItemMarkers, 
                isEnd: (markerValue & PathSupport.IS_END) != 0, 
                endOfCycle: (markerValue & PathSupport.IS_LOOPBACK) != 0,
                matchedByCountMatch: (markerValueForMatchCount & PathSupport.IS_MATCHED_BY_COUNT_MATCH) != 0);
        }

        public void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream stream, string option) {
            using (var sw = new TargetStreamWriter(stream)) {
                string[] options = (option ?? "").Split(' ');
                WritePaths(dependencies, globalContext.IgnoreCase, new [] { MarkerMatch.CreateMatcher(options[0], globalContext.IgnoreCase) }, sw, showItemMarkers: options.Contains("-sm"));
            }
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies(Environment renderingEnvironment) {
            return RendererSupport.CreateSomeTestItems(renderingEnvironment);
        }
    }
}
