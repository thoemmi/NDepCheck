using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.DependencyCreating {
    public class AddTransitiveDeps : ITransformer {
        public static readonly Option DependencyMatchOption = new Option("dm", "dependency-match", "&", "Match to select dependencies to reverse", @default: "reverse all edges", multiple: true);
        //public static readonly Option RemoveOriginalOption = new Option("ro", "remove-original", "", "If present, original dependency of a newly created reverse dependency is removed", @default: false);
        public static readonly Option MarkerToAddOption = new Option("ma", "marker-to-add", "&", "Marker added to newly created reverse dependencies", @default: "none");
        public static readonly Option IdempotentOption = new Option("ip", "idempotent", "", "Do not add if dependency with provided marker already exists", @default: false);
        public static readonly Option FromItemsOption = new Option("fi", "from-items-match", "&", "If present, original dependency of a newly created reverse dependency is removed", @default: "all items are matched", multiple: true);
        public static readonly Option ToItemsOption = new Option("ti", "to-items-match", "&", "If present, original dependency of a newly created reverse dependency is removed", @default: "all items are matched", multiple: true);
        //public static readonly Option MaxSpanLengthOption = new Option("ml", "max-length", "#", "maximum number of edges collapsed", @default: "arbitrary length");

        private static readonly Option[] _transformOptions =
            { DependencyMatchOption, /*RemoveOriginalOption,*/ MarkerToAddOption, IdempotentOption, FromItemsOption, ToItemsOption };

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Add transitive edges.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform(GlobalContext globalContext, [CanBeNull] string dependenciesFilename, IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            var dependencyMatches = new List<DependencyMatch>();
            var fromItemMatches = new List<ItemMatch>();
            var toItemMatches = new List<ItemMatch>();
            var markersToAdd = new List<string>();
            //bool removeOriginal = false;
            bool idempotent = false;

            Option.Parse(globalContext, transformOptions,
                DependencyMatchOption.Action((args, j) => {
                    dependencyMatches.Add(new DependencyMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency match"), _ignoreCase));
                    return j;
                }),
                FromItemsOption.Action((args, j) => {
                    fromItemMatches.Add(ItemMatch.CreateItemMatchWithGenericType( Option.ExtractRequiredOptionValue(args, ref j, "Missing 'from' match"), _ignoreCase));
                    return j;
                }),
                ToItemsOption.Action((args, j) => {
                    toItemMatches.Add(ItemMatch.CreateItemMatchWithGenericType( Option.ExtractRequiredOptionValue(args, ref j, "Missing 'to' match"), _ignoreCase));
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
                MarkerToAddOption.Action((args, j) => {
                    markersToAdd.Add(Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim());
                    return j;
                }));

            DependencyMatch idempotentMatch = new DependencyMatch("'" + string.Join("+", markersToAdd), _ignoreCase);
            Dictionary<FromTo, Dependency> checkPresence = idempotent ? FromTo.AggregateAllEdges(dependencies) : new Dictionary<FromTo, Dependency>();
            Dictionary<Item, IEnumerable<Dependency>> outgoing = Item.CollectOutgoingDependenciesMap(dependencies);
            var matchingFroms = outgoing.Keys.Where(i => Matches(fromItemMatches, i));

            var result = new List<Dependency>();
            foreach (var from in matchingFroms) {
                RecursivelyFlood(from, from, new HashSet<Item> { from }, checkPresence, idempotentMatch, outgoing,
                                 toItemMatches, dependencyMatches, markersToAdd, result, null);
            }

            transformedDependencies.AddRange(dependencies);
            transformedDependencies.AddRange(result);

            return Program.OK_RESULT;
        }

        private static bool Matches(IEnumerable<ItemMatch> itemMatches, Item i) {
            return !itemMatches.Any() || itemMatches.Any(m => m.Matches(i) != null);
        }

        private static bool Matches(IEnumerable<DependencyMatch> dependencyMatches, Dependency d) {
            return !dependencyMatches.Any() || dependencyMatches.Any(m => m.Matches(d));
        }

        private void RecursivelyFlood(Item root, Item from, HashSet<Item> visited, Dictionary<FromTo, Dependency> checkPresence,
            DependencyMatch idempotentMatch, Dictionary<Item, IEnumerable<Dependency>> outgoing, IEnumerable<ItemMatch> toItemMatches,
            List<DependencyMatch> dependencyMatches, IEnumerable<string> markersToAddOrNull, List<Dependency> result, Dependency collectedEdge) {
            if (outgoing.ContainsKey(from)) {
                foreach (var d in outgoing[from].Where(d => Matches(dependencyMatches, d))) {
                    Item target = d.UsedItem;
                    if (visited.Add(target)) {
                        Dependency rootToTarget = collectedEdge == null
                            ? d
                            : new Dependency(root, target, d.Source,
                                markersToAddOrNull ??
                                ObjectWithMarkers.ConcatOrUnionWithMarkers(collectedEdge.Markers, d.Markers, _ignoreCase),
                                collectedEdge.Ct + d.Ct, collectedEdge.QuestionableCt + d.QuestionableCt,
                                collectedEdge.BadCt + d.BadCt, d.ExampleInfo, d.InputContext);

                        if (Matches(toItemMatches, target)) {
                            Dependency alreadyThere;
                            var rootTargetKey = new FromTo(root, target);
                            if (checkPresence.TryGetValue(rootTargetKey, out alreadyThere) && idempotentMatch.Matches(alreadyThere)) {
                                // we do not add a dependency
                            } else {
                                checkPresence[rootTargetKey] = rootToTarget;
                                result.Add(rootToTarget);
                            }
                        }

                        // Continue search
                        RecursivelyFlood(root, target, visited, checkPresence, idempotentMatch, outgoing,
                                         toItemMatches, dependencyMatches, markersToAddOrNull, result, rootToTarget);
                    }
                }
            }
        }

        public void AfterAllTransforms(GlobalContext globalContext) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
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
