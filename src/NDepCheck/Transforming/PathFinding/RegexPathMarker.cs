using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;
using NDepCheck.PathMatching;

namespace NDepCheck.Transforming.PathFinding {
    public class RegexPathMarker {
        private class RegexPathMarkerTraverser : AbstractRegexDepthFirstPathTraverser<Dependency, Item, Ignore, Ignore, Ignore> {
            private readonly int _maxPathLength;
            private readonly Dictionary<Item, int> _onPath = new Dictionary<Item, int>();
            private readonly bool _backwards;
            private readonly List<RememberedPath> _foundPaths;

            public RegexPathMarkerTraverser(PathRegex<Item, Dependency> regex, int maxPathLength, bool backwards,
                [NotNull] Action checkAbort, List<RememberedPath> foundPaths) : base(regex, checkAbort) {
                _maxPathLength = maxPathLength;
                _backwards = backwards;
                _foundPaths = foundPaths;
            }

            public void Traverse(IEnumerable<Dependency> dependencies) {
                Dictionary<Item, Dependency[]> incidentDependencies = _backwards
                    ? Item.CollectIncomingDependenciesMap(dependencies)
                    : Item.CollectOutgoingDependenciesMap(dependencies);
                Item[] uniqueStarItems = incidentDependencies.Keys.ToArray();
                foreach (var item in uniqueStarItems.OrderBy(i => i.Name)) {
                    _onPath.Clear();
                    _onPath.Add(item, 1);
                    Traverse(root: item, incidentDependencies: incidentDependencies, down: Ignore.Om);
                }
            }

            protected override bool ShouldVisitSuccessors(Item tail, Stack<Dependency> currentPath, out Ignore initUpSum) {
                initUpSum = Ignore.Om;
                int ct;
                if (!_onPath.TryGetValue(tail, out ct)) {
                    _onPath.Add(tail, 1);
                } else {
                    _onPath[tail]++;
                }
                return currentPath.Count <= _maxPathLength;
            }

            protected override DownAndHere AfterPushDependency(Stack<Dependency> currentPath, bool isEnd, Ignore down, object countedObject) {
                return new DownAndHere(Ignore.Om, Ignore.Om);
            }

            protected override Ignore BeforePopDependency(Stack<Dependency> currentPath, bool isEnd, Ignore here,
                Ignore upSum, Ignore childUp, object countedObject) {
                Dependency top = currentPath.Peek();
                int ct;
                _onPath.TryGetValue(top.UsedItem, out ct);
                bool isEndOfCycle = ct > 0;
                if (isEnd || isEndOfCycle) {
                    _foundPaths.Add(new RememberedPath(currentPath.Reverse(), countedObject));
                }
                return Ignore.Om;
            }

            protected override Ignore AfterVisitingSuccessors(bool visitSuccessors, Item tail,
                                                              Stack<Dependency> currentPath, Ignore upSum) {
                if (visitSuccessors) {
                    _onPath[tail]--;
                }
                return upSum;
            }
        }

        private class RememberedPath {
            public readonly Dependency[] Path;
            public readonly object CountedObject;

            public RememberedPath(IEnumerable<Dependency> path, object countedObject) {
                Path = path.ToArray();
                CountedObject = countedObject;
            }

            public Item FirstItem => Path[0].UsingItem;
            public Item LastItem => Path[Path.Length - 1].UsedItem;
        }

        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "add path marker to all dependencies on path &", @default: "_");
        public static readonly Option AddIndexedMarkerOption = new Option("im", "indexed-marker", "&", "add separate path markers starting with &", @default: "");
        public static readonly Option AddDependencyOption = new Option("ad", "add-dependency", "&", "add a dependency instead of marking all dependencies along the path", @default: false);
        public static readonly Option KeepOnlyPathsOption = new Option("kp", "keep-only-paths", "", "remove all dependencies not on matched paths", @default: false);

        public static readonly Option DefineDependencyMatchOption = new Option("dd", "define-dependency-match", "name pattern", "define short name for a dependency match", @default: "", multiple: true);
        public static readonly Option DefineItemMatchOption = new Option("di", "define-item-match", "name pattern", "define short name for an item match", @default: "", multiple: true);
        public static readonly Option RegexOption = new Option("pr", "define-item-match", "name pattern", "define short name for an item match", @default: "", multiple: true);

        public static readonly Option BackwardsOption = new Option("bw", "upwards", "",
            "Traverses dependencies in opposite direction", @default: false);
        public static readonly Option MaxPathLengthOption = new Option("ml", "max-length", "#", "maximum length of path found",
            @default: "twice the number of provided anchors");

        private static readonly Option[] _allOptions = {
            AddIndexedMarkerOption, AddDependencyOption, AddMarkerOption,
            KeepOnlyPathsOption, DefineDependencyMatchOption, DefineItemMatchOption,
            BackwardsOption, MaxPathLengthOption
        };

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            // empty
        }

        public int Transform([NotNull] GlobalContext globalContext,
            [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            string transformOptions, [NotNull] List<Dependency> transformedDependencies,
            Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {

            bool backwards = false;
            string marker = "_";
            bool addIndex = false;
            bool keepOnlyPathEdges = false;
            int? maxPathLength = null;
            bool addTransitiveDependencyInsteadOfMarking = false;
            Dictionary<string, DependencyMatch> definedDependencyMatches = new Dictionary<string, DependencyMatch>();
            Dictionary<string, ItemMatch> definedItemMatches = new Dictionary<string, ItemMatch>();
            string regex = null;

            Option.Parse(globalContext, transformOptions,
                BackwardsOption.Action((args, j) => {
                    backwards = true;
                    return j;
                }),
                MaxPathLengthOption.Action((args, j) => {
                    maxPathLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum path length");
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
                KeepOnlyPathsOption.Action((args, j) => {
                    keepOnlyPathEdges = true;
                    return j;
                }),
                AddDependencyOption.Action((args, j) => {
                    addTransitiveDependencyInsteadOfMarking = true;
                    return j;
                }),
                DefineDependencyMatchOption.Action((args, j) => {
                    string name = Option.ExtractRequiredOptionValue(args, ref j, "missing name");
                    string pattern = Option.ExtractNextRequiredValue(args, ref j, "missing pattern");
                    definedDependencyMatches[name] = DependencyMatch.Create(pattern, globalContext.IgnoreCase);
                    return j;
                }),
                DefineItemMatchOption.Action((args, j) => {
                    string name = Option.ExtractRequiredOptionValue(args, ref j, "missing name");
                    string pattern = Option.ExtractNextRequiredValue(args, ref j, "missing pattern");
                    definedItemMatches[name] = new ItemMatch(pattern, globalContext.IgnoreCase, anyWhereMatcherOk: true);
                    return j;
                }),
                RegexOption.Action((args, j) => {
                    regex = Option.ExtractRequiredOptionValue(args, ref j, "missing regex");
                    return j;
                })
            );

            var foundPaths = new List<RememberedPath>();

            var pathRegex = new PathRegex<Item, Dependency>(regex, definedItemMatches, definedDependencyMatches, globalContext.IgnoreCase);
            var c = new RegexPathMarkerTraverser(regex == null ? null : pathRegex,
                maxPathLength ?? (regex == null || regex.Contains("*") || regex.Contains("+") ? 100 : regex.Length),
                backwards, globalContext.CheckAbort, foundPaths);
            c.Traverse(dependencies);

            Log.WriteInfo($"... marked {foundPaths.Count} paths");

            if (pathRegex.ContainsCountSymbol) {
                var countedObjects = new Dictionary<Item, HashSet<object>>();
                foreach (var f in foundPaths.Where(f => f.CountedObject != null)) {
                    HashSet<object> countedObjectsForThisItem;
                    if (!countedObjects.TryGetValue(f.LastItem, out countedObjectsForThisItem)) {
                        countedObjects.Add(f.LastItem, countedObjectsForThisItem = new HashSet<object>());
                    }
                    countedObjectsForThisItem.Add(f.CountedObject);
                }
                foreach (var kvp in countedObjects) {
                    kvp.Key.SetMarker(marker, kvp.Value.Count);
                }
            }

            if (addTransitiveDependencyInsteadOfMarking) {
                var transitiveEdges = new List<Dependency>();
                foreach (var f in foundPaths) {
                    transitiveEdges.Add(globalContext.CurrentGraph.CreateDependency(f.FirstItem, f.LastItem,                         
                        source: null, markers: marker, 
                        ct: f.Path.Max(d => d.Ct), questionableCt: f.Path.Max(d => d.QuestionableCt),
                        badCt: f.Path.Max(d => d.BadCt), notOkReason: f.Path.Select(d => d.NotOkReason).FirstOrDefault(),
                        exampleInfo: f.Path.Select(d => d.ExampleInfo).FirstOrDefault()));
                }
                transformedDependencies.AddRange(keepOnlyPathEdges
                    ? transitiveEdges
                    : dependencies.Concat(transitiveEdges));
            } else {
                int i = 0;
                string addIndexToMarkerFormat = addIndex ? "D" + ("" + foundPaths.Count).Length : null;
                foreach (var f in foundPaths) {
                    string m = addIndexToMarkerFormat == null ? marker : string.Format(addIndexToMarkerFormat, i++);
                    foreach (var d in f.Path) {
                        d.IncrementMarker(m);
                    }
                }

                transformedDependencies.AddRange(keepOnlyPathEdges
                    ? new HashSet<Dependency>(foundPaths.SelectMany(f => f.Path))
                    : dependencies);
            }
            return Program.OK_RESULT;
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
    $@"  Marks paths in dependency graph.

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
