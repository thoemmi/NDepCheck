using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Markers;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.Modifying {
    public class MarkDeps : TransformerWithOptions<Ignore, MarkDeps.TransformOptions> {
        public class TransformOptions {
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Matches = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Excludes = new List<DependencyMatch>();
            public bool ClearLeft;
            public bool ClearDependency;
            public bool ClearRight;
            [NotNull, ItemNotNull]
            public List<string> MarkersToAddOnLeft = new List<string>();
            [NotNull]
            public Dictionary<string, int> MarkersToAddOnDep = new Dictionary<string, int>();
            [NotNull, ItemNotNull]
            public List<string> MarkersToAddOnRight = new List<string>();
            [NotNull, ItemNotNull]
            public List<string> MarkerPatternsToRemoveOnLeft = new List<string>();
            [NotNull, ItemNotNull]
            public List<string> MarkersToRemoveOnDep = new List<string>();
            [NotNull, ItemNotNull]
            public List<string> MarkerPatternsToRemoveOnRight = new List<string>();
            public bool ResetBad;
            public bool ResetQuestionable;
        }

        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("mark");

        public static readonly Option MarkLeftItemOption = new Option("ml", "mark-left", "marker", "Marker to add to item on left", @default: "", multiple: true);
        public static readonly Option MarkDependencyItemOption = new Option("md", "mark-dependency", "marker", "Marker to add to dependency", @default: "", multiple: true);
        public static readonly Option MarkRightItemOption = new Option("mr", "mark-right", "marker", "Marker to add to item on right", @default: "", multiple: true);

        public static readonly Option UnmarkLeftItemOption = new Option("ul", "unmark-left", "marker", "Marker to be removed from item on left", @default: "", multiple: true);
        public static readonly Option UnmarkDependencyItemOption = new Option("ud", "unmark-dependency", "marker", "Marker to be removed from dependency", @default: "", multiple: true);
        public static readonly Option UnmarkRightItemOption = new Option("ur", "unmark-right", "marker", "Marker to be removed from item on right", @default: "", multiple: true);

        public static readonly Option ClearLeftItemOption = new Option("cl", "clear-left", "", "Remove all markers from item on left", @default: false);
        public static readonly Option ClearDependencyItemOption = new Option("cd", "clear-dependency", "", "Remove all markers from dependency", @default: false);
        public static readonly Option ClearRightItemOption = new Option("cr", "clear-right", "", "Remove all markers from item on right", @default: false);

        public static readonly Option ResetQuestionableCountOption = new Option("rq", "reset-questionable", "", "Reset questionable count on dependencies", @default: false);
        public static readonly Option ResetBadCountOption = new Option("rb", "reset-bad", "", "Reset bad count on dependencies", @default: false);

        private static readonly Option[] _configOptions = DependencyMatchOptions.WithOptions(
            MarkLeftItemOption, MarkDependencyItemOption, MarkRightItemOption,
            UnmarkLeftItemOption, UnmarkDependencyItemOption, UnmarkRightItemOption,
            ClearLeftItemOption, ClearDependencyItemOption, ClearRightItemOption,
            ResetBadCountOption, ResetQuestionableCountOption
        );

        public override string GetHelp(bool detailedHelp, string filter) {
            string result = $@"Modify counts and markers on dependencies, delete or keep dependencies.

Configuration options: None

Transformer options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}";
            if (detailedHelp) {
                result += @"

A dependency match has the format

    usingItemMatch -- dependencyMatch -> usedItemMatch

Each part can be empty, therefore the simplest match (which matches every
dependency) is -- -> or, equivalently, --->.

A dependency is modified if all three matches match it; empty matches
match always.

The three parts have the following syntax:
    usingItemMatch, usedItemMatch
        item pattern (see -help itempattern)

    dependencyMatch
        dependency pattern (see -help dependency)

Examples:
   'From -- -> 'To        Match dependencies where using item
                          has marker From and used item has marker To

   -- 'OnCycle ->         Match all dependencies with marker OnCycle 

   // TODO: add .Net examples
"; // TODO: add .Net examples


            }
            return result;
        }

        protected override Ignore CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            return Ignore.Om;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var transformOptions = new TransformOptions();
            
            DependencyMatchOptions.Parse(globalContext, transformOptionsString, globalContext.IgnoreCase, transformOptions.Matches, transformOptions.Excludes,
                MarkLeftItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    transformOptions.MarkersToAddOnLeft.Add(marker.TrimStart('\''));
                    return j;
                }),
                MarkDependencyItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    transformOptions.MarkersToAddOnDep[marker.TrimStart('\'')] = 1;
                    return j;
                }),
                MarkRightItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    transformOptions.MarkersToAddOnRight.Add(marker.TrimStart('\''));
                    return j;
                }),
                UnmarkLeftItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    transformOptions.MarkerPatternsToRemoveOnLeft.Add(marker.TrimStart('\''));
                    return j;
                }),
                UnmarkDependencyItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    transformOptions.MarkersToRemoveOnDep.Add(marker.TrimStart('\''));
                    return j;
                }),
                UnmarkRightItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker");
                    transformOptions.MarkerPatternsToRemoveOnRight.Add(marker.TrimStart('\''));
                    return j;
                }),
                ClearLeftItemOption.Action((args, j) => {
                    transformOptions.ClearLeft = true;
                    return j;
                }),
                ClearDependencyItemOption.Action((args, j) => {
                    transformOptions.ClearDependency = true;
                    return j;
                }),
                ClearRightItemOption.Action((args, j) => {
                    transformOptions.ClearRight = true;
                    return j;
                }),
                ResetBadCountOption.Action((args, j) => {
                    transformOptions.ResetBad = true;
                    return j;
                }),
                ResetQuestionableCountOption.Action((args, j) => {
                    transformOptions.ResetQuestionable = true;
                    return j;
                })
            );
            return transformOptions;
        }

        public override int Transform([NotNull] GlobalContext globalContext, Ignore Ignore, 
            [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, 
            [NotNull] List<Dependency> transformedDependencies) {

            var leftMatches = new HashSet<Item>();
            var rightMatches = new HashSet<Item>();

            int n = 0;
            bool ignoreCase = globalContext.IgnoreCase;

            foreach (var d in dependencies.Where(d => d.IsMarkerMatch(transformOptions.Matches, transformOptions.Excludes))) {
                leftMatches.Add(d.UsingItem);
                rightMatches.Add(d.UsedItem);
                if (transformOptions.ClearDependency) {
                    d.ClearMarkers();
                } else {
                    d.UnionWithMarkers(transformOptions.MarkersToAddOnDep);
                    d.RemoveMarkers(transformOptions.MarkersToRemoveOnDep, ignoreCase);
                }
                if (transformOptions.ResetBad) {
                    d.ResetBad();
                }
                if (transformOptions.ResetQuestionable) {
                    d.ResetQuestionable();
                }
                n++;
            }

            Log.WriteInfo($"Marked {n} dependencies");

            ReadOnlyMarkerSet markersToAddOnLeftMarkerSet = new ReadOnlyMarkerSet(ignoreCase, transformOptions.MarkersToAddOnLeft);
            ReadOnlyMarkerSet markersToAddOnRightMarkerSet = new ReadOnlyMarkerSet(ignoreCase, transformOptions.MarkersToAddOnRight);

            // Items are modified afterwards - match loop above uses unchanged values
            foreach (var left in leftMatches) {
                MarkItem(transformOptions.ClearLeft, left, markersToAddOnLeftMarkerSet, transformOptions.MarkerPatternsToRemoveOnLeft, ignoreCase);
            }
            foreach (var right in rightMatches) {
                MarkItem(transformOptions.ClearRight, right, markersToAddOnRightMarkerSet, transformOptions.MarkerPatternsToRemoveOnRight, ignoreCase);
            }

            transformedDependencies.AddRange(dependencies);
            return Program.OK_RESULT;
        }

        private static void MarkItem(bool clear, Item item, IMarkerSet markersToAdd,
                                     List<string> markerPatternsToRemove, bool ignoreCase) {
            if (clear) {
                item.ClearMarkers();
            } else {
                item.MergeWithMarkers(markersToAdd);
                item.RemoveMarkers(markerPatternsToRemove, ignoreCase);
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            Item am = transformingGraph.CreateItem(ItemType.SIMPLE, new[] { "A" }, new[] { "M" });
            Item bm = transformingGraph.CreateItem(ItemType.SIMPLE, new[] { "B" }, new[] { "M" });
            Item cn = transformingGraph.CreateItem(ItemType.SIMPLE, new[] { "C" }, new[] { "N" });
            return new[] {
                transformingGraph.CreateDependency(am, am, source: null, markers: "", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),
                transformingGraph.CreateDependency(am, bm, source: null, markers: "use+define", ct:1, questionableCt:0,badCt: 0),
                transformingGraph.CreateDependency(am, cn, source: null, markers: "define", ct:5, questionableCt:0, badCt:2, notOkReason: "test"),
                transformingGraph.CreateDependency(bm, am, source: null, markers: "define", ct:5, questionableCt:0, badCt:2, notOkReason: "test"),
            };
        }
    }
}
