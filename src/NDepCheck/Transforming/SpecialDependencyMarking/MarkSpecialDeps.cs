
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

            Dependency[] matchingDependencies = dependencies.Where(d => d.IsMatch(matches, excludes)).ToArray();

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

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
            Item a = Item.New(ItemType.SIMPLE, "Ax");
            Item b = Item.New(ItemType.SIMPLE, "Bx");
            Item c = Item.New(ItemType.SIMPLE, "Cloop");
            Item d = Item.New(ItemType.SIMPLE, "Dloop");
            Item e = Item.New(ItemType.SIMPLE, "Eselfloop");
            Item f = Item.New(ItemType.SIMPLE, "Fy");
            Item g = Item.New(ItemType.SIMPLE, "Gy");
            Item h = Item.New(ItemType.SIMPLE, "Hy");
            Item i = Item.New(ItemType.SIMPLE, "Iy");
            Item j = Item.New(ItemType.SIMPLE, "Jy");
            return new[] {
                // Pure sources
                new Dependency(a, b, source: null, markers: "", ct: 10, questionableCt: 5, badCt: 3),
                new Dependency(b, c, source: null, markers: "", ct: 1, questionableCt: 0, badCt: 0),

                // Long cycle
                new Dependency(c, d, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2),
                new Dependency(d, c, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2),

                new Dependency(d, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                // Self cycle
                new Dependency(e, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                // Pure sinks
                new Dependency(e, f, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(f, g, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(g, h, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(h, i, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(h, j, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2)
            };
        }
    }
}
