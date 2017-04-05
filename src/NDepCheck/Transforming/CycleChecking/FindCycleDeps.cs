using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.CycleChecking {
    public class FindCycleDeps : ITransformer {
        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp) {
            return @"Remove nodes - UNTESTED.

Configuration options: None

Transformer options: [-m typename pattern] [-i] [-n #] [-q] [-u &]
  -m      Regular expression matching node to remove; default: match none
  -i      Ignore cycles of length 1
  -k      Keep only cycle dependencies
  -q      Set questionable count for cycle edges; default: set bad count
  -u &    Set usage of cycle edges to &; default: do not change usage
  -n #    Limit length of detected cycles to #; default: do not limit length
";
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

            ItemMatch match = null;
            bool ignoreSelfCycles = false;
            bool setQuestionableCount = false;
            bool keepOnlyCycleEdges = false;
            string usageToAdd = null;
            int maxCycleLength = int.MaxValue;
            Option.Parse(transformOptions, new OptionAction("m", (args, j) => {
                string itemTypeName = Option.ExtractOptionValue(args, ref j);
                string itemPattern = Option.ExtractNextValue(args, ref j);
                ItemType itemType = ItemType.Find(itemTypeName);
                if (itemType == null) {
                    throw new ArgumentException($"Cannot find type {itemTypeName}");
                }
                match = new ItemMatch(itemType, itemPattern, _ignoreCase);
                return j;
            }), new OptionAction("i", (args, j) => {
                ignoreSelfCycles = true;
                return j;
            }), new OptionAction("k", (args, j) => {
                keepOnlyCycleEdges = true;
                return j;
            }), new OptionAction("n", (args, j) => {
                maxCycleLength = Option.ExtractIntOptionValue(args, ref j, "No valid cycle length after -n");
                return j;
            }), new OptionAction("q", (args, j) => {
                setQuestionableCount = true;
                return j;
            }), new OptionAction("u", (args, j) => {
                usageToAdd = Option.ExtractOptionValue(args, ref j);
                return j;
            }));

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
            foreach (var i in from.Keys.Where(i => match == null || match.Match(i))) {
                FindCyclesFrom(i, i, ignoreSelfCycles, from, trackItems, maxCycleLength, foundCycleHashs, AddHash(0, i), path, dependenciesOnCycles);
            }

            foreach (var d in dependenciesOnCycles) {
                if (setQuestionableCount) {
                    d.MarkAsQuestionable();
                } else {
                    d.MarkAsBad();
                }
                if (usageToAdd != null) {
                    d.AddUsage(usageToAdd);
                }
            }

            transformedDependencies.AddRange(keepOnlyCycleEdges ? dependenciesOnCycles : dependencies);

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
