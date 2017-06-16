using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.PathFinding {
    public class OLDAddPathDeps : TransformerWithOptions<Ignore, OLDAddPathDeps.TransformOptions> {
        public class TransformOptions {
            public bool Backwards;
            [CanBeNull]
            public ItemMatch PathAnchor;
            [NotNull, ItemNotNull]
            public List<AbstractPathMatch<Dependency, Item>> ExpectedPathMatches = new List<AbstractPathMatch<Dependency, Item>>();
            [CanBeNull]
            public string Marker;
            public bool PathAnchorIsCountMatch;
            [CanBeNull]
            public AbstractPathMatch<Dependency, Item> CountMatch;
            [CanBeNull]
            public int? MaxPathLength;
        }

        private struct DownInfo {
            public readonly IMatchableObject BehindCountMatch;

            public DownInfo(IMatchableObject behindCountMatch) {
                BehindCountMatch = behindCountMatch;
            }
        }

        private class UpInfo<TItem> where TItem : AbstractItem<TItem> {
            public readonly ISet<TItem> EndItems = new HashSet<TItem>();

            public void UnionWith(UpInfo<TItem> childUp) {
                EndItems.UnionWith(childUp.EndItems);
            }

            public void Add(TItem topUsedItem) {
                EndItems.Add(topUsedItem);
            }
        }

        private class AddPathDepsTraverser<TDependency, TItem> : AbstractDepthFirstPathTraverser<TDependency, TItem,
            DownInfo, Ignore, UpInfo<TItem>>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly int _maxPathLength;
            private readonly AbstractPathMatch<TDependency, TItem> _countMatch;
            private readonly HashSet<TItem> _seenInnerPathStarts = new HashSet<TItem>();

            private readonly Dictionary<TItem, HashSet<IMatchableObject>> Counts = new Dictionary<TItem, HashSet<IMatchableObject>>();

            private readonly HashSet<ItemAndInt> _onPath = new HashSet<ItemAndInt>();
            private readonly bool _backwards;

            public AddPathDepsTraverser(int maxPathLength, bool backwards, AbstractPathMatch<TDependency, TItem> countMatch,
                [NotNull] Action checkAbort) : base(checkAbort) {
                _maxPathLength = maxPathLength;
                _backwards = backwards;
                _countMatch = countMatch;
            }

            public List<FromTo<TItem, TDependency>> Traverse(IEnumerable<TDependency> dependencies, ItemMatch pathAnchor,
                bool pathAnchorIsCountMatch, AbstractPathMatch<TDependency, TItem>[] expectedPathMatches) {
                Dictionary<TItem, TDependency[]> incidentDependencies = _backwards
                    ? AbstractItem<TItem>.CollectIncomingDependenciesMap(dependencies)
                    : AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);
                _seenInnerPathStarts.Clear();
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

                var result = new List<FromTo<TItem, TDependency>>();

                foreach (var item in uniqueStartItems.OrderBy(i => i.Name)) {
                    if (_seenInnerPathStarts.Add(item)) {
                        UpInfo<TItem> up = Traverse(root: item,
                            incidentDependencies: incidentDependencies, expectedInnerPathMatches: innerMatches,
                            endMatch: endMatch, down: new DownInfo(pathAnchorIsCountMatch ? item : null));
                        result.AddRange(up.EndItems.Select(i => new FromTo<TItem, TDependency>(item, i)));
                    }
                }
                return result;
            }

            protected override bool ShouldVisitSuccessors(TItem tail, Stack<TDependency> currentPath,
                    int expectedPathMatchIndex, out UpInfo<TItem> initUpSum) {
                initUpSum = new UpInfo<TItem>();
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
                if (down.BehindCountMatch != null) {
                    pushDownMatch = down.BehindCountMatch;
                } else if (_countMatch == null) {
                    // default values are ok
                } else if (_countMatch == pathMatchOrNull) {
                    if (_countMatch is DependencyPathMatch<TDependency, TItem>) {
                        pushDownMatch = tailDependency;
                    } else {
                        pushDownMatch = usedItem;
                    }
                } else {
                    // default values are ok
                }
                return new DownAndHere(new DownInfo(pushDownMatch), Ignore.Om);
            }

            protected override UpInfo<TItem> BeforePopDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                    AbstractPathMatch<TDependency, TItem> pathMatchOrNull, bool isEnd,
                    Ignore here, UpInfo<TItem> upSum, UpInfo<TItem> childUp) {

                upSum.UnionWith(childUp);

                TDependency top = currentPath.Peek();
                var key = new ItemAndInt(top.UsedItem, expectedPathMatchIndex);
                bool isEndOfCycle = _onPath.Contains(key);

                if (isEnd || isEndOfCycle) {
                    upSum.Add(top.UsedItem);
                }
                return upSum;
            }

            protected override UpInfo<TItem> AfterVisitingSuccessors(bool visitSuccessors, TItem tail,
                    Stack<TDependency> currentPath, int expectedPathMatchIndex, UpInfo<TItem> upSum) {
                if (visitSuccessors) {
                    // We visit the children - tail was therefore definitely not in _onPath - so we can safely remove it!
                    var key = new ItemAndInt(tail, expectedPathMatchIndex);
                    _onPath.Remove(key);
                }
                return upSum;
            }
        }

        public static PathOptions PathOptions = new PathOptions();

        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "add path marker to all dependencies on path &", @default: "_");
        public static readonly Option KeepOnlyPathsOption = new Option("kp", "keep-only-paths", "", "remove all dependencies not on matched paths", @default: false);

        private static readonly Option[] _allOptions = PathOptions.WithOptions(AddMarkerOption, KeepOnlyPathsOption);

        protected override Ignore CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            return Ignore.Om;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext, 
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var transformOptions = new TransformOptions();

            PathOptions.Parse(globalContext, transformOptionsString, out transformOptions.Backwards,
                out transformOptions.PathAnchor, out transformOptions.PathAnchorIsCountMatch,
                transformOptions.ExpectedPathMatches, out transformOptions.CountMatch, out transformOptions.MaxPathLength,
                AddMarkerOption.Action((args, j) => {
                    transformOptions.Marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    return j;
                })
            ////KeepOnlyPathsOption.Action((args, j) => {
            ////    keepOnlyPathEdges = true;
            ////    return j;
            ////})
            );
            return transformOptions;
        }

        public override int Transform([NotNull] GlobalContext globalContext, Ignore Ignore, 
            [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            [NotNull] List<Dependency> transformedDependencies) {
            AbstractPathMatch<Dependency, Item>[] expectedPathMatchesArray = transformOptions.ExpectedPathMatches.ToArray();
            var c = new AddPathDepsTraverser<Dependency, Item>(
                transformOptions.MaxPathLength ?? 1 + expectedPathMatchesArray.Length * 2,
                transformOptions.Backwards, transformOptions.CountMatch, globalContext.CheckAbort);
            List<FromTo<Item, Dependency>> fromTos = c.Traverse(dependencies, transformOptions.PathAnchor, transformOptions.PathAnchorIsCountMatch, expectedPathMatchesArray);

            transformedDependencies.AddRange(dependencies);

            foreach (var r in fromTos) {
                transformedDependencies.Add(globalContext.CurrentGraph.CreateDependency(r.From, r.To, null, transformOptions.Marker ?? "", 0 /*TODO: Sum of edges????*/));
            }

            Log.WriteInfo($"... added {fromTos.Count} dependencies");

            return Program.OK_RESULT;
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            return
    $@"  Writes dependencies to .dip files, which can be read in by 
  NDepCheck's DipReader. This is very helpful for building pipelines 
  that process dependencies for different purposes.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
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
