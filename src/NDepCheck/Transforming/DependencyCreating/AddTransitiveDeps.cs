using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Markers;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.DependencyCreating {
    public class AddTransitiveDeps : ITransformer {
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

        private bool _ignoreCase;

        private int _transformRunCt = 0;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Add transitive edges.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, [NotNull] List<Dependency> transformedDependencies) {

            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();

            var fromItemMatches = new List<ItemMatch>();
            var toItemMatches = new List<ItemMatch>();
            var markersToAdd = new List<string>();
            //bool removeOriginal = false;
            bool idempotent = false;

            DependencyMatchOptions.Parse(globalContext, transformOptions, _ignoreCase, matches, excludes,
                FromItemsOption.Action((args, j) => {
                    fromItemMatches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing 'from' match"), _ignoreCase));
                    return j;
                }),
                ToItemsOption.Action((args, j) => {
                    toItemMatches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing 'to' match"), _ignoreCase));
                    return j;
                }),
                IdempotentOption.Action((args, j) => {
                    idempotent = true;
                    return j;
                }),
                //RemoveOriginalOption.Action((args, j) => {
                //    removeOriginal = true;
                //    return j;
                //}),
                AddMarkerOption.Action((args, j) => {
                    markersToAdd.Add(Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim());
                    return j;
                }));

            _transformRunCt++;
            MutableMarkerSet.AddComputedMarkerIfNoMarkers(markersToAdd, fromItemMatches, toItemMatches, "" + _transformRunCt);

            DependencyPattern idempotentPattern = new DependencyPattern("'" + string.Join("+", markersToAdd), _ignoreCase);
            Dictionary<FromTo, Dependency> checkPresence = idempotent ? FromTo.AggregateAllDependencies(dependencies) : new Dictionary<FromTo, Dependency>();
            Dictionary<Item, Dependency[]> outgoing = Item.CollectOutgoingDependenciesMap(dependencies);
            IEnumerable<Item> matchingFroms = outgoing.Keys.Where(i => IsMatch(fromItemMatches, i));

            Dictionary<string, int> markersToAddAsDictionary = markersToAdd.Distinct().ToDictionary(s => s, s => 1);

            var result = new List<Dependency>();
            foreach (var from in matchingFroms) {
                RecursivelyFlood(from, from, new HashSet<Item> { from }, checkPresence, idempotentPattern, outgoing,
                                 toItemMatches, matches, excludes, markersToAddAsDictionary, result, null, globalContext.CheckAbort);
            }

            transformedDependencies.AddRange(dependencies);
            transformedDependencies.AddRange(result);
            Log.WriteInfo($"... added {result.Count} new dependencies");

            return Program.OK_RESULT;
        }

        private static bool IsMatch(IEnumerable<ItemMatch> itemMatches, Item i) {
            return !itemMatches.Any() || itemMatches.Any(m => m.Matches(i).Success);
        }

        private void RecursivelyFlood(Item root, Item @from, HashSet<Item> visited, Dictionary<FromTo, Dependency> checkPresence, DependencyPattern idempotentPattern, 
            Dictionary<Item, Dependency[]> outgoing, IEnumerable<ItemMatch> toItemMatches, List<DependencyMatch> matches, List<DependencyMatch> excludes,
            Dictionary<string, int> markersToAddOrNull, List<Dependency> result, Dependency collectedEdge, [NotNull] Action checkAbort) {
            if (outgoing.ContainsKey(from)) {
                checkAbort();
                foreach (var d in outgoing[from].Where(d => d.IsMatch(matches, excludes))) {
                    Item target = d.UsedItem;
                    if (visited.Add(target)) {
                        Dependency rootToTarget = collectedEdge == null
                            ? d
                            : new Dependency(root, target, d.Source,
                                new MutableMarkerSet(_ignoreCase, markersToAddOrNull ?? MutableMarkerSet.ConcatOrUnionWithMarkers(collectedEdge.AbstractMarkerSet, d.AbstractMarkerSet, _ignoreCase)),
                                collectedEdge.Ct + d.Ct, collectedEdge.QuestionableCt + d.QuestionableCt,
                                collectedEdge.BadCt + d.BadCt, d.ExampleInfo);

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
                                         toItemMatches, matches, excludes, markersToAddOrNull, result, rootToTarget, checkAbort);
                    }
                }
            }
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
            var s1 = Item.New(ItemType.SIMPLE, "S1");
            var s2 = Item.New(ItemType.SIMPLE, "S2");
            var a = Item.New(ItemType.SIMPLE, "A");
            var t1 = Item.New(ItemType.SIMPLE, "T1");
            var t2 = Item.New(ItemType.SIMPLE, "T2");
            var b = Item.New(ItemType.SIMPLE, "B");
            var c = Item.New(ItemType.SIMPLE, "C");
            var d = Item.New(ItemType.SIMPLE, "D");
            var t3 = Item.New(ItemType.SIMPLE, "T3");
            var t4 = Item.New(ItemType.SIMPLE, "T4");
            return new[] {
                new Dependency(s1, a, source: null, markers: "1", ct:10, questionableCt:5, badCt:3),
                new Dependency(s1, d, source: null, markers: "D", ct:10, questionableCt:5, badCt:3),
                new Dependency(s1, t4, source: null, markers: "D", ct:10, questionableCt:5, badCt:3),
                new Dependency(s2, a, source: null, markers: "2", ct:10, questionableCt:5, badCt:3),

                new Dependency(a, t1, source: null, markers: "1", ct:1, questionableCt:0, badCt: 0),
                new Dependency(a, t2, source: null, markers: "2", ct:2, questionableCt:0, badCt: 0),
                new Dependency(a, t2, source: null, markers: "2", ct:3, questionableCt:0, badCt: 0),

                new Dependency(a, b, source: null, markers: "", ct:1, questionableCt:0, badCt: 0),
                new Dependency(b, c, source: null, markers: "", ct:1, questionableCt:0, badCt: 0),
                new Dependency(c, b, source: null, markers: "", ct:1, questionableCt:0, badCt: 0),

                new Dependency(c, t3, source: null, markers: "3", ct:5, questionableCt:0, badCt:2),

                new Dependency(d, t4, source: null, markers: "4", ct:5, questionableCt:0, badCt:2),
            };
        }
    }
}
