
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.SpecialDependencyMarking {
    public class MarkSpecialDeps : TransformerWithOptions<Ignore, MarkSpecialDeps.TransformOptions> {
        public class TransformOptions {
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Matches = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Excludes = new List<DependencyMatch>();
            public bool MarkSingleCycleNodes;
            public bool MarkTransitiveDependencies;
            public string MarkerToAdd;
        }

        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("mark");

        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker added to identified items", @default: null);
        //public static readonly Option RecursiveMarkOption = new Option("mr", "mark-recursively", "", "Repeat marking", @default: false);
        public static readonly Option MarkTransitiveDependenciesOption = new Option("mt", "mark-transitive", "", "Marks transitive dependencies", @default: false);
        public static readonly Option MarkSingleCyclesOption = new Option("mi", "mark-single-loops", "", "Mark single cycles", @default: false);

        private static readonly Option[] _transformOptions = DependencyMatchOptions.WithOptions(
            AddMarkerOption, MarkTransitiveDependenciesOption, MarkSingleCyclesOption
        );

        public override string GetHelp(bool detailedHelp, string filter) {
            return $@"Mark dependencies with special properties - UNTESTED.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        protected override Ignore CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            return Ignore.Om;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext, [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var transformOptions = new TransformOptions();
            //bool recursive = false;

            DependencyMatchOptions.Parse(globalContext, transformOptionsString, globalContext.IgnoreCase, transformOptions.Matches, transformOptions.Excludes,
                MarkSingleCyclesOption.Action((args, j) => {
                    transformOptions.MarkSingleCycleNodes = true;
                    return j;
                    //}), RecursiveMarkOption.Action((args, j) => {
                    //    recursive = true;
                    //    return j;
                }),
                MarkTransitiveDependenciesOption.Action((args, j) => {
                    transformOptions.MarkTransitiveDependencies = true;
                    return j;
                }),
                AddMarkerOption.Action((args, j) => {
                    transformOptions.MarkerToAdd = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim();
                    return j;
                }));

            return transformOptions;
        }

        public override int Transform([NotNull] GlobalContext globalContext, Ignore Ignore, [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, [NotNull] List<Dependency> transformedDependencies) {

            Dependency[] matchingDependencies = dependencies.Where(d => d.IsMarkerMatch(transformOptions.Matches, transformOptions.Excludes)).ToArray();

            if (transformOptions.MarkSingleCycleNodes) {
                foreach (var d in matchingDependencies) {
                    if (Equals(d.UsingItem, d.UsedItem)) {
                        d.IncrementMarker(transformOptions.MarkerToAdd);
                    }
                }
            }

            if (transformOptions.MarkTransitiveDependencies) {
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
                            itemsAtDistance1FromRoot, transformOptions.MarkerToAdd);
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

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            Item a = transformingGraph.CreateItem(ItemType.SIMPLE, "Ax");
            Item b = transformingGraph.CreateItem(ItemType.SIMPLE, "Bx");
            Item c = transformingGraph.CreateItem(ItemType.SIMPLE, "Cloop");
            Item d = transformingGraph.CreateItem(ItemType.SIMPLE, "Dloop");
            Item e = transformingGraph.CreateItem(ItemType.SIMPLE, "Eselfloop");
            Item f = transformingGraph.CreateItem(ItemType.SIMPLE, "Fy");
            Item g = transformingGraph.CreateItem(ItemType.SIMPLE, "Gy");
            Item h = transformingGraph.CreateItem(ItemType.SIMPLE, "Hy");
            Item i = transformingGraph.CreateItem(ItemType.SIMPLE, "Iy");
            Item j = transformingGraph.CreateItem(ItemType.SIMPLE, "Jy");
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
