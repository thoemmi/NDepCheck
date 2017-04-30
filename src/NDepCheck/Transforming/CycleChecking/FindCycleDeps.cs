using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.CycleChecking {
    public class FindCycleDeps : ITransformer {
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

        private int AddHash(int hash, [NotNull] Item i) {
            // This is a somewhat complicated hash: Its purpose is to map cyclically
            // shifted cycles to the same hashcode. For simplicity, it is therefore
            // commutative.
            // However, a simple XOR would be bad if e.g. the items were named A, B, C, D:
            // Then, A ^ B is the same as C ^ D. So, I try to inject the names somewhat
            // more "specifically": I do this by also XORing with a single bit at some
            // name-dependent position (not ORing, because that would fill up the
            // hash code to all 1s).
            // Hopefully, this works.
            var h = i.GetHashCode();
            return hash ^ h ^ (1 << (h % 31));

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

            Dictionary<Item, IEnumerable<Dependency>> outgoing = Item.CollectOutgoingDependenciesMap(dependencies);

            var foundCycleHashs = new HashSet<int>();
            var dependenciesOnCycles = new HashSet<Dependency>();
            foreach (var i in outgoing.Keys.Where(i => ItemMatch.IsMatch(cycleAnchorsMatch, i)).OrderBy(i => i.Name)) {
                var pathHeadFromI = new Stack<Dependency>();
                var visitedItem2CheckedPathLengthBehindVisitedItem = new Dictionary<Item, int>();
                FindCyclesFrom(i, i, ignoreSelfCycles, outgoing, visitedItem2CheckedPathLengthBehindVisitedItem, 
                    maxCycleLength, foundCycleHashs, AddHash(0, i), pathHeadFromI, dependenciesOnCycles);
            }

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

        private void FindCyclesFrom(Item root, Item tail, bool ignoreCyclesInThisRecursion,
            Dictionary<Item, IEnumerable<Dependency>> outgoing, Dictionary<Item, int> allVisitedItems, int restLength,
            HashSet<int> foundCycleHashs, int pathHash, Stack<Dependency> pathFromRoot, HashSet<Dependency> dependenciesOnCycles) {
            int checkedBehindTail;
            if ((!allVisitedItems.TryGetValue(tail, out checkedBehindTail) || checkedBehindTail < restLength)
                && restLength > 0 && outgoing.ContainsKey(tail)) {
                allVisitedItems[tail] = restLength;
                // we are at this item for the first time - check whether we find a path back to the root item
                foreach (var nextDep in outgoing[tail]) {
                    Item newTail = nextDep.UsedItem;
                    pathFromRoot.Push(nextDep);
                    if (!ignoreCyclesInThisRecursion && Equals(newTail, root)) {
                        // We found a cycle to the rootItem!

                        pathHash ^= restLength;
                        if (foundCycleHashs.Contains(pathHash)) {
                            // The cycle was already found via another item
                            // - we ignore it.
                        } else {
                            // New cycle found; we record it
                            dependenciesOnCycles.UnionWith(pathFromRoot);
                            foundCycleHashs.Add(pathHash);
                        }
                    } else {
                        FindCyclesFrom(root, newTail, false, outgoing, allVisitedItems, restLength - 1, foundCycleHashs,
                            AddHash(pathHash, newTail), pathFromRoot, dependenciesOnCycles);
                    }
                    pathFromRoot.Pop();
                }
            }
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
