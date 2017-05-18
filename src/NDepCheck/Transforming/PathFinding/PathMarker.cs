using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;
using NDepCheck.Transforming.PathFinding;

namespace NDepCheck.Rendering.PathFinding {
    public class PathMarker : ITransformer {
        private struct DownInfo {
            public readonly IMatchableObject BehindCountMatch;

            public DownInfo(IMatchableObject behindCountMatch) {
                BehindCountMatch = behindCountMatch;
            }
        }

        private struct HereInfo {
            public readonly bool DependencyMatchedByCountMatch;
            public readonly bool UsedItemMatchedByCountMatch;

            public HereInfo(bool dependencyMatchedByCountMatch, bool usedItemMatchedByCountMatch) {
                DependencyMatchedByCountMatch = dependencyMatchedByCountMatch;
                UsedItemMatchedByCountMatch = usedItemMatchedByCountMatch;
            }
        }

        private class PathNode<TItem, TDependency>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            public bool IsEnd {
                get;
            }
            public bool LeadsToNodesToBePrinted {
                get;
            }
            public readonly TDependency Dependency;
            public readonly bool IsEndOfCycle;
            public readonly bool DependencyMatchedByCountMatch;
            public readonly bool UsedItemMatchedByCountMatch;

            [NotNull, ItemNotNull]
            public readonly IEnumerable<PathNode<TItem, TDependency>> Children;

            public PathNode(TDependency dependency, bool isEnd, bool isEndOfCycle,
                            bool dependencyMatchedByCountMatch, bool usedItemMatchedByCountMatch, IEnumerable<PathNode<TItem, TDependency>> children) {
                Dependency = dependency;
                IsEnd = isEnd;
                IsEndOfCycle = isEndOfCycle;
                DependencyMatchedByCountMatch = dependencyMatchedByCountMatch;
                UsedItemMatchedByCountMatch = usedItemMatchedByCountMatch;
                Children = children ?? Enumerable.Empty<PathNode<TItem, TDependency>>();
                LeadsToNodesToBePrinted = isEndOfCycle || isEnd || Children.Any(ch => ch.LeadsToNodesToBePrinted);
            }
        }

        private class PathWriterTraverser<TDependency, TItem> : AbstractDepthFirstPathTraverser<TDependency, TItem,
            DownInfo, HereInfo, List<PathNode<TItem, TDependency>>>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly int _maxPathLength;
            private readonly AbstractPathMatch<TDependency, TItem> _countMatch;
            private readonly HashSet<TItem> _seenInnerPathStarts = new HashSet<TItem>();

            public readonly Dictionary<TItem, List<PathNode<TItem, TDependency>>> Paths = new Dictionary<TItem, List<PathNode<TItem, TDependency>>>();
            public readonly Dictionary<TItem, HashSet<IMatchableObject>> Counts = new Dictionary<TItem, HashSet<IMatchableObject>>();

            private readonly HashSet<ItemAndInt> _onPath = new HashSet<ItemAndInt>();
            private readonly bool _backwards;

            private int _nodesWithEndFlag;

            public PathWriterTraverser(int maxPathLength, bool backwards, AbstractPathMatch<TDependency, TItem> countMatch, [NotNull] Action checkAbort) : base(checkAbort) {
                _maxPathLength = maxPathLength;
                _backwards = backwards;
                _countMatch = countMatch;
            }

            public int NodesWithEndFlag => _nodesWithEndFlag;

            public void Traverse(IEnumerable<TDependency> dependencies, ItemMatch pathAnchor,
                bool pathAnchorIsCountMatch, AbstractPathMatch<TDependency, TItem>[] expectedPathMatches) {
                Dictionary<TItem, TDependency[]> incidentDependencies = _backwards
                    ? AbstractItem<TItem>.CollectIncomingDependenciesMap(dependencies)
                    : AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);
                _seenInnerPathStarts.Clear();
                Paths.Clear();
                Counts.Clear();
                _onPath.Clear();

                IEnumerable<TItem> uniqueStartItems = pathAnchor == null
                    ? incidentDependencies.Keys
                    : incidentDependencies.Keys.Where(i => pathAnchor.Matches(i).Success);
                AbstractPathMatch<TDependency, TItem> endMatch;
                AbstractPathMatch<TDependency, TItem>[] innerMatches;
                int n = expectedPathMatches.Length;
                if (n == 0) {
                    endMatch = null;
                    innerMatches = expectedPathMatches;
                } else {
                    endMatch = expectedPathMatches[n - 1];
                    innerMatches = expectedPathMatches.Take(n - 1).ToArray();
                }

                foreach (var item in uniqueStartItems.OrderBy(i => i.Name)) {
                    if (_seenInnerPathStarts.Add(item)) {
                        List<PathNode<TItem, TDependency>> up = Traverse(root: item,
                            incidentDependencies: incidentDependencies, expectedInnerPathMatches: innerMatches,
                            endMatch: endMatch, down: new DownInfo(pathAnchorIsCountMatch ? item : null));
                        if (up != null) {
                            Paths.Add(item, up);
                        }
                    }
                }
            }

            protected override bool ShouldVisitSuccessors(TItem tail, Stack<TDependency> currentPath, int expectedPathMatchIndex, out List<PathNode<TItem, TDependency>> initUpSum) {
                initUpSum = new List<PathNode<TItem, TDependency>>();
                var onPathItem = new ItemAndInt(tail, expectedPathMatchIndex);
                if (currentPath.Count == 0) {
                    // Behind head
                    _onPath.Add(onPathItem);
                    return true;
                } else if (currentPath.Count >= _maxPathLength) {
                    // We have reached the checking limit - no further graph traversal
                    return false;
                } else {
                    // Check for loop
                    bool tailNewOnPath = _onPath.Add(onPathItem);
                    return tailNewOnPath;
                }
            }

            protected override DownAndHere AfterPushDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                    AbstractPathMatch<TDependency, TItem> pathMatchOrNull, bool isEnd, DownInfo down) {
                TDependency tailDependency = currentPath.Peek();
                TItem usedItem = tailDependency.UsedItem;
                if (down.BehindCountMatch != null) {
                    if (!Counts.ContainsKey(usedItem)) {
                        Counts[usedItem] = new HashSet<IMatchableObject>();
                    }
                    Counts[usedItem].Add(down.BehindCountMatch);
                }
                _seenInnerPathStarts.Add(usedItem);

                IMatchableObject pushDownMatch = null;
                bool dependencyMatchedByCountMatch = false;
                bool usedItemMatchedByCountMatch = false;
                if (down.BehindCountMatch != null) {
                    pushDownMatch = down.BehindCountMatch;
                } else if (_countMatch == null) {
                    // default values are ok
                } else if (_countMatch == pathMatchOrNull) {
                    if (_countMatch is DependencyPathMatch<TDependency, TItem>) {
                        pushDownMatch = tailDependency;
                        dependencyMatchedByCountMatch = true;
                    } else {
                        pushDownMatch = usedItem;
                        usedItemMatchedByCountMatch = true;
                    }
                } else {
                    // default values are ok
                }
                return new DownAndHere(new DownInfo(pushDownMatch), new HereInfo(dependencyMatchedByCountMatch, usedItemMatchedByCountMatch));
            }

            protected override List<PathNode<TItem, TDependency>> BeforePopDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                    AbstractPathMatch<TDependency, TItem> pathMatchOrNull, bool isEnd,
                    HereInfo here, List<PathNode<TItem, TDependency>> upSum, List<PathNode<TItem, TDependency>> childUp) {

                TDependency top = currentPath.Peek();
                var key = new ItemAndInt(top.UsedItem, expectedPathMatchIndex);
                bool isEndOfCycle = _onPath.Contains(key);

                if (isEnd || isEndOfCycle || childUp.Any(p => p.LeadsToNodesToBePrinted)) {
                    PathNode<TItem, TDependency> n = new PathNode<TItem, TDependency>(top, isEnd,
                        isEndOfCycle, here.DependencyMatchedByCountMatch, here.UsedItemMatchedByCountMatch, childUp);
                    if (isEnd || isEndOfCycle) {
                        _nodesWithEndFlag++; 
                    }
                    upSum.Add(n);
                }
                return upSum;
            }

            protected override List<PathNode<TItem, TDependency>> AfterVisitingSuccessors(bool visitSuccessors, TItem tail,
                    Stack<TDependency> currentPath, int expectedPathMatchIndex, List<PathNode<TItem, TDependency>> upSum) {
                if (visitSuccessors) {
                    // We visit the children - tail was therefore definitely not in _onPath - so we can safely remove it!
                    var key = new ItemAndInt(tail, expectedPathMatchIndex);
                    _onPath.Remove(key);
                }
                return upSum;
            }
        }

        private class PathNodesTraverser {
            [NotNull]
            private readonly string _pathMarker;
            private readonly string _addIndexToMarkerFormat;
            [NotNull]
            private readonly Action _checkAbort;
            private int _pathCount;
            private readonly HashSet<Dependency> _pathDependencies = new HashSet<Dependency>();

            public PathNodesTraverser([NotNull] string pathMarker, string addIndexToMarkerFormat, [NotNull] Action checkAbort) {
                _pathMarker = pathMarker;
                _addIndexToMarkerFormat = addIndexToMarkerFormat;
                _checkAbort = checkAbort;
            }

            public IEnumerable<Dependency> PathDependencies => _pathDependencies;

            public void Traverse(Dictionary<Item, List<PathNode<Item, Dependency>>> paths, Dictionary<Item, int> counts) {
                foreach (var head in paths.Keys.OrderBy(i => i.AsString())) {
                    Traverse(paths, head);
                }
                foreach (var kvp in counts) {
                    kvp.Key.SetMarker(_pathMarker, kvp.Value);
                }
            }

            private void Traverse(Dictionary<Item, List<PathNode<Item, Dependency>>> paths, Item head) {
                var stack = new Stack<PathNode<Item, Dependency>>();
                var pathMarkers = new List<string>();
                foreach (var p in paths[head]) {
                    pathMarkers.AddRange(Traverse(stack, p));
                }
                foreach (var m in pathMarkers) {
                    head.MarkPathElement(m, 0, true, false /*??*/, false /*??*/, false /*??*/);
                }
            }

            private IEnumerable<string> Traverse(Stack<PathNode<Item, Dependency>> stack, [NotNull] PathNode<Item, Dependency> node) {
                _checkAbort();

                var result = new List<string>();
                stack.Push(node);
                if (node.Children.Any()) {
                    foreach (var child in node.Children) {
                        result.AddRange(Traverse(stack, child));
                    }
                } else {
                    string pathMarker = _addIndexToMarkerFormat != null ? _pathMarker + _pathCount.ToString(_addIndexToMarkerFormat) : _pathMarker;
                    PathNode<Item, Dependency>[] pathAsArray = stack.Reverse().ToArray();
                    pathAsArray[0].Dependency.UsingItem.MarkPathElement(pathMarker, 0, isStart: true, isEnd: false, isMatchedByCountMatch: false, isLoopBack: false);
                    for (var i = 0; i < pathAsArray.Length; i++) {
                        PathNode<Item, Dependency> pathNode = pathAsArray[i];
                        Dependency dependency = pathNode.Dependency;
                        dependency.MarkPathElement(pathMarker, i, isStart: i == 0, isEnd: pathNode.IsEnd,
                            isMatchedByCountMatch: pathNode.DependencyMatchedByCountMatch || pathNode.UsedItemMatchedByCountMatch, isLoopBack: pathNode.IsEndOfCycle);
                        _pathDependencies.Add(dependency);
                    }
                    result.Add(pathMarker);
                    _pathCount++;
                }
                stack.Pop();
                return result;
            }

            public int PathCount => _pathCount;
        }

        public static readonly Option PathItemAnchorOption = new Option("pi", "path-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option PathDependencyAnchorOption = new Option("pd", "path-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly Option CountItemAnchorOption = new Option("ci", "count-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option CountDependencyAnchorOption = new Option("cd", "count-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly Option MultipleItemAnchorOption = new Option("mi", "multiple-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option MultipleDependencyAnchorOption = new Option("md", "multiple-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly Option NoSuchItemAnchorOption = new Option("ni", "no-such-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option NoSuchDependencyAnchorOption = new Option("nd", "no-such-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);
        public static readonly Option BackwardsOption = new Option("bw", "upwards", "", "Traverses dependencies in opposite direction", @default: false);
        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "add path marker to all dependencies on path &", @default: "_");
        public static readonly Option AddIndexedMarkerOption = new Option("im", "indexed-marker", "&", "add separate path markers starting with &", @default: "");
        public static readonly Option KeepOnlyCyclesOption = new Option("kp", "keep-only-paths", "", "remove all dependencies not on matched paths", @default: false);
        public static readonly Option MaxPathLengthOption = new Option("ml", "max-length", "#", "maximum length of path found", @default: "twice the number of provided anchors");

        private static readonly Option[] _allOptions = {
            PathItemAnchorOption, PathDependencyAnchorOption,
            CountItemAnchorOption, CountDependencyAnchorOption,
            MultipleItemAnchorOption, MultipleDependencyAnchorOption,
            NoSuchItemAnchorOption, NoSuchDependencyAnchorOption,
            NoExampleInfoOption, BackwardsOption, AddMarkerOption,
            AddIndexedMarkerOption
        };

        private bool _ignoreCase;

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, string transformOptions,
            [NotNull] List<Dependency> transformedDependencies) {
            bool backwards = false;
            //int maxPathLength = int.MaxValue;
            ItemMatch pathAnchor = null;
            bool pathAnchorIsCountMatch = false;
            var expectedPathMatches = new List<AbstractPathMatch<Dependency, Item>>();
            var dontMatches = new List<AbstractPathMatch<Dependency, Item>>();
            AbstractPathMatch<Dependency, Item> countMatch = null;
            string marker = "_";
            bool addIndex = false;
            bool keepOnlyPathEdges = false;
            int? maxPathLength = null;

            Option.Parse(globalContext, transformOptions,
                BackwardsOption.Action((args, j) => {
                    backwards = true;
                    return j;
                }),
                PathItemAnchorOption.Action((args, j) => {
                    if (pathAnchor == null) {
                        pathAnchor = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item pattern"), _ignoreCase);
                    } else {
                        expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
                    }
                    return j;
                }),
                PathDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
                    return j;
                }),
                CountItemAnchorOption.Action((args, j) => {
                    if (pathAnchor == null) {
                        pathAnchor = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item pattern"), _ignoreCase);
                        pathAnchorIsCountMatch = true;
                    } else {
                        expectedPathMatches.Add(countMatch = CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
                    }
                    return j;
                }),
                CountDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(countMatch = CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
                    return j;
                }),
                MultipleItemAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: true, mayContinue: true, dontMatches: dontMatches));
                    return j;
                }),
                MultipleDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: true, mayContinue: true, dontMatches: dontMatches));
                    return j;
                }),
                NoSuchItemAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    dontMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false, dontMatches: new AbstractPathMatch<Dependency, Item>[0]));
                    return j;
                }),
                NoSuchDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    dontMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false, dontMatches: new AbstractPathMatch<Dependency, Item>[0]));
                    return j;
                }),
                AddMarkerOption.Action((args, j) => {
                    marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    return j;
                }),
                AddIndexedMarkerOption.Action((args, j) => {
                    marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    addIndex = true;
                    return j;
                }),
                KeepOnlyCyclesOption.Action((args, j) => {
                    keepOnlyPathEdges = true;
                    return j;
                }),
                MaxPathLengthOption.Action((args, j) => {
                    maxPathLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum path length");
                    return j;
                })
            );
            AbstractPathMatch<Dependency, Item>[] expectedPathMatchesArray = expectedPathMatches.ToArray();
            var c = new PathWriterTraverser<Dependency, Item>(maxPathLength ?? 1 + expectedPathMatchesArray.Length * 2, backwards, countMatch, globalContext.CheckAbort);
            c.Traverse(dependencies, pathAnchor, pathAnchorIsCountMatch, expectedPathMatchesArray);

            string addIndexToMarkerFormat = addIndex ? "D" + ("" + c.NodesWithEndFlag).Length : null;

            var t = new PathNodesTraverser(pathMarker: marker, addIndexToMarkerFormat: addIndexToMarkerFormat, checkAbort: globalContext.CheckAbort);
            t.Traverse(c.Paths, c.Counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count));

            Log.WriteInfo($"... marked {t.PathCount} paths");

            transformedDependencies.AddRange(keepOnlyPathEdges ? t.PathDependencies : dependencies);

            return Program.OK_RESULT;
        }

        // ReSharper disable once UnusedParameter.Local -- method is just a precondition check
        private static void CheckPathAnchorSet(ItemMatch pathAnchor) {
            if (pathAnchor == null) {
                throw new ArgumentException($"First path pattern must be specified with {PathItemAnchorOption}");
            }
        }

        private static DependencyPathMatch<Dependency, Item> CreateDependencyPathMatch([NotNull] GlobalContext globalContext, string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue, IEnumerable<AbstractPathMatch<Dependency, Item>> dontMatches) {
            return new DependencyPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed, mayContinue: mayContinue, dontMatches: dontMatches);
        }

        private static ItemPathMatch<Dependency, Item> CreateItemPathMatch([NotNull] GlobalContext globalContext, string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue, IEnumerable<AbstractPathMatch<Dependency, Item>> dontMatches) {
            return new ItemPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed, mayContinue: mayContinue, dontMatches: dontMatches);
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
    $@"  Writes dependencies to .dip files, which can be read in by 
  NDepCheck's DipReader. This is very helpful for building pipelines 
  that process dependencies for different purposes.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var b = Item.New(t3, "b:bb:bbb".Split(':'));
            var c = Item.New(t3, "c:cc:ccc".Split(':'));
            var d = Item.New(t3, "d:dd:ddd".Split(':'));
            var e = Item.New(t3, "e:ee:eee".Split(':'));
            var f = Item.New(t3, "f:ff:fff".Split(':'));
            var g = Item.New(t3, "g:gg:ggg".Split(':'));
            var h = Item.New(t3, "h:hh:hhh".Split(':'));

            return new[] {
                FromTo(a, b),
                FromTo(a, h),
                FromTo(b, c),
                FromTo(c, d),
                FromTo(d, e),
                FromTo(d, b),
                FromTo(e, f),
                FromTo(b, g),
                FromTo(h, g),
            };
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
            return new Dependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }
    }
}
