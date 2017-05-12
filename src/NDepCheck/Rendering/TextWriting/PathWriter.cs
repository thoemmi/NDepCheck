using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.TextWriting {
    public class PathWriter : IRenderer {
        private struct DownInfo {
            public readonly bool BehindCountMatch;

            public DownInfo(bool behindCountMatch) {
                BehindCountMatch = behindCountMatch;
            }
        }

        private struct SaveInfo {
        }

        private class PathNode<TItem, TDependency>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            public bool IsEnd { get; }
            public bool LeadsToNodesToBePrinted { get; }
            public readonly TDependency Dependency;
            public readonly bool EndOfCycle;
            public readonly IEnumerable<PathNode<TItem, TDependency>> Children;

            public PathNode(TDependency dependency, bool isEnd, bool endOfCycle, IEnumerable<PathNode<TItem, TDependency>> children) {
                Dependency = dependency;
                IsEnd = isEnd;
                EndOfCycle = endOfCycle;
                Children = children ?? Enumerable.Empty<PathNode<TItem, TDependency>>();
                LeadsToNodesToBePrinted = isEnd || Children.Any(ch => ch.LeadsToNodesToBePrinted);
            }
        }

        private class PathWriterTraverser<TDependency, TItem> : AbstractDepthFirstPathTraverser<TDependency, TItem,
            DownInfo, SaveInfo, List<PathNode<TItem, TDependency>>>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly IPathMatch<TDependency, TItem> _countMatch;
            private readonly HashSet<TItem> _seenInnerPathStarts = new HashSet<TItem>();

            public readonly Dictionary<TItem, List<PathNode<TItem, TDependency>>> Paths = new Dictionary<TItem, List<PathNode<TItem, TDependency>>>();
            public readonly Dictionary<TItem, int> Counts = new Dictionary<TItem, int>();
            private readonly HashSet<ItemAndInt> _onPath = new HashSet<ItemAndInt>();
            private readonly bool _backwards;

            public PathWriterTraverser(bool backwards, IPathMatch<TDependency, TItem> countMatch) {
                _backwards = backwards;
                _countMatch = countMatch;
            }

            public void Traverse(IEnumerable<TDependency> dependencies, ItemMatch pathAnchor,
                IPathMatch<TDependency, TItem>[] expectedPathMatches) {
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
                IPathMatch<TDependency, TItem> endMatch;
                IPathMatch<TDependency, TItem>[] innerMatches;
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
                        List<PathNode<TItem, TDependency>> up = Traverse(root: item, incidentDependencies: incidentDependencies, expectedInnerPathMatches: innerMatches, endMatch: endMatch, down: new DownInfo(false));
                        if (up != null) {
                            Paths.Add(item, up);
                        }
                    }
                }
            }

            protected override bool VisitSuccessors(TItem tail, Stack<TDependency> currentPath, int expectedPathMatchIndex, out List<PathNode<TItem, TDependency>> initUpSum) {
                initUpSum = new List<PathNode<TItem, TDependency>>();
                bool tailNewOnPath = _onPath.Add(new ItemAndInt(tail, expectedPathMatchIndex));
                if (currentPath.Count == 0) {
                    // Behind head
                    return true;
                } else {
                    // Check for loop
                    return tailNewOnPath;
                }
            }

            protected override DownAndSave AfterPushDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, DownInfo down) {
                TItem usedItem = currentPath.Peek().UsedItem;
                if (down.BehindCountMatch) {
                    if (!Counts.ContainsKey(usedItem)) {
                        Counts[usedItem] = 1;
                    } else {
                        Counts[usedItem]++;
                    }
                }
                _seenInnerPathStarts.Add(usedItem);
                return new DownAndSave(_countMatch == null
                    ? new DownInfo()
                    : new DownInfo(down.BehindCountMatch || _countMatch == dependencyMatchOrNull || _countMatch == itemMatchOrNull),
                    new SaveInfo());
            }

            protected override List<PathNode<TItem, TDependency>> BeforePopDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd,
                SaveInfo save, List<PathNode<TItem, TDependency>> upSum, List<PathNode<TItem, TDependency>> childUp) {

                var top = currentPath.Peek();
                var key = new ItemAndInt(top.UsedItem, expectedPathMatchIndex);
                var endOfCycle = _onPath.Contains(key);

                if (isEnd || endOfCycle || childUp != null) {
                    PathNode<TItem, TDependency> n = new PathNode<TItem, TDependency>(top, isEnd, endOfCycle, childUp);
                    upSum.Add(n);
                }
                return upSum;
            }

            protected override List<PathNode<TItem, TDependency>> AfterVisitingSuccessors(bool visitSuccessors, TItem tail, Stack<TDependency> currentPath, int expectedPathMatchIndex, List<PathNode<TItem, TDependency>> upSum) {
                if (visitSuccessors) {
                    // We visit the children - tail was therefore definitely not in _onPath - so we can safely remove it!
                    var key = new ItemAndInt(tail, expectedPathMatchIndex);
                    _onPath.Remove(key);
                }
                return upSum.Any(p => p.LeadsToNodesToBePrinted) ? upSum : null;
            }
        }

        private interface IPrintTraverser {
            void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths, Dictionary<Item, int> counts);
        }

        private abstract class AbstractPathWriterTraverser : IPrintTraverser {
            public abstract void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths, Dictionary<Item, int> counts);

            protected static string ItemAsString(bool showItemMarkers, Dictionary<Item, int> counts, Item usedItem, bool isEnd, bool endOfCycle) {
                int count;
                counts.TryGetValue(usedItem, out count);
                return (endOfCycle ? "<= " : "")
                       + (showItemMarkers ? usedItem.AsFullString() : usedItem.AsString())
                       + (isEnd ? " $" : "")
                       + (count > 0 ? " (" + count + ")" : "");
            }
        }

        private class FlatPathWriterTraverser : AbstractPathWriterTraverser {
            private readonly bool _showItemMarkers;

            public FlatPathWriterTraverser(bool showItemMarkers) {
                _showItemMarkers = showItemMarkers;
            }

            public override void Traverse(TextWriter tw, Dictionary<Item, List<PathNode<Item, Dependency>>> paths, Dictionary<Item, int> counts) {
                foreach (var head in paths.Keys.OrderBy(i => i.AsString())) {
                    var stack = new Stack<string>();
                    stack.Push(ItemAsString(_showItemMarkers, counts, head, false, false));
                    foreach (var p in paths[head]) {
                        Traverse(tw, stack, p, counts);
                    }
                }
            }

            private void Traverse(TextWriter tw, Stack<string> stack, [NotNull] PathNode<Item, Dependency> node, Dictionary<Item, int> counts) {
                stack.Push(ItemAsString(_showItemMarkers, counts, node.Dependency.UsedItem, node.IsEnd, node.EndOfCycle));
                if (node.Children.Any()) {
                    foreach (var child in node.Children) {
                        Traverse(tw, stack, child, counts);
                    }
                } else {
                    foreach (var s in stack.Reverse()) {
                        tw.WriteLine(s);
                    }
                    tw.WriteLine();
                }
                stack.Pop();
            }
        }

        //private abstract class TreePathWriterTraverser<TDependency, TItem> : AbstractPathWriterTraverser<TDependency, TItem>
        //        where TDependency : AbstractDependency<TItem>
        //        where TItem : AbstractItem<TItem> {
        //    // For advanced ideas, see e.g. https://www.informatik.uni-rostock.de/~ct/pub_files/Tominski06GraphLenses.pdf

        //    private readonly int _manyPopsLimit;
        //    private readonly Stack<string> _indents = new Stack<string>();
        //    private int _popsAfterLastPush;

        //    protected TreePathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch,
        //                                   int manyPopsLimit = 3) : base(tw, showItemMarkers, backwards, countMatch) {
        //        _manyPopsLimit = manyPopsLimit;
        //        _indents.Push("");
        //    }

        //    protected override void HandleFirstItemBeforeTraverse(TItem firstItem) {
        //        _tw.WriteLine(GetItemAsString(firstItem, _showItemMarkers, false));
        //    }

        //    protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
        //        _tw.WriteLine(_indents.Peek() + GetIndent(true) + "<= " + GetItemAsString(tail, false, false));
        //    }

        //    protected override DownInfo AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, DownInfo down) {
        //        DownInfo result = base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, dependencyMatchOrNull, itemMatchOrNull, isEnd, down);

        //        TItem usedItem = currentPath.Peek().UsedItem;

        //        if (alreadyVisitedLastUsedItemInCurrentPath) {
        //            _tw.Write(_indents.Peek() + GetLead(incidentIndex == incidentCount - 1));
        //            _tw.Write("<= ");
        //            _tw.WriteLine(GetItemAsString(usedItem, false, isEnd));
        //            _indents.Push(_indents.Peek() + GetIndent(incidentIndex == incidentCount - 1));
        //        } else {
        //            _tw.Write(_indents.Peek());
        //            if (_popsAfterLastPush > _manyPopsLimit) {
        //                _tw.Write(GetIndent(false));
        //                _tw.WriteLine();
        //                _tw.Write(_indents.Peek());
        //            }
        //            _tw.Write(GetLead(incidentIndex == incidentCount - 1));
        //            _tw.WriteLine(GetItemAsString(usedItem, _showItemMarkers, isEnd));
        //            _popsAfterLastPush = 0;
        //            _indents.Push(_indents.Peek() + GetIndent(incidentIndex == incidentCount - 1));
        //        }
        //        return result;
        //    }

        //    protected abstract string GetLead(bool isLastIncidentDependency);

        //    protected abstract string GetIndent(bool isLastIncidentDependency);

        //    protected override PathNode BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd, PathNode up) {
        //        _indents.Pop();
        //        _popsAfterLastPush++;
        //        return base.BeforePopDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, isEnd, up);
        //    }
        //}

        //private class TreePathWriterTraverserWithFixedIndent<TDependency, TItem> : TreePathWriterTraverser<TDependency, TItem>
        //                    where TDependency : AbstractDependency<TItem>
        //        where TItem : AbstractItem<TItem> {
        //    private readonly string _indent;

        //    public TreePathWriterTraverserWithFixedIndent(TextWriter tw, string indent, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch, int manyPopsLimit = 3) 
        //        : base(tw, showItemMarkers, backwards, countMatch, manyPopsLimit) {
        //        _indent = indent;
        //    }

        //    protected override string GetLead(bool isLastIncidentDependency) {
        //        return _indent;
        //    }

        //    protected override string GetIndent(bool isLastIncidentDependency) {
        //        return _indent;
        //    }
        //}

        //private class TreePathWriterTraverserWithLineIndent<TDependency, TItem> : TreePathWriterTraverser<TDependency, TItem>
        //                    where TDependency : AbstractDependency<TItem>
        //        where TItem : AbstractItem<TItem> {
        //    private readonly char _spaceChar;

        //    public TreePathWriterTraverserWithLineIndent(TextWriter tw, char spaceChar, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch, int manyPopsLimit = 3)
        //        : base(tw, showItemMarkers, backwards, countMatch, manyPopsLimit) {
        //        _spaceChar = spaceChar;
        //    }

        //    protected override string GetLead(bool isLastIncidentDependency) {
        //        return isLastIncidentDependency ? @"\-" : "+-";
        //    }

        //    protected override string GetIndent(bool isLastIncidentDependency) {
        //        return "" + (isLastIncidentDependency ? _spaceChar : '|') + _spaceChar;
        //    }
        //}

        //private class FlatPathWriterTraverser<TDependency, TItem> : AbstractPathWriterTraverser<TDependency, TItem>
        //        where TDependency : AbstractDependency<TItem>
        //        where TItem : AbstractItem<TItem> {
        //    private bool _dontWriteBeforeNextPush = false;
        //    private readonly List<bool> _isEndStack = new List<bool>();

        //    public FlatPathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch) : base(tw, showItemMarkers, backwards, countMatch) {
        //        _isEndStack.Add(false);
        //    }

        //    protected override void HandleFirstItemBeforeTraverse(TItem firstItem) {
        //        // empty
        //    }

        //    protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
        //        WritePath(currentPath);
        //        _tw.WriteLine("<= " + GetItemAsString(tail, false, false));
        //        _tw.WriteLine();
        //    }

        //    protected override DownInfo AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, DownInfo down) {
        //        DownInfo result = base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, dependencyMatchOrNull, itemMatchOrNull, isEnd, down);
        //        _isEndStack.Add(isEnd);
        //        _dontWriteBeforeNextPush = false;
        //        if (alreadyVisitedLastUsedItemInCurrentPath) {
        //            WritePath(currentPath);
        //            _tw.WriteLine("<= " + GetItemAsString(currentPath.Peek().UsedItem, false, isEnd));
        //            _tw.WriteLine();
        //            _dontWriteBeforeNextPush = true;
        //        }
        //        return result;
        //    }

        //    protected override PathNode BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd, PathNode up) {
        //        if (isEnd && !_dontWriteBeforeNextPush) {
        //            WritePath(currentPath);
        //            _tw.WriteLine(GetItemAsString(currentPath.Peek().UsedItem, _showItemMarkers, true));
        //            _tw.WriteLine();
        //            _dontWriteBeforeNextPush = true;
        //        }
        //        _isEndStack.RemoveAt(_isEndStack.Count - 1);
        //        return base.BeforePopDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, isEnd, up);
        //    }

        //    private void WritePath(Stack<TDependency> currentPath) {
        //        int i = 0;
        //        foreach (var d in currentPath.Reverse()) {
        //            _tw.WriteLine(GetItemAsString(d.UsingItem, _showItemMarkers, _isEndStack[i++]));
        //        }
        //    }
        //}

        public static readonly Option PathItemAnchorOption = new Option("pi", "path-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option PathDependencyAnchorOption = new Option("pd", "path-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly Option CountItemAnchorOption = new Option("ci", "count-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option CountDependencyAnchorOption = new Option("cd", "count-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly Option MultipleItemAnchorOption = new Option("mi", "multiple-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option MultipleDependencyAnchorOption = new Option("md", "multiple-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly Option NoSuchItemAnchorOption = new Option("ni", "no-such-item", "itempattern", "item pattern to be matched by path", @default: "all items match", multiple: true);
        public static readonly Option NoSuchDependencyAnchorOption = new Option("nd", "no-such-dependency", "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions();
        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);
        public static readonly Option StyleOption = new Option("ps", "path-style", "&", "One of: SpaceIndent or SI, TabIndent or TI, LineIndent or LI, Flat or F", @default: "SI");
        public static readonly Option BackwardsOption = new Option("bw", "upwards", "", "Traverses dependencies in opposite direction", @default: false);
        public static readonly Option ShowItemMarkersOption = new Option("sm", "show-markers", "", "Shows markers on items", @default: false);

        private static readonly Option[] _allOptions = DependencyMatchOptions.WithOptions(
            PathItemAnchorOption, PathDependencyAnchorOption,
            CountItemAnchorOption, CountDependencyAnchorOption,
            MultipleItemAnchorOption, MultipleDependencyAnchorOption,
            NoSuchItemAnchorOption, NoSuchDependencyAnchorOption,
            NoExampleInfoOption, StyleOption, BackwardsOption, ShowItemMarkersOption);

        private static void Write(bool backwards, IPathMatch<Dependency, Item> countMatch,
                bool withHeader, IEnumerable<Dependency> dependencies, ItemMatch pathAnchor,
                [NotNull] IPathMatch<Dependency, Item>[] expectedPathMatches,
                IPrintTraverser printTraverser, TextWriter tw) {
            if (withHeader) {
                tw.WriteLine($"// Written {DateTime.Now} by {typeof(PathWriter).Name} in NDepCheck {Program.VERSION}");
            }

            var c = new PathWriterTraverser<Dependency, Item>(backwards, countMatch);
            c.Traverse(dependencies, pathAnchor, expectedPathMatches);
            printTraverser.Traverse(tw, c.Paths, c.Counts);
        }

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount,
                           string argsAsString, string baseFileName, bool ignoreCase) {
            bool showItemMarkers = false;
            bool backwards = false;
            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();
            //int maxPathLength = int.MaxValue;
            ItemMatch pathAnchor = null;
            var expectedPathMatches = new List<IPathMatch<Dependency, Item>>();
            IPathMatch<Dependency, Item> countMatch = null;
            string styleOption = "SI";

            DependencyMatchOptions.Parse(globalContext, argsAsString, globalContext.IgnoreCase, matches, excludes,
                BackwardsOption.Action((args, j) => {
                    backwards = true;
                    return j;
                }),
                ShowItemMarkersOption.Action((args, j) => {
                    showItemMarkers = true;
                    return j;
                }),
                PathItemAnchorOption.Action((args, j) => {
                    if (pathAnchor == null) {
                        pathAnchor = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item pattern"), ignoreCase);
                    } else {
                        expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    }
                    return j;
                }),
                PathDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    return j;
                }),
                CountItemAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(countMatch = CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    return j;
                }),
                CountDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(countMatch = CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    return j;
                }),
                MultipleItemAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    return j;
                }),
                MultipleDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    return j;
                }),
                NoSuchItemAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    return j;
                }),
                NoSuchDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false, mayContinue: false));
                    return j;
                }),
                StyleOption.Action((args, j) => {
                    styleOption = Option.ExtractRequiredOptionValue(args, ref j, "missing style");
                    return j;
                })
                //MaxPathLengthOption.Action((args, j) => {
                //    maxPathLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum path length");
                //    return j;
                //})
                );

            using (var sw = GlobalContext.CreateTextWriter(GetMasterFileName(globalContext, argsAsString, baseFileName))) {
                IPrintTraverser traverser;
                switch (styleOption.ToUpperInvariant()) {
                    //case "SPACEINDENT":
                    //case "SI":
                    //    traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw.Writer, " ", showItemMarkers: showItemMarkers, backwards: backwards, countMatch: countMatch);
                    //    break;
                    //case "TABINDENT":
                    //case "TI":
                    //    traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw.Writer, "\t", showItemMarkers: showItemMarkers, backwards: backwards, countMatch: countMatch);
                    //    break;
                    //case "LINEINDENT":
                    //case "LI":
                    //    traverser = new TreePathWriterTraverserWithLineIndent<Dependency, Item>(sw.Writer, ' ', showItemMarkers: showItemMarkers, backwards: backwards, countMatch: countMatch);
                    //    break;
                    case "FLAT":
                    case "F":
                        traverser = new FlatPathWriterTraverser(showItemMarkers: showItemMarkers);
                        break;
                    default:
                        throw new ArgumentException($"Style '{styleOption}' not supported for PathWriter");
                }

                Write(backwards, countMatch, true, dependencies, pathAnchor, expectedPathMatches.ToArray(), traverser, sw.Writer);
            }
        }

        // ReSharper disable once UnusedParameter.Local -- method is just a precondition check
        private static void CheckPathAnchorSet(ItemMatch pathAnchor) {
            if (pathAnchor == null) {
                throw new ArgumentException($"First path pattern must be specified with {PathItemAnchorOption}");
            }
        }

        private static DependencyPathMatch<Dependency, Item> CreateDependencyPathMatch(GlobalContext globalContext, string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue) {
            return new DependencyPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed, mayContinue: mayContinue);
        }

        private static ItemPathMatch<Dependency, Item> CreateItemPathMatch(GlobalContext globalContext, string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue) {
            return new ItemPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed, mayContinue: mayContinue);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output, string option) {
            using (var sw = new StreamWriter(output)) {
                IPrintTraverser traverser;
                switch (option) {
                    //case "SI":
                    //    traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw, ".", showItemMarkers: false, backwards: false, countMatch: null);
                    //    break;
                    //case "LI":
                    //    traverser = new TreePathWriterTraverserWithLineIndent<Dependency, Item>(sw, '.', showItemMarkers: false, backwards: false, countMatch: null);
                    //    break;
                    case "F":
                        traverser = new FlatPathWriterTraverser(showItemMarkers: false);
                        break;
                    default:
                        throw new ArgumentException($"option '{option}' not supported");
                }

                Write(backwards: false, countMatch: null, withHeader: false,
                    dependencies: dependencies, pathAnchor: new ItemMatch("a:", true),
                    expectedPathMatches: new IPathMatch<Dependency, Item>[] {
                          new ItemPathMatch<Dependency, Item>("~c:", true, multipleOccurrencesAllowed: true, mayContinue: true)
                    }, printTraverser: traverser, tw: sw);
            }
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
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
    $@"  Writes dependencies to .dip files, which can be read in by 
  NDepCheck's DipReader. This is very helpful for building pipelines 
  that process dependencies for different purposes.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".dip");
        }
    }
}
