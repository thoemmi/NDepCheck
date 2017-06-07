using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.PathFinding {
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
            public bool LeadsToEndNodes {
                get;
            }
            public readonly TDependency Dependency;
            public readonly bool IsEndOfCycle;
            public readonly bool DependencyMatchedByCountMatch;
            public readonly bool UsedItemMatchedByCountMatch;

            [NotNull, ItemNotNull]
            public readonly IEnumerable<PathNode<TItem, TDependency>> Children;

            public PathNode(TDependency dependency, bool isEnd, bool isEndOfCycle,
                            bool dependencyMatchedByCountMatch, bool usedItemMatchedByCountMatch, 
                            IEnumerable<PathNode<TItem, TDependency>> children) {
                Dependency = dependency;
                IsEnd = isEnd;
                IsEndOfCycle = isEndOfCycle;
                DependencyMatchedByCountMatch = dependencyMatchedByCountMatch;
                UsedItemMatchedByCountMatch = usedItemMatchedByCountMatch;
                Children = children ?? Enumerable.Empty<PathNode<TItem, TDependency>>();
                LeadsToEndNodes = isEndOfCycle || isEnd || Children.Any(ch => ch.LeadsToEndNodes);
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

                TItem[] uniqueStartItems = (pathAnchor == null
                    ? incidentDependencies.Keys
                    : incidentDependencies.Keys.Where(i => pathAnchor.Matches(i).Success)).ToArray();
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

                if (isEnd || isEndOfCycle || childUp.Any(p => p.LeadsToEndNodes)) {
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

        public static PathOptions PathOptions = new PathOptions();

        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "add path marker to all dependencies on path &", @default: "_");
        public static readonly Option AddIndexedMarkerOption = new Option("im", "indexed-marker", "&", "add separate path markers starting with &", @default: "");
        public static readonly Option KeepOnlyPathsOption = new Option("kp", "keep-only-paths", "", "remove all dependencies not on matched paths", @default: false);

        private static readonly Option[] _allOptions = PathOptions.WithOptions(AddMarkerOption, AddIndexedMarkerOption, KeepOnlyPathsOption);
        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            // empty
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            string transformOptions, [NotNull] List<Dependency> transformedDependencies, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            bool backwards;
            ItemMatch pathAnchor;
            bool pathAnchorIsCountMatch;
            var expectedPathMatches = new List<AbstractPathMatch<Dependency, Item>>();
            AbstractPathMatch<Dependency, Item> countMatch;
            string marker = "_";
            bool addIndex = false;
            bool keepOnlyPathEdges = false;
            int? maxPathLength;

            PathOptions.Parse(globalContext, transformOptions, out backwards, out pathAnchor, out pathAnchorIsCountMatch,
                expectedPathMatches, out countMatch, out maxPathLength,
                AddMarkerOption.Action((args, j) => {
                    marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    return j;
                }),
                AddIndexedMarkerOption.Action((args, j) => {
                    marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    addIndex = true;
                    return j;
                }),
                KeepOnlyPathsOption.Action((args, j) => {
                    keepOnlyPathEdges = true;
                    return j;
                })
            );
            AbstractPathMatch<Dependency, Item>[] expectedPathMatchesArray = expectedPathMatches.ToArray();
            var c = new PathWriterTraverser<Dependency, Item>(maxPathLength ?? 1 + expectedPathMatchesArray.Length * 2,
                                                              backwards, countMatch, globalContext.CheckAbort);
            c.Traverse(dependencies, pathAnchor, pathAnchorIsCountMatch, expectedPathMatchesArray);

            string addIndexToMarkerFormat = addIndex ? "D" + ("" + c.NodesWithEndFlag).Length : null;

            var t = new PathNodesTraverser(pathMarker: marker, addIndexToMarkerFormat: addIndexToMarkerFormat,
                                           checkAbort: globalContext.CheckAbort);
            t.Traverse(c.Paths, c.Counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count));

            Log.WriteInfo($"... marked {t.PathCount} paths");

            transformedDependencies.AddRange(keepOnlyPathEdges ? t.PathDependencies : dependencies);

            return Program.OK_RESULT;
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
    $@"  Writes dependencies to .dip files, which can be read in by 
  NDepCheck's DipReader. This is very helpful for building pipelines 
  that process dependencies for different purposes.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = transformingGraph.CreateItem(t3, "a:aa:aaa".Split(':'));
            var b = transformingGraph.CreateItem(t3, "b:bb:bbb".Split(':'));
            var c = transformingGraph.CreateItem(t3, "c:cc:ccc".Split(':'));
            var d = transformingGraph.CreateItem(t3, "d:dd:ddd".Split(':'));
            var e = transformingGraph.CreateItem(t3, "e:ee:eee".Split(':'));
            var f = transformingGraph.CreateItem(t3, "f:ff:fff".Split(':'));
            var g = transformingGraph.CreateItem(t3, "g:gg:ggg".Split(':'));
            var h = transformingGraph.CreateItem(t3, "h:hh:hhh".Split(':'));

            return new[] {
                FromTo(transformingGraph, a, b),
                FromTo(transformingGraph, a, h),
                FromTo(transformingGraph, b, c),
                FromTo(transformingGraph, c, d),
                FromTo(transformingGraph, d, e),
                FromTo(transformingGraph, d, b),
                FromTo(transformingGraph, e, f),
                FromTo(transformingGraph, b, g),
                FromTo(transformingGraph, h, g),
            };
        }

        private Dependency FromTo(WorkingGraph transformingGraph, Item from, Item to, int ct = 1, int questionable = 0) {
            return transformingGraph.CreateDependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }
    }
}
