
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.SpecialDependencyMarking {
    public class MarkSpecialDeps : ITransformer {
        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("mark");

        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker added to identified items", @default: null);
        //public static readonly Option RecursiveMarkOption = new Option("mr", "mark-recursively", "", "Repeat marking", @default: false);
        public static readonly Option MarkTransitiveDependenciesOption = new Option("mt", "mark-transitive", "", "Marks transitive dependencies", @default: false);
        public static readonly Option MarkSingleCyclesOption = new Option("mi", "mark-single-loops", "", "Mark single cycles", @default: false);

        private static readonly Option[] _transformOptions = DependencyMatchOptions.WithOptions(
            AddMarkerOption, MarkTransitiveDependenciesOption, MarkSingleCyclesOption
        );

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Mark dependencies with special properties - UNTESTED.

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
            bool markSingleCycleNodes = false;
            //bool recursive = false;
            bool markTransitiveDependencies = false;
            string markerToAdd = null;

            DependencyMatchOptions.Parse(globalContext, transformOptions, _ignoreCase, matches, excludes,
                MarkSingleCyclesOption.Action((args, j) => {
                    markSingleCycleNodes = true;
                    return j;
                    //}), RecursiveMarkOption.Action((args, j) => {
                    //    recursive = true;
                    //    return j;
                }),
                MarkTransitiveDependenciesOption.Action((args, j) => {
                    markTransitiveDependencies = true;
                    return j;
                }),
                AddMarkerOption.Action((args, j) => {
                    markerToAdd = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim();
                    return j;
                }));

            Dependency[] matchingDependencies = dependencies.Where(d => d.IsMarkerMatch(matches, excludes)).ToArray();

            if (markSingleCycleNodes) {
                foreach (var d in matchingDependencies) {
                    if (Equals(d.UsingItem, d.UsedItem)) {
                        d.IncrementMarker(markerToAdd);
                    }
                }
            }

            if (markTransitiveDependencies) {
                Dictionary<Item, Dependency[]> outgoing = Item.CollectOutgoingDependenciesMap(matchingDependencies);
                foreach (var root in outgoing.Keys) {
                    var itemsAtDistance1FromRoot = new Dictionary<Item, List<Dependency>>();
                    foreach (var d in outgoing[root]) {
                        List<Dependency> list;
                        if (!itemsAtDistance1FromRoot.TryGetValue(d.UsedItem, out list)) {
                            itemsAtDistance1FromRoot.Add(d.UsedItem, list = new List<Dependency>());
                        }
                        list.Add(d);
                    }

                    foreach (var itemAtDistance2FromRoot in
                             itemsAtDistance1FromRoot.Keys.ToArray().SelectMany(i1 => outgoing[i1].Select(d => d.UsedItem))) {
                        if (itemsAtDistance1FromRoot.Count == 0) {
                            break;
                        }
                        RemoveReachableItems(itemAtDistance2FromRoot, new HashSet<Item> { root }, outgoing,
                            itemsAtDistance1FromRoot, markerToAdd);
                    }
                }
            }

            transformedDependencies.AddRange(dependencies);

            return Program.OK_RESULT;
        }

        private void RemoveReachableItems(Item itemAtDistanceNFromRoot, HashSet<Item> visited, Dictionary<Item, Dependency[]> outgoing,
            Dictionary<Item, List<Dependency>> itemsAtDistance1FromRoot, string markerToAdd) {
            if (!visited.Contains(itemAtDistanceNFromRoot)) {
                visited.Add(itemAtDistanceNFromRoot);
                if (itemsAtDistance1FromRoot.ContainsKey(itemAtDistanceNFromRoot)) {
                    foreach (var d in itemsAtDistance1FromRoot[itemAtDistanceNFromRoot]) {
                        d.IncrementMarker(markerToAdd);
                    }
                    // This item can be "reached transitively" - we are done with it.
                    itemsAtDistance1FromRoot.Remove(itemAtDistanceNFromRoot);
                }
                foreach (var itemAtDistanceNPlus1 in outgoing[itemAtDistanceNFromRoot].Select(d => d.UsedItem)) {
                    if (itemsAtDistance1FromRoot.Count == 0) {
                        break;
                    }
                    RemoveReachableItems(itemAtDistanceNPlus1, visited, outgoing, itemsAtDistance1FromRoot, markerToAdd);
                }
            }
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            Item a = transformingGraph.NewItem(ItemType.SIMPLE, "Ax");
            Item b = transformingGraph.NewItem(ItemType.SIMPLE, "Bx");
            Item c = transformingGraph.NewItem(ItemType.SIMPLE, "Cloop");
            Item d = transformingGraph.NewItem(ItemType.SIMPLE, "Dloop");
            Item e = transformingGraph.NewItem(ItemType.SIMPLE, "Eselfloop");
            Item f = transformingGraph.NewItem(ItemType.SIMPLE, "Fy");
            Item g = transformingGraph.NewItem(ItemType.SIMPLE, "Gy");
            Item h = transformingGraph.NewItem(ItemType.SIMPLE, "Hy");
            Item i = transformingGraph.NewItem(ItemType.SIMPLE, "Iy");
            Item j = transformingGraph.NewItem(ItemType.SIMPLE, "Jy");
            return new[] {
                // Pure sources
                transformingGraph.CreateDependency(a, b, source: null, markers: "", ct: 10, questionableCt: 5, badCt: 3),
                transformingGraph.CreateDependency(b, c, source: null, markers: "", ct: 1, questionableCt: 0, badCt: 0),

                // Long cycle
                transformingGraph.CreateDependency(c, d, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2),
                transformingGraph.CreateDependency(d, c, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2),

                transformingGraph.CreateDependency(d, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                // Self cycle
                transformingGraph.CreateDependency(e, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                // Pure sinks
                transformingGraph.CreateDependency(e, f, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                transformingGraph.CreateDependency(f, g, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                transformingGraph.CreateDependency(g, h, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                transformingGraph.CreateDependency(h, i, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                transformingGraph.CreateDependency(h, j, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2)
            };
        }
    }
}
