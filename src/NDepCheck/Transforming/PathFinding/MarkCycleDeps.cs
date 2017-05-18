using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.PathFinding {
    public class MarkCycleDeps : ITransformer {
        private class FindCycleDepsPathFinder<TDependency, TItem>
            : AbstractDepthFirstPathTraverser<TDependency, TItem, Ignore, Ignore, Ignore>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly HashSet<int> _foundCycleHashs = new HashSet<int>();
            private readonly Dictionary<TItem, int> _visited2RestLength;
            private readonly int _maxCycleLength;
            private readonly TItem _root;
            private readonly bool _ignoreSelfCycles;
            [NotNull]
            private readonly Action<int, Stack<TDependency>, string> _recordNewCycleToRoot;

            private readonly string _addIndexToMarkerFormat;

            public int FoundCycleCount => _foundCycleHashs.Count;

            public FindCycleDepsPathFinder([NotNull, ItemNotNull] IEnumerable<TDependency> dependencies,
                    [CanBeNull] ItemMatch cycleAnchorsMatch, bool ignoreSelfCycles, int maxCycleLength,
                    [NotNull] Action<int, Stack<TDependency>, string> recordNewCycleToRoot,
                    [NotNull, ItemCanBeNull] AbstractPathMatch<TDependency, TItem>[] expectedPathMatches, Action checkAbort) : base(checkAbort) {
                Dictionary<TItem, TDependency[]> outgoing = AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);
                _maxCycleLength = maxCycleLength;
                _ignoreSelfCycles = ignoreSelfCycles;
                _recordNewCycleToRoot = recordNewCycleToRoot;

                IEnumerable<TItem> roots = outgoing.Keys.Where(i => ItemMatch.IsMatch(cycleAnchorsMatch, i));
                _addIndexToMarkerFormat = "D" + ("" + roots.Count() / 2).Length;

                foreach (var root in roots.OrderBy(i => i.Name)) {
                    _root = root;
                    _visited2RestLength = new Dictionary<TItem, int>();
                    Traverse(root, outgoing, expectedPathMatches, endMatch: null, down: Ignore.Om);
                }
            }

            protected override bool ShouldVisitSuccessors(TItem tail, Stack<TDependency> currentPath, int expectedPathMatchIndex,
                    out Ignore initUpSum) {
                initUpSum = Ignore.Om;
                if (currentPath.Count == 0) {
                    // We are at root ... nothing to do, and of course we visit all successors
                    return true;
                } else if (currentPath.Count > _maxCycleLength) {
                    // We have reached the checking limit - no further graph traversal
                    return false;
                } else if (Equals(tail, _root)) {
                    // We found a cycle to the root
                    if (currentPath.Count == 1 && _ignoreSelfCycles) {
                        // Self cycles is ignored
                    } else {
                        RecordCycleToRoot(currentPath);
                    }
                    // No need to drill deeper after the cycle is found
                    return false;
                } else {
                    int lengthToBeChecked = _maxCycleLength - currentPath.Count;
                    int restLengthCheckedSoFar;
                    if (!_visited2RestLength.TryGetValue(tail, out restLengthCheckedSoFar)) {
                        // We never visited tail up to now
                        _visited2RestLength.Add(tail, _maxCycleLength - currentPath.Count);
                        return true;
                    } else {
                        // We did visit tail before ...
                        if (lengthToBeChecked > restLengthCheckedSoFar) {
                            // ... but last time the rest length was smaller than now, so we must retraverse it
                            _visited2RestLength[tail] = lengthToBeChecked;
                            return true;
                        } else {
                            // ... and we don't have to drill deeper.
                            return false;
                        }
                    }
                }
            }

            protected override DownAndHere AfterPushDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                    AbstractPathMatch<TDependency, TItem> pathMatchOrNull,
                    bool isEnd, Ignore down) {
                return new DownAndHere();
            }

            private void RecordCycleToRoot(Stack<TDependency> currentPath) {
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
                if (_foundCycleHashs.Add(cycleHash)) {
                    _recordNewCycleToRoot(_foundCycleHashs.Count - 1, currentPath, _addIndexToMarkerFormat);
                }
            }

            protected override Ignore BeforePopDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                    AbstractPathMatch<TDependency, TItem> pathMatchOrNull,
                    bool isEnd, Ignore here, Ignore upSum, Ignore childUp) {
                return childUp;
            }

            protected override Ignore AfterVisitingSuccessors(bool visitSuccessors, TItem tail,
                    Stack<TDependency> currentPath, int expectedPathMatchIndex, Ignore upSum) {
                return upSum;
            }
        }

        public static readonly Option IgnoreSelfCyclesOption = new Option("il", "ignore-loops", "",
            "ignore cycles of length 1, i.e. looping from an item to itself", @default: false);

        public static readonly Option KeepOnlyCyclesOption = new Option("kc", "keep-only-cycles", "",
            "remove all non-cycle dependencies", @default: false);

        public static readonly Option CycleAnchorsOption = new Option("ca", "cycle-anchors", "itempattern", "items checked for cycles through them", @default: "all items are checked");
        public static readonly Option MaxCycleLengthOption = new Option("ml", "max-length", "#", "maximum length of cycles found", @default: "arbitrary length");
        public static readonly Option AddIndexedMarkerOption = new Option("im", "indexed-marker", "&", "add separate cycle markers starting with &", @default: "");
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

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, [NotNull] List<Dependency> transformedDependencies) {
            bool ignoreSelfCycles = false;
            bool keepOnlyCycleEdges = false;
            int maxCycleLength = int.MaxValue;
            ItemMatch cycleAnchorsMatch = null;
            string indexedMarkerPrefix = null;

            IEnumerable<Action<Dependency>> effects = EffectOptions.Parse(globalContext: globalContext,
                argsAsString: transformOptions, defaultReasonForSetBad: typeof(MarkCycleDeps).Name,
                    ignoreCase: _ignoreCase, moreOptionActions: new[] {
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
                    }),
                    AddIndexedMarkerOption.Action((args, j) => {
                        indexedMarkerPrefix = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                        return j;
                    }),
                });

            var dependenciesOnCycles = new HashSet<Dependency>();
            Action<int, Stack<Dependency>, string> recordNewCycleToRoot = (cycleIndex, cycle, addIndexToMarkerFormat) => dependenciesOnCycles.UnionWith(cycle);

            if (indexedMarkerPrefix != null) {
                recordNewCycleToRoot += (cycleIndex, cycle, addIndexToMarkerFormat) => {
                    string indexedMarker = indexedMarkerPrefix + cycleIndex.ToString(addIndexToMarkerFormat);
                    Dependency[] cycleDependencies = cycle.Reverse().ToArray();
                    cycleDependencies[0].UsingItem.MarkPathElement(indexedMarker, 0, isStart: true, isEnd: false, isMatchedByCountMatch: false, isLoopBack: false);
                    for (var i = 0; i < cycleDependencies.Length; i++) {
                        Dependency d = cycleDependencies[i];
                        bool isEnd = i == cycleDependencies.Length - 1;
                        d.MarkPathElement(indexedMarker, i, isStart: i == 0, isEnd: isEnd, isMatchedByCountMatch: false, isLoopBack: isEnd);
                        // Currently no reachability counts, as no path match options are implemented
                    }
                };
            }

            var cycleFinder = new FindCycleDepsPathFinder<Dependency, Item>(dependencies, cycleAnchorsMatch, ignoreSelfCycles,
                                    maxCycleLength, recordNewCycleToRoot, new AbstractPathMatch<Dependency, Item>[0], // TODO: Cycles via path matches would also be a nice feature ...
                                    globalContext.CheckAbort);

            Log.WriteInfo($"... found {cycleFinder.FoundCycleCount} cycles");

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

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
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
