using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.CycleChecking {
    public class FindCycleDeps : ITransformer {
        private class FindCycleDepsPathFinder<TDependency, TItem> : AbstractDepthFirstPathTraverser<TDependency, TItem>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            public readonly HashSet<TDependency> DependenciesOnCycles = new HashSet<TDependency>();
            public readonly HashSet<int> FoundCycleHashs = new HashSet<int>();

            public FindCycleDepsPathFinder(IEnumerable<TDependency> dependencies, ItemMatch cycleAnchorsMatch, 
                                           bool ignoreSelfCycles,  int maxCycleLength, IPathMatch<TDependency, TItem>[] expectedPathMatches) : base(retraverseItems: false) {
                Dictionary<TItem, TDependency[]> outgoing = AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);

                foreach (var i in outgoing.Keys.Where(i => ItemMatch.IsMatch(cycleAnchorsMatch, i)).OrderBy(i => i.Name)) {
                    Traverse(i, ignoreSelfCycles, outgoing, maxCycleLength, expectedPathMatches);
                }
            }

            protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
                // empty
            }

            protected override void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd) {
                // empty
            }

            protected override void OnFoundCycleToRoot(Stack<TDependency> currentPath) {
                int[] nodeHashes = currentPath.Select(d => d.UsingItem.GetHashCode()).ToArray();
                int minHashCode = nodeHashes[0];
                int minPos = 0;
                for (int i = 1; i < currentPath.Count; i++) {
                    if (nodeHashes[i] < minHashCode) {
                        minPos = i;
                        minHashCode = nodeHashes[i];
                    }
                }

                int cycleHash = 0;
                for (int i = minPos; i < nodeHashes.Length; i++) {
                    cycleHash = unchecked(cycleHash * 17 + nodeHashes[i]);
                }
                for (int i = 0; i < minPos; i++) {
                    cycleHash = unchecked(cycleHash * 17 + nodeHashes[i]);
                }
                if (FoundCycleHashs.Add(cycleHash)) {
                    // actually a new cycle
                    DependenciesOnCycles.UnionWith(currentPath);
                }
            }

            protected override void BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd) {
                // empty
            }
        }

        public static readonly Option IgnoreSelfCyclesOption = new Option("il", "ignore-loops", "",
            "ignore cycles of length 1, i.e. looping from an item to itself", @default: false);

        public static readonly Option KeepOnlyCyclesOption = new Option("kc", "keep-only-cycles", "",
            "remove all non-cycle dependencies", @default: false);

        public static readonly Option CycleAnchorsOption = new Option("ca", "cycle-anchors", "itempattern",
            "items checked for cycles through them", @default: "all items are checked");

        public static readonly Option MaxCycleLengthOption = new Option("ml", "max-length", "#",
            "maximum length of cycles found", @default: "arbitrary length");

        public static readonly DependencyEffectOptions EffectOptions = new DependencyEffectOptions();

        private static readonly IEnumerable<Option> _transformOptions =
            EffectOptions.AllOptions.Concat(new[]
                { IgnoreSelfCyclesOption, KeepOnlyCyclesOption, CycleAnchorsOption, MaxCycleLengthOption });

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Find cycles in dependency graph.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform(GlobalContext globalContext, [CanBeNull] string dependenciesFilename, IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {
            bool ignoreSelfCycles = false;
            bool keepOnlyCycleEdges = false;
            int maxCycleLength = int.MaxValue;
            ItemMatch cycleAnchorsMatch = null;

            IEnumerable<Action<Dependency>> effects = EffectOptions.Parse(globalContext: globalContext,
                argsAsString: transformOptions, ignoreCase: _ignoreCase, moreOptions: new[] {
                IgnoreSelfCyclesOption.Action((args, j) => {
                    ignoreSelfCycles = true;
                    return j;
                }),
                KeepOnlyCyclesOption.Action((args, j) => {
                    keepOnlyCycleEdges = true;
                    return j;
                }),
                CycleAnchorsOption.Action((args, j) => {
                    cycleAnchorsMatch = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), _ignoreCase);
                    return j;
                }),
                MaxCycleLengthOption.Action((args, j) => {
                    maxCycleLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum cycle length");
                    return j;
                })
            });

            // TODO: Cycles via path matches would also be a nice feature ...
            var cycleFinder = new FindCycleDepsPathFinder<Dependency,Item>(dependencies, cycleAnchorsMatch, ignoreSelfCycles, maxCycleLength, new IPathMatch<Dependency, Item>[0]);
            HashSet<Dependency> dependenciesOnCycles = cycleFinder.DependenciesOnCycles;
            HashSet<int> foundCycleHashs = cycleFinder.FoundCycleHashs;

            Log.WriteInfo($"... found {foundCycleHashs.Count} cycles");

            if (effects.Contains(DependencyEffectOptions.DELETE_ACTION_MARKER)) {
                var deps = new HashSet<Dependency>(dependencies);
                deps.ExceptWith(dependenciesOnCycles);
                transformedDependencies.AddRange(deps);
            } else {
                DependencyEffectOptions.Execute(effects, dependenciesOnCycles);
                transformedDependencies.AddRange(keepOnlyCycleEdges ? dependenciesOnCycles : dependencies);
            }
            return Program.OK_RESULT;
        }

        public void AfterAllTransforms(GlobalContext globalContext) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            Item a = Item.New(ItemType.SIMPLE, "A");
            Item b = Item.New(ItemType.SIMPLE, "B");
            Item c = Item.New(ItemType.SIMPLE, "C");
            Item d = Item.New(ItemType.SIMPLE, "D");
            Item e = Item.New(ItemType.SIMPLE, "E");
            Item f = Item.New(ItemType.SIMPLE, "F");
            Item g = Item.New(ItemType.SIMPLE, "G");
            Item h = Item.New(ItemType.SIMPLE, "H");
            Item i = Item.New(ItemType.SIMPLE, "I");
            Item j = Item.New(ItemType.SIMPLE, "J");

            //   a<=>b->c->d->e<=>f->g
            //          ^  ^         |
            // h<=>i->j-+  +---------+
            return new[] {
                new Dependency(a,b,source: null, markers: "", ct:1), // on cycle
                new Dependency(b,a,source: null, markers: "", ct:1), // on cycle
                new Dependency(b,c,source: null, markers: "", ct:1),
                new Dependency(c,d,source: null, markers: "", ct:1),
                new Dependency(d,e,source: null, markers: "", ct:1), // on cycle
                new Dependency(e,f,source: null, markers: "", ct:1), // on cycle
                new Dependency(f,e,source: null, markers: "", ct:1), // on cycle
                new Dependency(f,g,source: null, markers: "", ct:1), // on cycle
                new Dependency(g,d,source: null, markers: "", ct:1), // on cycle
                new Dependency(h,i,source: null, markers: "", ct:1), // on cycle
                new Dependency(i,h,source: null, markers: "", ct:1), // on cycle
                new Dependency(i,j,source: null, markers: "", ct:1),
                new Dependency(j,c,source: null, markers: "", ct:1),
            };
        }
    }
}
