using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;
using NDepCheck.PathMatching;

namespace NDepCheck.Transforming.PathFinding {
    public class PathMarker : TransformerWithOptions<Ignore, PathMarker.TransformOptions> {
        public class TransformOptions {
            public bool Backwards;
            [NotNull]
            public string Marker = "_";
            public bool AddIndex;
            public bool KeepOnlyPathEdges;
            public bool IgnorePrefixPaths;
            public bool IgnoreSubpaths;
            [CanBeNull]
            public int? MaxPathLength;
            public bool AddTransitiveDependencyInsteadOfMarking;
            [NotNull]
            public Dictionary<string, DependencyMatch> DefinedDependencyMatches = new Dictionary<string, DependencyMatch>();
            [NotNull]
            public Dictionary<string, ItemMatch> DefinedItemMatches = new Dictionary<string, ItemMatch>();
            public string Regex;
        }

        private class PathNode {
            [CanBeNull]
            private readonly PathNode _previous;
            [NotNull]
            public readonly Dependency Dependency;
            public readonly bool IsEnd;
            public readonly bool IsMatchedByCountSymbol;
            public readonly bool IsEndOfCycle;
            public readonly int PathLength;
            public readonly long PathHash;

            public PathNode([CanBeNull] PathNode previous, [NotNull] Dependency dependency, bool isEnd,
                bool isMatchedByCountSymbol, bool isEndOfCycle) {
                _previous = previous;
                Dependency = dependency;
                IsEnd = isEnd;
                IsMatchedByCountSymbol = isMatchedByCountSymbol;
                IsEndOfCycle = isEndOfCycle;
                PathLength = previous == null ? 1 : previous.PathLength + 1;
                PathHash = (1L << (dependency.GetHashCode() % 63)) | (previous == null ? 0L : previous.PathHash);
            }

            public IEnumerable<PathNode> PathNodes {
                get {
                    if (_previous != null) {
                        foreach (var p in _previous.PathNodes) {
                            yield return p;
                        }
                    }
                    yield return this;
                }
            }
        }

        private class PathInfo {
            [NotNull]
            public readonly Item Root;
            public readonly bool RootIsCounted;
            [CanBeNull]
            public readonly PathNode Tail;
            [CanBeNull]
            public readonly IMatchableObject CountedObject;
            private PathNode[] _pathAsArray;

            public PathInfo([NotNull] Item root, bool rootIsCounted, [CanBeNull] IMatchableObject countedObject,
                [CanBeNull] PathNode tail) {
                Root = root;
                RootIsCounted = rootIsCounted;
                CountedObject = countedObject;
                Tail = tail;
            }

            public IEnumerable<PathNode> PathNodes => Tail == null ? Enumerable.Empty<PathNode>() : Tail.PathNodes;

            public PathNode[] AsArray => _pathAsArray ?? (_pathAsArray = PathNodes.ToArray());

            public int PathLength => Tail?.PathLength ?? 0;
            public long PathHash => Tail?.PathHash ?? 0;
        }

        private struct UpInfo {
            public readonly bool FoundPathBelow;

            public UpInfo(bool foundPathBelow) {
                FoundPathBelow = foundPathBelow;
            }
        }

        private class RegexPathMarkerTraverser :
            AbstractRegexDepthFirstPathTraverser<Dependency, Item, PathInfo, PathInfo, UpInfo, Item> {
            private readonly int _maxPathLength;
            private readonly Dictionary<Item, int> _onPath = new Dictionary<Item, int>();
            private readonly bool _backwards;
            private readonly List<PathInfo> _foundPaths;
            private readonly Dictionary<Item, HashSet<IMatchableObject>> _countedObjectsPerItem;
            private readonly bool _ignorePrefixPaths;

            public RegexPathMarkerTraverser([NotNull] PathRegex<Item, Dependency> regex, int maxPathLength,
                bool backwards, bool ignorePrefixPaths, [NotNull] Action checkAbort,
                List<PathInfo> foundPaths, Dictionary<Item, HashSet<IMatchableObject>> countedObjectsPerItem)
                : base(regex, checkAbort, maxRecursionDepth: maxPathLength) {
                _maxPathLength = maxPathLength;
                _backwards = backwards;
                _foundPaths = foundPaths;
                _countedObjectsPerItem = countedObjectsPerItem;
                _ignorePrefixPaths = ignorePrefixPaths;
            }

            public void Traverse(IEnumerable<Dependency> dependencies) {
                Dictionary<Item, Dependency[]> incidentDependencies = _backwards
                    ? Item.CollectIncomingDependenciesMap(dependencies)
                    : Item.CollectOutgoingDependenciesMap(dependencies);
                Item[] uniqueStarItems = incidentDependencies.Keys.ToArray();
                foreach (var item in uniqueStarItems.OrderBy(i => i.Name)) {
                    _onPath.Clear();
                    _onPath.Add(item, 1);
                    Traverse(root: item, incidentDependencies: incidentDependencies,
                        down: (root, rootIsCounted) => new PathInfo(root, rootIsCounted, rootIsCounted ? root : null, null));
                }
            }

            protected override bool ShouldVisitSuccessors(Item tail, Stack<Dependency> currentPath, out UpInfo initUpSum) {
                initUpSum = new UpInfo(false);
                _onPath.Increment(tail);
                return currentPath.Count <= _maxPathLength;
            }

            protected override Item CreateFirstStateElement(
                IBeforeDependencyGraphkenState<Item, Dependency, ItemMatch, DependencyMatch> beforeNextDependencyState, Item root) {
                return root;
            }

            protected override Item CreateStateElement(
                IBeforeDependencyGraphkenState<Item, Dependency, ItemMatch, DependencyMatch> beforeNextDependencyState, Dependency nextDep) {
                return nextDep.UsedItem;
            }

            protected override DownAndHere AfterPushDependency(Stack<Dependency> currentPath, bool isEnd,
                bool isLoopBack, CountedEnum counted, PathInfo down) {
                Dependency top = currentPath.Peek();
                PathNode pathNode = new PathNode(down.Tail, top, isEnd,
                    isMatchedByCountSymbol: counted != CountedEnum.NotCounted, isEndOfCycle: isLoopBack);
                PathInfo downInfo = new PathInfo(down.Root, down.RootIsCounted,
                    counted == CountedEnum.DependencyCounted ? top :
                    counted == CountedEnum.UsedItemCounted ? top.UsedItem : down.CountedObject, pathNode);
                return new DownAndHere(downInfo, downInfo);
            }

            protected override UpInfo BeforePopDependency(Stack<Dependency> currentPath, bool isEnd, bool isLoopBack,
                CountedEnum counted, PathInfo down, PathInfo here, UpInfo upSum, UpInfo childUp) {
                Dependency top = currentPath.Peek();

                bool isEndOfCycle = _onPath.Get(top.UsedItem) > 0;

                if (childUp.FoundPathBelow || isEnd || isEndOfCycle) {
                    _countedObjectsPerItem
                        .GetOrAdd(top.UsedItem, () => new HashSet<IMatchableObject>())
                        .Add(down.CountedObject);
                }

                if (isEnd || isEndOfCycle) {
                    if (_ignorePrefixPaths && childUp.FoundPathBelow) {
                        // don't add this prefix path to list of found paths; however, the node will
                        // be reached and have its correct end flag
                    } else {
                        _foundPaths.Add(here);
                    }
                    return new UpInfo(true);
                } else {
                    return new UpInfo(upSum.FoundPathBelow | childUp.FoundPathBelow);
                }
            }

            protected override UpInfo AfterVisitingSuccessors(bool visitSuccessors, Item tail,
                Stack<Dependency> currentPath, UpInfo upSum) {
                if (visitSuccessors) {
                    _onPath[tail]--;
                }
                return upSum;
            }
        }

        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&",
            "add path marker to all dependencies on path &", @default: "_");

        public static readonly Option AddIndexedMarkerOption = new Option("im", "indexed-marker", "&",
            "add separate path markers starting with &", @default: "");

        public static readonly Option AddDependencyOption = new Option("ad", "add-dependency", "&",
            "add a dependency instead of marking all dependencies along the path", @default: false);

        public static readonly Option KeepOnlyPathsOption = new Option("kp", "keep-only-paths", "",
            "remove all dependencies not on matched paths", @default: false);

        public static readonly Option IgnorePrefixPathsOption = new Option("ip", "ignore-prefix-paths", "",
            "ignore paths that are the prefix (start) of anotehr path", @default: false);

        public static readonly Option IgnoreSubpathsOption = new Option("is", "ignore-subpaths", "",
            "ignore paths that are contained in another (longer) path", @default: false);

        public static readonly Option DefineDependencyMatchOption = new Option("dd", "define-dependency-match",
            "name pattern", "define short name for a dependency match", @default: "", multiple: true);

        public static readonly Option DefineItemMatchOption = new Option("di", "define-item-match", "name pattern",
            "define short name for an item match", @default: "", multiple: true);

        public static readonly Option RegexOption = new Option("pr", "path-regex", "pattern",
            "Define regular expression for paths to be found", @default: ":(.:)+", multiple: true);

        public static readonly Option BackwardsOption = new Option("bw", "upwards", "",
            "Traverses dependencies in opposite direction", @default: false);

        public static readonly Option MaxPathLengthOption = new Option("ml", "max-length", "#",
            "maximum length of path found", @default: "twice the number of provided anchors");

        private static readonly Option[] _allOptions = {
            AddIndexedMarkerOption, AddDependencyOption, AddMarkerOption,
            KeepOnlyPathsOption, IgnorePrefixPathsOption, IgnoreSubpathsOption, DefineDependencyMatchOption, DefineItemMatchOption,
            BackwardsOption, MaxPathLengthOption
        };

        protected override Ignore CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            return Ignore.Om;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var transformOptions = new TransformOptions();

            Option.Parse(globalContext, transformOptionsString, BackwardsOption.Action((args, j) => {
                transformOptions.Backwards = true;
                return j;
            }), MaxPathLengthOption.Action((args, j) => {
                transformOptions.MaxPathLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum path length");
                return j;
            }), AddMarkerOption.Action((args, j) => {
                transformOptions.Marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                return j;
            }), AddIndexedMarkerOption.Action((args, j) => {
                transformOptions.Marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                transformOptions.AddIndex = true;
                return j;
            }), KeepOnlyPathsOption.Action((args, j) => {
                transformOptions.KeepOnlyPathEdges = true;
                return j;
            }), IgnorePrefixPathsOption.Action((args, j) => {
                transformOptions.IgnorePrefixPaths = true;
                return j;
            }), IgnoreSubpathsOption.Action((args, j) => {
                transformOptions.IgnoreSubpaths = true;
                return j;
            }), AddDependencyOption.Action((args, j) => {
                transformOptions.AddTransitiveDependencyInsteadOfMarking = true;
                return j;
            }), DefineDependencyMatchOption.Action((args, j) => {
                string name = Option.ExtractRequiredOptionValue(args, ref j, "missing name");
                string pattern = Option.ExtractNextRequiredValue(args, ref j, "missing pattern");
                transformOptions.DefinedDependencyMatches[name] = DependencyMatch.Create(pattern, globalContext.IgnoreCase);
                return j;
            }), DefineItemMatchOption.Action((args, j) => {
                string name = Option.ExtractRequiredOptionValue(args, ref j, "missing name");
                string pattern = Option.ExtractNextRequiredValue(args, ref j, "missing pattern");
                transformOptions.DefinedItemMatches[name] = new ItemMatch(pattern, globalContext.IgnoreCase, anyWhereMatcherOk: true);
                return j;
            }), RegexOption.Action((args, j) => {
                transformOptions.Regex = Option.ExtractRequiredOptionValue(args, ref j, "missing regex");
                return j;
            }));

            return transformOptions;
        }

        public override int Transform([NotNull] GlobalContext globalContext, Ignore Ignore,
            [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, 
            [NotNull] List<Dependency> transformedDependencies) {

            var foundPaths = new List<PathInfo>();
            string regex = transformOptions.Regex ?? ":(.:)+";
            var pathRegex = new PathRegex<Item, Dependency>(regex, transformOptions.DefinedItemMatches,
                transformOptions.DefinedDependencyMatches, globalContext.IgnoreCase);
            var countedObjectsPerItem = new Dictionary<Item, HashSet<IMatchableObject>>();
            var traverser = new RegexPathMarkerTraverser(pathRegex,
                transformOptions.MaxPathLength ?? (pathRegex.ContainsLoops ? 1000 : regex.Length),
                transformOptions.Backwards, transformOptions.IgnorePrefixPaths,
                globalContext.CheckAbort, foundPaths, countedObjectsPerItem);
            traverser.Traverse(dependencies);

            Log.WriteInfo($"... marked {foundPaths.Count} paths");

            if (pathRegex.ContainsCountSymbol) {
                foreach (var kvp in countedObjectsPerItem) {
                    kvp.Value.Remove(null);
                    kvp.Key.SetMarker(transformOptions.Marker, kvp.Value.Count);
                }
            }

            if (transformOptions.IgnoreSubpaths) {
                foundPaths.Sort((f1, f2) => f2.PathLength - f1.PathLength);
                var longPaths = new List<PathInfo>();
                foreach (var f in foundPaths) {
                    if (longPaths.All(lp => (lp.PathHash & f.PathHash) == f.PathHash && lp.AsArray.IndexOf(f.AsArray) < 0)) {
                        longPaths.Add(f);
                    }
                }
                foundPaths = longPaths;
            }

            if (transformOptions.AddTransitiveDependencyInsteadOfMarking) {
                var transitiveEdges = new HashSet<Dependency>();
                foreach (var f in foundPaths) {
                    if (f.Tail == null) {
                        throw new Exception("Internal error - found path of length zero");
                    }

                    IEnumerable<Dependency> path = f.PathNodes.Select(n => n.Dependency);
                    transitiveEdges.Add(globalContext.CurrentGraph.CreateDependency(f.Root, f.Tail.Dependency.UsedItem,
                        source: null, markers: transformOptions.Marker, ct: path.Max(p => p.Ct),
                        questionableCt: path.Max(d => d.QuestionableCt), badCt: path.Max(d => d.BadCt),
                        notOkReason: path.Select(d => d.NotOkReason).FirstOrDefault(),
                        exampleInfo: path.Select(d => d.ExampleInfo).FirstOrDefault()));
                }
                transformedDependencies.AddRange(transformOptions.KeepOnlyPathEdges
                    ? transitiveEdges
                    : dependencies.Concat(transitiveEdges));
            } else {
                int n = 0;
                string addIndexToMarkerFormat = transformOptions.AddIndex ? "D" + ("" + foundPaths.Count).Length : null;
                var pathDependencies = new HashSet<Dependency>();
                foreach (var f in foundPaths) {
                    string m = transformOptions.Marker + (addIndexToMarkerFormat == null ? "" : n++.ToString(addIndexToMarkerFormat));
                    int i = 0;
                    foreach (var node in f.PathNodes) {
                        pathDependencies.Add(node.Dependency);
                        node.Dependency.MarkPathElement(m, posInPath: i, isStart: i == 0, isEnd: node.IsEnd,
                            isMatchedByCountSymbol: node.IsMatchedByCountSymbol, isLoopBack: node.IsEndOfCycle);
                        i++;
                    }
                    f.Root.MarkPathElement(m, 0, isStart: true, isEnd: f.Root.Equals(f.Tail?.Dependency.UsedItem),
                        isMatchedByCountSymbol: f.RootIsCounted, isLoopBack: false /*??*/);
                }

                transformedDependencies.AddRange(transformOptions.KeepOnlyPathEdges ? pathDependencies : dependencies);
            }
            return Program.OK_RESULT;
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            var result = $@"  Marks paths in dependency graph.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
            if (detailedHelp) {
                result += $@"

Path regular expressions:
{PathRegex.HELP}

Path marking:
{PathSupport.HELP}
";
            }
            return result;
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

            //      +---------+
            //      v         |
            // a -> b -> c -> d -> e -> f
            // |    |
            // v    v
            // h -> g
            return new[] {
                FromTo(transformingGraph, a, b), FromTo(transformingGraph, a, h), FromTo(transformingGraph, b, c),
                FromTo(transformingGraph, c, d), FromTo(transformingGraph, d, e), FromTo(transformingGraph, d, b),
                FromTo(transformingGraph, e, f), FromTo(transformingGraph, b, g), FromTo(transformingGraph, h, g),
            };
        }

        private Dependency FromTo(WorkingGraph transformingGraph, Item from, Item to, int ct = 1, int questionable = 0) {
            return transformingGraph.CreateDependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct,
                questionableCt: questionable);
        }
    }
}