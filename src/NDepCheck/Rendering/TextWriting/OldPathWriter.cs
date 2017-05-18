//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using JetBrains.Annotations;
//using NDepCheck.Matching;

//namespace NDepCheck.Rendering.TextWriting {
//    TODO - remove after a TreeWriter has been derived from this!
//    public class OldPathWriter : IRenderer {
//        private struct DownInfo {
//            public readonly IMatchableObject BehindCountMatch;

//            public DownInfo(IMatchableObject behindCountMatch) {
//                BehindCountMatch = behindCountMatch;
//            }
//        }

//        private struct HereInfo {
//            public readonly bool MatchedByCountMatch;

//            public HereInfo(bool matchedByCountMatch) {
//                MatchedByCountMatch = matchedByCountMatch;
//            }
//        }

//        private class PathNode<TItem, TDependency>
//                where TDependency : AbstractDependency<TItem>
//                where TItem : AbstractItem<TItem> {
//            public bool IsEnd {
//                get;
//            }
//            public bool LeadsToNodesToBePrinted {
//                get;
//            }
//            public readonly TDependency Dependency;
//            public readonly bool IsEndOfCycle;
//            public readonly bool MatchedByCountMatch;

//            [NotNull, ItemNotNull]
//            public readonly IEnumerable<PathNode<TItem, TDependency>> Children;

//            public PathNode(TDependency dependency, bool isEnd, bool isEndOfCycle, bool matchedByCountMatch, IEnumerable<PathNode<TItem, TDependency>> children) {
//                Dependency = dependency;
//                IsEnd = isEnd;
//                IsEndOfCycle = isEndOfCycle;
//                MatchedByCountMatch = matchedByCountMatch;
//                Children = children ?? Enumerable.Empty<PathNode<TItem, TDependency>>();
//                LeadsToNodesToBePrinted = isEndOfCycle || isEnd || Children.Any(ch => ch.LeadsToNodesToBePrinted);
//            }
//        }

//        private class PathWriterTraverser<TDependency, TItem> : AbstractDepthFirstPathTraverser<TDependency, TItem,
//            DownInfo, HereInfo, List<PathNode<TItem, TDependency>>>
//                where TDependency : AbstractDependency<TItem>
//                where TItem : AbstractItem<TItem> {
//            private readonly AbstractPathMatch<TDependency, TItem> _countMatch;
//            private readonly HashSet<TItem> _seenInnerPathStarts = new HashSet<TItem>();

//            public readonly Dictionary<TItem, List<PathNode<TItem, TDependency>>> Paths = new Dictionary<TItem, List<PathNode<TItem, TDependency>>>();
//            public readonly Dictionary<TItem, HashSet<IMatchableObject>> Counts = new Dictionary<TItem, HashSet<IMatchableObject>>();
//            private readonly HashSet<ItemAndInt> _onPath = new HashSet<ItemAndInt>();
//            private readonly bool _backwards;

//            public PathWriterTraverser(bool backwards, AbstractPathMatch<TDependency, TItem> countMatch, [NotNull] Action checkAbort) : base(checkAbort) {
//                _backwards = backwards;
//                _countMatch = countMatch;
//            }

//            public void Traverse(IEnumerable<TDependency> dependencies, ItemMatch pathAnchor,
//                bool pathAnchorIsCountMatch, AbstractPathMatch<TDependency, TItem>[] expectedPathMatches) {
//                Dictionary<TItem, TDependency[]> incidentDependencies = _backwards
//                    ? AbstractItem<TItem>.CollectIncomingDependenciesMap(dependencies)
//                    : AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);
//                _seenInnerPathStarts.Clear();
//                Paths.Clear();
//                Counts.Clear();
//                _onPath.Clear();

//                IEnumerable<TItem> uniqueStartItems = pathAnchor == null
//                    ? incidentDependencies.Keys
//                    : incidentDependencies.Keys.Where(i => pathAnchor.Matches(i).Success);
//                AbstractPathMatch<TDependency, TItem> endMatch;
//                AbstractPathMatch<TDependency, TItem>[] innerMatches;
//                int n = expectedPathMatches.Length;
//                if (n == 0) {
//                    endMatch = null;
//                    innerMatches = expectedPathMatches;
//                } else {
//                    endMatch = expectedPathMatches[n - 1];
//                    innerMatches = expectedPathMatches.Take(n - 1).ToArray();
//                }

//                foreach (var item in uniqueStartItems.OrderBy(i => i.Name)) {
//                    if (_seenInnerPathStarts.Add(item)) {
//                        List<PathNode<TItem, TDependency>> up = Traverse(root: item,
//                            incidentDependencies: incidentDependencies, expectedInnerPathMatches: innerMatches,
//                            endMatch: endMatch, down: new DownInfo(pathAnchorIsCountMatch ? item : null));
//                        if (up != null) {
//                            Paths.Add(item, up);
//                        }
//                    }
//                }
//            }

//            protected override bool ShouldVisitSuccessors(TItem tail, Stack<TDependency> currentPath, int expectedPathMatchIndex, out List<PathNode<TItem, TDependency>> initUpSum) {
//                initUpSum = new List<PathNode<TItem, TDependency>>();
//                bool tailNewOnPath = _onPath.Add(new ItemAndInt(tail, expectedPathMatchIndex));
//                if (currentPath.Count == 0) {
//                    // Behind head
//                    return true;
//                } else {
//                    // Check for loop
//                    return tailNewOnPath;
//                }
//            }

//            protected override DownAndHere AfterPushDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
//                    AbstractPathMatch<TDependency, TItem> pathMatchOrNull, bool isEnd, DownInfo down) {
//                TDependency tailDependency = currentPath.Peek();
//                TItem usedItem = tailDependency.UsedItem;
//                if (down.BehindCountMatch != null) {
//                    if (!Counts.ContainsKey(usedItem)) {
//                        Counts[usedItem] = new HashSet<IMatchableObject>();
//                    }
//                    Counts[usedItem].Add(down.BehindCountMatch);
//                }
//                _seenInnerPathStarts.Add(usedItem);

//                IMatchableObject pushDownMatch;
//                bool matchedByCountMatch;
//                if (down.BehindCountMatch != null) {
//                    pushDownMatch = down.BehindCountMatch;
//                    matchedByCountMatch = false;
//                } else if (_countMatch == null) {
//                    pushDownMatch = null;
//                    matchedByCountMatch = false;
//                } else if (_countMatch == pathMatchOrNull) {
//                    pushDownMatch = _countMatch is DependencyPathMatch<TDependency, TItem>
//                        ? (IMatchableObject) tailDependency
//                        : usedItem;
//                    matchedByCountMatch = true;
//                } else {
//                    pushDownMatch = null;
//                    matchedByCountMatch = false;
//                }

//                return new DownAndHere(new DownInfo(pushDownMatch), new HereInfo(matchedByCountMatch));
//            }

//            protected override List<PathNode<TItem, TDependency>> BeforePopDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
//                    AbstractPathMatch<TDependency, TItem> pathMatchOrNull, AbstractPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd,
//                    HereInfo here, List<PathNode<TItem, TDependency>> upSum, List<PathNode<TItem, TDependency>> childUp) {

//                TDependency top = currentPath.Peek();
//                var key = new ItemAndInt(top.UsedItem, expectedPathMatchIndex);
//                bool isEndOfCycle = _onPath.Contains(key);

//                if (isEnd || isEndOfCycle || childUp.Any(p => p.LeadsToNodesToBePrinted)) {
//                    PathNode<TItem, TDependency> n = new PathNode<TItem, TDependency>(top, isEnd, isEndOfCycle, here.MatchedByCountMatch, childUp);
//                    upSum.Add(n);
//                }
//                return upSum;
//            }

//            protected override List<PathNode<TItem, TDependency>> AfterVisitingSuccessors(bool visitSuccessors, TItem tail,
//                    Stack<TDependency> currentPath, int expectedPathMatchIndex, List<PathNode<TItem, TDependency>> upSum) {
//                if (visitSuccessors) {
//                    // We visit the children - tail was therefore definitely not in _onPath - so we can safely remove it!
//                    var key = new ItemAndInt(tail, expectedPathMatchIndex);
//                    _onPath.Remove(key);
//                }
//                return upSum;
//            }
//        }

//        private interface IPrintTraverser {
//            void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths, Dictionary<Item, int> counts);
//        }

//        private abstract class AbstractPrintTraverser : IPrintTraverser {
//            protected readonly bool _showItemMarkers;
//            [NotNull]
//            protected readonly Action _checkAbort;

//            protected AbstractPrintTraverser(bool showItemMarkers, [NotNull] Action checkAbort) {
//                _showItemMarkers = showItemMarkers;
//                _checkAbort = checkAbort;
//            }

//            public void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths, Dictionary<Item, int> counts) {
//                foreach (var head in paths.Keys.OrderBy(i => i.AsString())) {
//                    Traverse(tw, paths, counts, head);
//                }
//            }

//            protected abstract void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths,
//                Dictionary<Item, int> counts, Item head);
//        }

//        private class FlatPrintTraverser : AbstractPrintTraverser {
//            public FlatPrintTraverser(bool showItemMarkers, [NotNull] Action checkAbort) : base(showItemMarkers, checkAbort) {
//            }

//            protected override void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths,
//                Dictionary<Item, int> counts, Item head) {
//                var stack = new Stack<string>();

//                stack.Push(head.ItemAsString(_showItemMarkers, false, false, false));
//                foreach (var p in paths[head]) {
//                    Traverse(tw, stack, p, counts);
//                }
//            }

//            private void Traverse(TextWriter tw, Stack<string> stack, [NotNull] PathNode<Item, Dependency> node, Dictionary<Item, int> counts) {
//                _checkAbort();

//                Item item = node.Dependency.UsedItem;
//                stack.Push(item.ItemAsString(_showItemMarkers, node.IsEnd, node.IsEndOfCycle, node.MatchedByCountMatch));
//                if (node.Children.Any()) {
//                    foreach (var child in node.Children) {
//                        Traverse(tw, stack, child, counts);
//                    }
//                } else {
//                    foreach (var s in stack.Reverse()) {
//                        tw.WriteLine(s);
//                    }
//                    tw.WriteLine();
//                }
//                stack.Pop();
//            }
//        }

//        private class TreePrintTraverser : AbstractPrintTraverser {
//            private readonly int _manyPopsLimit;
//            private readonly string _regularIndent;
//            private readonly string _lastIndent;
//            private readonly string _regularPrefix;
//            private readonly string _lastPrefix;
//            private int _popsAfterLastPush;

//            public TreePrintTraverser(string regularIndent, string lastIndent, string regularPrefix, string lastPrefix, int manyPopsLimit, bool showItemMarkers, [NotNull] Action checkAbort) : base(showItemMarkers, checkAbort) {
//                _manyPopsLimit = manyPopsLimit;
//                _regularIndent = regularIndent;
//                _lastIndent = lastIndent;
//                _regularPrefix = regularPrefix;
//                _lastPrefix = lastPrefix;
//            }

//            protected override void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths,
//                Dictionary<Item, int> counts, Item head) {
//                tw.WriteLine(head.ItemAsString(_showItemMarkers, false, false, false));
//                List<PathNode<Item, Dependency>> path = paths[head];
//                var lastChild = path.LastOrDefault();
//                foreach (var p in path) {
//                    // ReSharper disable once AssignNullToNotNullAttribute - pathNodes in lists are never null
//                    Traverse(tw, p == lastChild, "", p, counts);
//                }
//            }

//            private void Traverse(TextWriter tw, bool isLastChild, string currentIndent, [NotNull] PathNode<Item, Dependency> node, Dictionary<Item, int> counts) {
//                _checkAbort();

//                if (_popsAfterLastPush >= _manyPopsLimit) {
//                    tw.WriteLine(currentIndent + _regularIndent);
//                }
//                _popsAfterLastPush = 0;
//                Item item = node.Dependency.UsedItem;
//                tw.WriteLine(
//                    currentIndent
//                    + (isLastChild ? _lastPrefix : _regularPrefix)
//                    + item.ItemAsString(_showItemMarkers, node.IsEnd, node.IsEndOfCycle, node.MatchedByCountMatch)
//                );
//                var lastChild = node.Children.LastOrDefault();
//                foreach (var child in node.Children) {
//                    Traverse(tw, child == lastChild, currentIndent + (isLastChild ? _lastIndent : _regularIndent), child, counts);
//                }
//                _popsAfterLastPush++;
//            }
//        }

//        public static readonly Option PathItemAnchorOption = new Option("pi", "path-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
//        public static readonly Option PathDependencyAnchorOption = new Option("pd", "path-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
//        public static readonly Option CountItemAnchorOption = new Option("ci", "count-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
//        public static readonly Option CountDependencyAnchorOption = new Option("cd", "count-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
//        public static readonly Option MultipleItemAnchorOption = new Option("mi", "multiple-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
//        public static readonly Option MultipleDependencyAnchorOption = new Option("md", "multiple-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
//        public static readonly Option NoSuchItemAnchorOption = new Option("ni", "no-such-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
//        public static readonly Option NoSuchDependencyAnchorOption = new Option("nd", "no-such-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
//        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);
//        public static readonly Option StyleOption = new Option("ps", "path-style", "&", "One of: SpaceIndent or SI, TabIndent or TI, LineIndent or LI, Flat or F", @default: "SI");
//        public static readonly Option BackwardsOption = new Option("bw", "upwards", "", "Traverses dependencies in opposite direction", @default: false);
//        public static readonly Option ShowItemMarkersOption = new Option("sm", "show-markers", "", "Shows markers on items", @default: false);

//        private static readonly Option[] _allOptions = {
//            PathItemAnchorOption, PathDependencyAnchorOption,
//            CountItemAnchorOption, CountDependencyAnchorOption,
//            MultipleItemAnchorOption, MultipleDependencyAnchorOption,
//            NoSuchItemAnchorOption, NoSuchDependencyAnchorOption,
//            NoExampleInfoOption, StyleOption, BackwardsOption, ShowItemMarkersOption};

//        private static void Write(bool backwards, AbstractPathMatch<Dependency, Item> countMatch,
//                bool withHeader, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, ItemMatch pathAnchor,
//                bool pathAnchorIsCountMatch, [NotNull] AbstractPathMatch<Dependency, Item>[] expectedPathMatches,
//                IPrintTraverser printTraverser, TextWriter tw, [NotNull] GlobalContext globalContext) {
//            if (withHeader) {
//                tw.WriteLine($"// Written {DateTime.Now} by {typeof(OldPathWriter).Name} in NDepCheck {Program.VERSION}");
//            }

//            var c = new PathWriterTraverser<Dependency, Item>(backwards, countMatch, globalContext.CheckAbort);
//            OperationCanceledException abortedException = null;
//            try {
//                c.Traverse(dependencies, pathAnchor, pathAnchorIsCountMatch, expectedPathMatches);
//            } catch (OperationCanceledException ex) {
//                abortedException = ex;
//                globalContext.RestartAbortWatchDog();
//            }
//            printTraverser.Traverse(tw, c.Paths, c.Counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count));

//            if (abortedException != null) {
//                throw abortedException;
//            }
//        }

//        public void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, int? dependenciesCount,
//                           string argsAsString, string baseFileName, bool ignoreCase) {
//            bool showItemMarkers = false;
//            bool backwards = false;
//            //int maxPathLength = int.MaxValue;
//            ItemMatch pathAnchor = null;
//            bool pathAnchorIsCountMatch = false;
//            var expectedPathMatches = new List<AbstractPathMatch<Dependency, Item>>();
//            var dontMatches = new List<AbstractPathMatch<Dependency, Item>>();
//            AbstractPathMatch<Dependency, Item> countMatch = null;
//            string styleOption = "SI";

//            Option.Parse(globalContext, argsAsString,
//                BackwardsOption.Action((args, j) => {
//                    backwards = true;
//                    return j;
//                }),
//                ShowItemMarkersOption.Action((args, j) => {
//                    showItemMarkers = true;
//                    return j;
//                }),
//                PathItemAnchorOption.Action((args, j) => {
//                    if (pathAnchor == null) {
//                        pathAnchor = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item pattern"), ignoreCase);
//                    } else {
//                        expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
//                    }
//                    return j;
//                }),
//                PathDependencyAnchorOption.Action((args, j) => {
//                    CheckPathAnchorSet(pathAnchor);
//                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
//                    return j;
//                }),
//                CountItemAnchorOption.Action((args, j) => {
//                    CheckPathAnchorSet(pathAnchor);
//                    if (pathAnchor == null) {
//                        pathAnchor = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item pattern"), ignoreCase);
//                        pathAnchorIsCountMatch = true;
//                    } else {
//                        expectedPathMatches.Add(countMatch = CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
//                    }
//                    return j;
//                }),
//                CountDependencyAnchorOption.Action((args, j) => {
//                    CheckPathAnchorSet(pathAnchor);
//                    expectedPathMatches.Add(countMatch = CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: true, dontMatches: dontMatches));
//                    return j;
//                }),
//                MultipleItemAnchorOption.Action((args, j) => {
//                    CheckPathAnchorSet(pathAnchor);
//                    expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: true, mayContinue: true, dontMatches: dontMatches));
//                    return j;
//                }),
//                MultipleDependencyAnchorOption.Action((args, j) => {
//                    CheckPathAnchorSet(pathAnchor);
//                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: true, mayContinue: true, dontMatches: dontMatches));
//                    return j;
//                }),
//                NoSuchItemAnchorOption.Action((args, j) => {
//                    CheckPathAnchorSet(pathAnchor);
//                    dontMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false, dontMatches: new AbstractPathMatch<Dependency, Item>[0]));
//                    return j;
//                }),
//                NoSuchDependencyAnchorOption.Action((args, j) => {
//                    CheckPathAnchorSet(pathAnchor);
//                    dontMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false, dontMatches: new AbstractPathMatch<Dependency, Item>[0]));
//                    return j;
//                }),
//                StyleOption.Action((args, j) => {
//                    styleOption = Option.ExtractRequiredOptionValue(args, ref j, "missing style (on of F, FLAT, SI, SPACEINDENT, LI, LINEINDENT");
//                    return j;
//                })
//                //MaxPathLengthOption.Action((args, j) => {
//                //    maxPathLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum path length");
//                //    return j;
//                //})
//                );

//            using (var sw = GlobalContext.CreateTextWriter(GetMasterFileName(globalContext, argsAsString, baseFileName))) {
//                IPrintTraverser traverser;
//                switch (styleOption.ToUpperInvariant()) {
//                    case "SPACEINDENT":
//                    case "SI":
//                        traverser = new TreePrintTraverser("  ", "  ", "  ", "  ", manyPopsLimit: 3, showItemMarkers: showItemMarkers, checkAbort: globalContext.CheckAbort);
//                        break;
//                    case "TABINDENT":
//                    case "TI":
//                        traverser = new TreePrintTraverser("\t", "\t", "  ", "  ", manyPopsLimit: 3, showItemMarkers: showItemMarkers, checkAbort: globalContext.CheckAbort);
//                        break;
//                    case "LINEINDENT":
//                    case "LI":
//                        traverser = new TreePrintTraverser("| ", "  ", "+-", @"\-", manyPopsLimit: 3, showItemMarkers: showItemMarkers, checkAbort: globalContext.CheckAbort);
//                        break;
//                    case "FLAT":
//                    case "F":
//                        traverser = new FlatPrintTraverser(showItemMarkers: showItemMarkers, checkAbort: globalContext.CheckAbort);
//                        break;
//                    default:
//                        throw new ArgumentException($"Style '{styleOption}' not supported for PathWriter");
//                }

//                Write(backwards, countMatch, true, dependencies, pathAnchor, pathAnchorIsCountMatch,
//                      expectedPathMatches.ToArray(), traverser, sw.Writer, globalContext);
//            }
//        }

//        // ReSharper disable once UnusedParameter.Local -- method is just a precondition check
//        private static void CheckPathAnchorSet(ItemMatch pathAnchor) {
//            if (pathAnchor == null) {
//                throw new ArgumentException($"First path pattern must be specified with {PathItemAnchorOption}");
//            }
//        }

//        private static DependencyPathMatch<Dependency, Item> CreateDependencyPathMatch([NotNull] GlobalContext globalContext, string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue, IEnumerable<AbstractPathMatch<Dependency, Item>> dontMatches) {
//            return new DependencyPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed, mayContinue: mayContinue, dontMatches: dontMatches);
//        }

//        private static ItemPathMatch<Dependency, Item> CreateItemPathMatch([NotNull] GlobalContext globalContext, string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue, IEnumerable<AbstractPathMatch<Dependency, Item>> dontMatches) {
//            return new ItemPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed, mayContinue: mayContinue, dontMatches: dontMatches);
//        }

//        public void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream output, string option) {
//            using (var sw = new StreamWriter(output)) {
//                IPrintTraverser traverser;
//                switch (option) {
//                    case "SI":
//                        traverser = new TreePrintTraverser(".", ".", ".", ".", manyPopsLimit: 3, showItemMarkers: false, checkAbort: () => {
//                        });
//                        break;
//                    case "LI":
//                        traverser = new TreePrintTraverser("|.", "..", "+-", @"\-", manyPopsLimit: 3, showItemMarkers: false, checkAbort: () => {
//                        });
//                        break;
//                    case "F":
//                        traverser = new FlatPrintTraverser(showItemMarkers: false, checkAbort: () => {
//                        });
//                        break;
//                    default:
//                        throw new ArgumentException($"option '{option}' not supported");
//                }

//                Write(backwards: false, countMatch: null, withHeader: false,
//                    dependencies: dependencies, pathAnchor: new ItemMatch("a:", true),
//                    pathAnchorIsCountMatch: true, expectedPathMatches: new AbstractPathMatch<Dependency, Item>[] {
//                          new ItemPathMatch<Dependency, Item>("~c:", ignoreCase: true,
//                                        multipleOccurrencesAllowed: true, mayContinue: true, dontMatches: new AbstractPathMatch<Dependency, Item>[0])
//                    }, printTraverser: traverser, tw: sw, globalContext: globalContext);
//            }
//        }

//        public IEnumerable<Dependency> CreateSomeTestDependencies() {
//            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

//            var a = Item.New(t3, "a:aa:aaa".Split(':'));
//            var b = Item.New(t3, "b:bb:bbb".Split(':'));
//            var c = Item.New(t3, "c:cc:ccc".Split(':'));
//            var d = Item.New(t3, "d:dd:ddd".Split(':'));
//            var e = Item.New(t3, "e:ee:eee".Split(':'));
//            var f = Item.New(t3, "f:ff:fff".Split(':'));
//            var g = Item.New(t3, "g:gg:ggg".Split(':'));
//            var h = Item.New(t3, "h:hh:hhh".Split(':'));

//            return new[] {
//                FromTo(a, b),
//                FromTo(a, h),
//                FromTo(b, c),
//                FromTo(c, d),
//                FromTo(d, e),
//                FromTo(d, b),
//                FromTo(e, f),
//                FromTo(b, g),
//                FromTo(h, g),
//            };
//        }

//        private Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
//            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionable);
//        }

//        public string GetHelp(bool detailedHelp, string filter) {
//            return
//    $@"  Writes complete paths to a .txt files.

//{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
//        }

//        public string GetMasterFileName([NotNull] GlobalContext globalContext, string argsAsString, string baseFileName) {
//            return GlobalContext.CreateFullFileName(baseFileName, ".txt");
//        }
//    }
//}
