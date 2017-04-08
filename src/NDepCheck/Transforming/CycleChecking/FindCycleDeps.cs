using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.CycleChecking {
    public class FindCycleDeps : ITransformer {
        public static readonly Option IgnoreSelfCyclesOption = new Option("il", "ignore-loops", "", "ignore cycles of length 1, i.e. looping from an item to itself", @default: false);
        public static readonly Option KeepOnlyCyclesOption = new Option("kc", "keep-only-cycles", "", "remove all non-cycle dependencies", @default: false);
        public static readonly Option CycleAnchorsOption = new Option("ca", "cycle-anchors", "itempattern", "nodes checked for cycles through them", @default: "all nodes are checked");
        public static readonly Option MaxCycleLengthOption = new Option("ml", "max-length", "#", "maximum length of cycles found", @default: "arbitrary length");
        public static readonly DepencencyEffectOptions EffectOptions = new DepencencyEffectOptions();

        private static readonly IEnumerable<Option> _transformOptions =
            EffectOptions.AllOptions.Concat(new[] { IgnoreSelfCyclesOption, KeepOnlyCyclesOption, CycleAnchorsOption, MaxCycleLengthOption });

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp) {
            return $@"Find cycles in dependency graph.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp)}";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions) {
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

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies, string transformOptions,
            string dependencySourceForLogging, List<Dependency> transformedDependencies) {
            bool ignoreSelfCycles = false;
            bool keepOnlyCycleEdges = false;
            int maxCycleLength = int.MaxValue;
            ItemMatch cycleAnchors = null;

            IEnumerable<Action<Dependency>> effects = EffectOptions.Parse(transformOptions,
                 moreOptions: new[] {
                    IgnoreSelfCyclesOption.Action((args, j) => {
                        ignoreSelfCycles = true;
                        return j;
                    }),
                    KeepOnlyCyclesOption.Action((args, j) => {
                        keepOnlyCycleEdges = true;
                        return j;
                    }),
                    CycleAnchorsOption.Action((args, j) => {
                        cycleAnchors = new ItemMatch(null, Option.ExtractOptionValue(args, ref j), _ignoreCase);
                        return j;
                    }),
                    MaxCycleLengthOption.Action((args, j) => {
                        maxCycleLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum cycle length");
                        return j;
                    })
                 });

            var from = new Dictionary<Item, List<Dependency>>();
            foreach (var d in dependencies) {
                List<Dependency> edges;
                if (!from.TryGetValue(d.UsingItem, out edges)) {
                    from.Add(d.UsingItem, edges = new List<Dependency>());
                }
                edges.Add(d);
            }

            var foundCycleHashs = new HashSet<int>();
            var trackItems = new HashSet<Item>();
            var dependenciesOnCycles = new HashSet<Dependency>();
            var path = new Stack<Dependency>();
            foreach (var i in from.Keys.Where(i => cycleAnchors == null || cycleAnchors.Matches(i) != null)) {
                FindCyclesFrom(i, i, ignoreSelfCycles, from, trackItems, maxCycleLength, foundCycleHashs, AddHash(0, i), path, dependenciesOnCycles);
            }

            if (effects.Contains(DepencencyEffectOptions.DELETE_ACTION_MARKER)) {
                var deps = new HashSet<Dependency>(dependencies);
                deps.ExceptWith(dependenciesOnCycles);
                transformedDependencies.AddRange(deps);
            } else {
                DepencencyEffectOptions.Execute(effects, dependenciesOnCycles);
                transformedDependencies.AddRange(keepOnlyCycleEdges ? dependenciesOnCycles : dependencies);
            }
            return Program.OK_RESULT;
        }

        private void FindCyclesFrom(Item rootItem, Item tailItem, bool ignoreCyclesInThisRecursion,
                                    Dictionary<Item, List<Dependency>> from, HashSet<Item> trackItems,
                                    int restLength, HashSet<int> foundCycleHashs, int pathHash, Stack<Dependency> path,
                                    HashSet<Dependency> dependenciesOnCycles) {
            if (restLength > 0) {
                trackItems.Add(tailItem);
                foreach (var nextDep in from[tailItem]) {
                    var newTail = nextDep.UsedItem;
                    path.Push(nextDep);
                    if (trackItems.Contains(newTail)) {
                        if (!ignoreCyclesInThisRecursion && Equals(newTail, rootItem)) {
                            // We found a cycle through the rootItem

                            pathHash ^= restLength;
                            if (foundCycleHashs.Contains(pathHash)) {
                                // The cycle was already found via another node
                                // - we ignore it.
                            } else {
                                // New cycle found; we record it
                                foreach (var pathDep in path) {
                                    dependenciesOnCycles.Add(pathDep);
                                }
                                foundCycleHashs.Add(pathHash);
                            }
                        } else {
                            // We found another cycle; we ignore it, but
                            // stop recursing
                        }
                    } else {
                        FindCyclesFrom(rootItem, newTail, false, from, trackItems, restLength - 1, foundCycleHashs,
                                       AddHash(pathHash, newTail), path, dependenciesOnCycles);
                    }
                    path.Pop();
                }
                trackItems.Remove(tailItem);
            }
        }

        public void FinishTransform(GlobalContext context) {
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
                new Dependency(a,b,source: null, usage: "", ct:1), // on cycle
                new Dependency(b,a,source: null, usage: "", ct:1), // on cycle
                new Dependency(b,c,source: null, usage: "", ct:1),
                new Dependency(c,d,source: null, usage: "", ct:1),
                new Dependency(d,e,source: null, usage: "", ct:1), // on cycle
                new Dependency(e,f,source: null, usage: "", ct:1), // on cycle
                new Dependency(f,e,source: null, usage: "", ct:1), // on cycle
                new Dependency(f,g,source: null, usage: "", ct:1), // on cycle
                new Dependency(g,d,source: null, usage: "", ct:1), // on cycle
                new Dependency(h,i,source: null, usage: "", ct:1), // on cycle
                new Dependency(i,h,source: null, usage: "", ct:1), // on cycle
                new Dependency(i,j,source: null, usage: "", ct:1),
                new Dependency(j,c,source: null, usage: "", ct:1),
            };
        }
    }
}
