using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Markers;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.DependencyCreating {
    public class AddTransitiveDeps : TransformerWithOptions<Ignore, AddTransitiveDeps.TransformOptions> {
        public class TransformOptions {
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Matches = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Excludes = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<ItemMatch> FromItemMatches = new List<ItemMatch>();
            [NotNull, ItemNotNull]
            public List<ItemMatch> ToItemMatches = new List<ItemMatch>();
            [NotNull, ItemNotNull]
            public List<string> MarkersToAdd = new List<string>();
            public bool Idempotent;
        }

        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("traverse");

        //public static readonly Option RemoveOriginalOption = new Option("ro", "remove-original", "", "If present, original dependency of a newly created reverse dependency is removed", @default: false);
        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker added to newly created transitive dependencies", @default: "none");
        public static readonly Option IdempotentOption = new Option("ip", "idempotent", "", "Do not add if dependency with provided marker already exists", @default: false);
        public static readonly Option FromItemsOption = new Option("fi", "from-items-match", "&", "Match for items where added transitive dependencies are to start", @default: "all items are matched", multiple: true);
        public static readonly Option ToItemsOption = new Option("ti", "to-items-match", "&", "Match for items where added transitive dependencies are to end", @default: "all items are matched", multiple: true);
        //public static readonly Option MaxSpanLengthOption = new Option("ml", "max-length", "#", "maximum number of edges collapsed", @default: "arbitrary length");

        private static readonly Option[] _transformOptions = DependencyMatchOptions.WithOptions(
            AddMarkerOption, IdempotentOption, FromItemsOption, ToItemsOption
        );

        private int _transformRunCt = 0;

        public override string GetHelp(bool detailedHelp, string filter) {
            return $@"Add transitive edges.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        protected override Ignore CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            var options = new Ignore();
            return options;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext, 
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var transformOptions = new TransformOptions();

            //bool removeOriginal = false;

            DependencyMatchOptions.Parse(globalContext, transformOptionsString, globalContext.IgnoreCase, 
                transformOptions.Matches, transformOptions.Excludes,
                FromItemsOption.Action((args, j) => {
                    transformOptions.FromItemMatches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing 'from' match"), globalContext.IgnoreCase, anyWhereMatcherOk: true));
                    return j;
                }),
                ToItemsOption.Action((args, j) => {
                    transformOptions.ToItemMatches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing 'to' match"), globalContext.IgnoreCase, anyWhereMatcherOk: true));
                    return j;
                }),
                IdempotentOption.Action((args, j) => {
                    transformOptions.Idempotent = true;
                    return j;
                }),
                //RemoveOriginalOption.Action((args, j) => {
                //    removeOriginal = true;
                //    return j;
                //}),
                AddMarkerOption.Action((args, j) => {
                    transformOptions.MarkersToAdd.Add(Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim());
                    return j;
                }));

            return transformOptions;
        }

        public override int Transform([NotNull] GlobalContext globalContext, Ignore Ignore, 
            [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            [NotNull] List<Dependency> transformedDependencies) {

            _transformRunCt++;
            MutableMarkerSet.AddComputedMarkerIfNoMarkers(transformOptions.MarkersToAdd, 
                transformOptions.FromItemMatches, transformOptions.ToItemMatches, "" + _transformRunCt);

            WorkingGraph currentWorkingGraph = globalContext.CurrentGraph;

            DependencyPattern idempotentPattern = new DependencyPattern("'" + string.Join("+", transformOptions.MarkersToAdd), globalContext.IgnoreCase);
            Dictionary<FromTo, Dependency> checkPresence = transformOptions.Idempotent
                ? FromTo.AggregateAllDependencies(currentWorkingGraph, dependencies) 
                : new Dictionary<FromTo, Dependency>();
            Dictionary<Item, Dependency[]> outgoing = Item.CollectOutgoingDependenciesMap(dependencies);
            IEnumerable<Item> matchingFroms = outgoing.Keys.Where(i => IsMatch(transformOptions.FromItemMatches, i));

            Dictionary<string, int> markersToAddAsDictionary = transformOptions.MarkersToAdd.Distinct().ToDictionary(s => s, s => 1);

            var result = new List<Dependency>();
            foreach (var from in matchingFroms) {
                RecursivelyFlood(from, from, new HashSet<Item> { from }, checkPresence, idempotentPattern, outgoing,
                                 transformOptions.ToItemMatches, transformOptions.Matches, transformOptions.Excludes, 
                                 markersToAddAsDictionary, result, null, globalContext.CheckAbort,
                                 currentWorkingGraph, globalContext.IgnoreCase);
            }

            transformedDependencies.AddRange(dependencies);
            transformedDependencies.AddRange(result);
            Log.WriteInfo($"... added {result.Count} new dependencies");

            return Program.OK_RESULT;
        }

        private static bool IsMatch(IEnumerable<ItemMatch> itemMatches, Item i) {
            return !itemMatches.Any() || itemMatches.Any(m => m.Matches(i).Success);
        }

        private void RecursivelyFlood(Item root, Item from, HashSet<Item> visited, Dictionary<FromTo, Dependency> checkPresence,
            DependencyPattern idempotentPattern, Dictionary<Item, Dependency[]> outgoing, IEnumerable<ItemMatch> toItemMatches,
            List<DependencyMatch> matches, List<DependencyMatch> excludes, Dictionary<string, int> markersToAddOrNull,
            List<Dependency> result, Dependency collectedEdge, [NotNull] Action checkAbort, WorkingGraph workingGraph, bool ignoreCase) {
            if (outgoing.ContainsKey(from)) {
                checkAbort();
                foreach (var d in outgoing[from].Where(d => d.IsMarkerMatch(matches, excludes))) {
                    Item target = d.UsedItem;
                    if (visited.Add(target)) {
                        Dependency rootToTarget = collectedEdge == null
                            ? d
                            : workingGraph.CreateDependency(root, target, d.Source,
                                new MutableMarkerSet(ignoreCase, 
                                    markersToAddOrNull
                                    ?? MutableMarkerSet.ConcatOrUnionWithMarkers(collectedEdge.AbstractMarkerSet, 
                                                                                 d.AbstractMarkerSet,ignoreCase)),
                                collectedEdge.Ct + d.Ct, collectedEdge.QuestionableCt + d.QuestionableCt,
                                collectedEdge.BadCt + d.BadCt, collectedEdge.NotOkReason ?? d.NotOkReason, d.ExampleInfo);

                        if (IsMatch(toItemMatches, target)) {
                            Dependency alreadyThere;
                            var rootTargetKey = new FromTo(root, target);
                            if (checkPresence.TryGetValue(rootTargetKey, out alreadyThere) && idempotentPattern.IsMatch(alreadyThere)) {
                                // we do not add a dependency
                            } else {
                                checkPresence[rootTargetKey] = rootToTarget;
                                result.Add(rootToTarget);
                            }
                        }

                        // Continue search
                        RecursivelyFlood(root, target, visited, checkPresence, idempotentPattern, outgoing,
                                         toItemMatches, matches, excludes, markersToAddOrNull, result, rootToTarget,
                                         checkAbort, workingGraph, ignoreCase);
                    }
                }
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            var s1 = transformingGraph.CreateItem(ItemType.SIMPLE, "S1");
            var s2 = transformingGraph.CreateItem(ItemType.SIMPLE, "S2");
            var a = transformingGraph.CreateItem(ItemType.SIMPLE, "A");
            var t1 = transformingGraph.CreateItem(ItemType.SIMPLE, "T1");
            var t2 = transformingGraph.CreateItem(ItemType.SIMPLE, "T2");
            var b = transformingGraph.CreateItem(ItemType.SIMPLE, "B");
            var c = transformingGraph.CreateItem(ItemType.SIMPLE, "C");
            var d = transformingGraph.CreateItem(ItemType.SIMPLE, "D");
            var t3 = transformingGraph.CreateItem(ItemType.SIMPLE, "T3");
            var t4 = transformingGraph.CreateItem(ItemType.SIMPLE, "T4");
            return new[] {
                transformingGraph.CreateDependency(s1, a, source: null, markers: "1", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),
                transformingGraph.CreateDependency(s1, d, source: null, markers: "D", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),
                transformingGraph.CreateDependency(s1, t4, source: null, markers: "D", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),
                transformingGraph.CreateDependency(s2, a, source: null, markers: "2", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),

                transformingGraph.CreateDependency(a, t1, source: null, markers: "1", ct:1, questionableCt:0, badCt: 0),
                transformingGraph.CreateDependency(a, t2, source: null, markers: "2", ct:2, questionableCt:0, badCt: 0),
                transformingGraph.CreateDependency(a, t2, source: null, markers: "2", ct:3, questionableCt:0, badCt: 0),

                transformingGraph.CreateDependency(a, b, source: null, markers: "", ct:1, questionableCt:0, badCt: 0),
                transformingGraph.CreateDependency(b, c, source: null, markers: "", ct:1, questionableCt:0, badCt: 0),
                transformingGraph.CreateDependency(c, b, source: null, markers: "", ct:1, questionableCt:0, badCt: 0),

                transformingGraph.CreateDependency(c, t3, source: null, markers: "3", ct:5, questionableCt:0, badCt:2, notOkReason: "test"),

                transformingGraph.CreateDependency(d, t4, source: null, markers: "4", ct:5, questionableCt:0, badCt:2, notOkReason: "test"),
            };
        }
    }
}
