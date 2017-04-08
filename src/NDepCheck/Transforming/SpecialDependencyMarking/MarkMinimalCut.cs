using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.SpecialDependencyMarking {
    public class MarkMinimalCutDeps {
        public static readonly Option MatchSourceOption = new Option("ms", "match-sources", "&", "Match to select source items", @default: null, multiple: true);
        public static readonly Option MatchTargetOption = new Option("mt", "match-targets", "&", "Match to select target items", @default: null, multiple: true);
        public static readonly Option MarkerToAddOption = new Option("ma", "marker-to-add", "&", "Marker added to identified items", @default: null);
        public static readonly Option UseQuestionableCountOption = new Option("uq", "use-questionable-count", "", "Use questionable count as weight", @default: "Use bad count");
        public static readonly Option UseCountOption = new Option("uc", "use-count", "", "Use count as weight", @default: "Use bad count");

        private static readonly Option[] _transformOptions = {
            MatchSourceOption, MatchTargetOption, MarkerToAddOption
        };

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp) {
            return $@"Mark dependencies with special properties - UNTESTED.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp)}";
        }

        public bool RunsPerInputContext => true;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        private class Edge {
            public readonly Dependency Dependency;
            public readonly int Capacity;
            public int Flow;

            public Edge(Dependency dependency, Func<Dependency, int> capacity) {
                Dependency = dependency;
                Capacity = capacity(dependency);
                Flow = 0;
            }

            public Item UsingItem => Dependency.UsingItem;
            public Item UsedItem => Dependency.UsedItem;

            public bool InResidual => Flow < Capacity;
            public bool ReverseInResidual => Flow > 0;
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            var sourceMatches = new List<ItemMatch>();
            var targetMatches = new List<ItemMatch>();
            //string markerToAddToSourceSide = null;
            //string markerToAddToTargetSide = null;
            string markerToAddToCut = null;
            Func<Dependency, int> weightForCut = d => d.BadCt;

            Option.Parse(transformOptions,
                MatchSourceOption.Action((args, j) => {
                    sourceMatches.Add(new ItemMatch(null, Option.ExtractOptionValue(args, ref j), _ignoreCase));
                    return j;
                }),
                MatchTargetOption.Action((args, j) => {
                    targetMatches.Add(new ItemMatch(null, Option.ExtractOptionValue(args, ref j), _ignoreCase));
                    return j;
                }),
                UseQuestionableCountOption.Action((args, j) => {
                    weightForCut = d => d.QuestionableCt;
                    return j;
                }),
                UseCountOption.Action((args, j) => {
                    weightForCut = d => d.Ct;
                    return j;
                }),
                MarkerToAddOption.Action((args, j) => {
                    markerToAddToCut = Option.ExtractOptionValue(args, ref j).Trim('\'').Trim();
                    return j;
                }));

            var items = new HashSet<Item>(dependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
            var sourceItems = new List<Item>(items.Where(i => sourceMatches.Any(m => m.Matches(i) != null)));
            var targetItems = new HashSet<Item>(items.Where(i => targetMatches.Any(m => m.Matches(i) != null)));
            if (targetItems.Overlaps(sourceItems)) {
                throw new ApplicationException("Source and target items overlap - minimal cut cannot be computed");
            }

            // According to the "Max-flow min-cut theorem" (see e.g.
            // http://www.cs.princeton.edu/courses/archive/spr04/cos226/lectures/maxflow.4up.pdf, 
            // http://web.stanford.edu/class/cs97si/08-network-flow-problems.pdf,
            // https://en.wikipedia.org/wiki/Max-flow_min-cut_theorem), we first find the maximum 
            // flow; then the residual graph; and from this the minimal cut.

            // Find maximal flow from sources to targets via the Ford-Fulkerson algorithm - see e.g. 
            // http://www.cs.princeton.edu/courses/archive/spr04/cos226/lectures/maxflow.4up.pdf;
            Dictionary<Dependency, Edge> edges = dependencies.ToDictionary(d => d, d => new Edge(d, weightForCut));

            Dictionary<Item, List<Edge>> outgoing = Item.CollectMap(dependencies, d => d.UsingItem, d => edges[d]);
            SortByFallingCapacity(outgoing);

            Dictionary<Item, List<Edge>> incoming = Item.CollectMap(dependencies, d => d.UsedItem, d => edges[d]);
            SortByFallingCapacity(incoming);

            bool flowIncreased;
            do {
                flowIncreased = false;
                foreach (var i in sourceItems) {
                    int increase = IncreaseFlow(i, int.MaxValue, new HashSet<Item> { i }, outgoing, incoming, targetItems);
                    flowIncreased |= increase > 0;
                }
            } while (flowIncreased);

            // Find residual graph (only on sources side)
            var reachableFromSources = new HashSet<Item>(sourceItems);
            foreach (var i in sourceItems) {
                AddReachableInResidualGraph(i, reachableFromSources, outgoing, incoming);
            }

            // Find minimal cut
            foreach (var s in reachableFromSources) {
                foreach (var d in outgoing[s].Where(e => !reachableFromSources.Contains(e.UsedItem))) {
                    d.Dependency.AddMarker(markerToAddToCut);
                }
            }

            transformedDependencies.AddRange(dependencies);

            return Program.OK_RESULT;
        }

        private static void SortByFallingCapacity(Dictionary<Item, List<Edge>> e) {
            foreach (var li in e.Values) {
                li.Sort((e1, e2) => e2.Capacity - e1.Capacity);
            }
        }

        private int IncreaseFlow(Item item, int maxPossibleFlowIncreaseAlongPath, HashSet<Item> visited,
            Dictionary<Item, List<Edge>> outgoing, Dictionary<Item, List<Edge>> incoming,
            HashSet<Item> targetItems) {
            if (targetItems.Contains(item)) {
                return maxPossibleFlowIncreaseAlongPath;
            } else {
                foreach (var e in outgoing[item]) {
                    int possibleFlowIncrease = Math.Min(maxPossibleFlowIncreaseAlongPath, e.Capacity - e.Flow);
                    int actualIncrease = ComputeActualIncrease(visited, outgoing, incoming, targetItems, possibleFlowIncrease, e.UsedItem);
                    if (actualIncrease > 0) {
                        // There is a path to the end with free capacity actualReduction - we reduce current edge!
                        e.Flow += actualIncrease;
                        return actualIncrease;
                    }
                }
                if (incoming.ContainsKey(item)) { // not true for sources - no need to check for back flows there!
                    foreach (var e in incoming[item]) {
                        int possiblePushedBackFlow = Math.Min(maxPossibleFlowIncreaseAlongPath, e.Flow);
                        var actualIncrease = ComputeActualIncrease(visited, outgoing, incoming, targetItems,
                            possiblePushedBackFlow, e.UsedItem);
                        if (actualIncrease > 0) {
                            // There is a path to the end with free capacity actualReduction - we reduce current edge!
                            e.Flow -= actualIncrease;
                            return actualIncrease;
                        }
                    }
                }
                return 0;
            }
        }

        private int ComputeActualIncrease(HashSet<Item> visited, Dictionary<Item, List<Edge>> outgoing, 
                                          Dictionary<Item, List<Edge>> incoming, HashSet<Item> targetItems,
            int possibleFlowIncrease, Item usedItem) {
            try {
                return possibleFlowIncrease > 0 && visited.Add(usedItem)
                    ? IncreaseFlow(usedItem, possibleFlowIncrease, visited, outgoing, incoming, targetItems)
                    : 0;
            } finally {
                visited.Remove(usedItem);
            }
        }

        private void AddReachableInResidualGraph(Item item, HashSet<Item> reachableFromSources, Dictionary<Item, List<Edge>> outgoing, Dictionary<Item, List<Edge>> incoming) {
            foreach (var e in outgoing[item].Where(e => e.InResidual)) {
                if (reachableFromSources.Add(e.UsedItem)) {
                    AddReachableInResidualGraph(e.UsedItem, reachableFromSources, outgoing, incoming);
                }
            }
            if (incoming.ContainsKey(item)) { // not true for sources
                foreach (var e in incoming[item].Where(e => e.ReverseInResidual)) {
                    if (reachableFromSources.Add(e.UsingItem)) {
                        AddReachableInResidualGraph(e.UsingItem, reachableFromSources, outgoing, incoming);
                    }
                }
            }
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
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
