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

        private struct UpInfo {
            public readonly int NumberOfReachedEnds;

            public UpInfo(int numberOfReachedEnds) {
                NumberOfReachedEnds = numberOfReachedEnds;
            }
        }

        private abstract class AbstractPathWriterTraverser<TDependency, TItem> : AbstractDepthFirstPathTraverser<TDependency, TItem, DownInfo, UpInfo>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            protected readonly TextWriter _tw;
            protected readonly bool _showItemMarkers;
            private readonly bool _backwards;
            private readonly IPathMatch<Dependency, Item> _countMatch;

            private readonly HashSet<TItem> _seenPathStarts = new HashSet<TItem>();

            protected AbstractPathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch) : base(retraverseItems: true) {
                _tw = tw;
                _showItemMarkers = showItemMarkers;
                _backwards = backwards;
                _countMatch = countMatch;
            }

            public void Traverse(IEnumerable<TDependency> dependencies, ItemMatch pathAnchor,
                IPathMatch<TDependency, TItem>[] expectedPathMatches) {
                Dictionary<TItem, TDependency[]> incidentDependencies = _backwards
                    ? AbstractItem<TItem>.CollectIncomingDependenciesMap(dependencies)
                    : AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);
                _seenPathStarts.Clear();

                IEnumerable<TItem> startItems = pathAnchor == null ? incidentDependencies.Keys : incidentDependencies.Keys.Where(i => pathAnchor.Matches(i).Success);
                IPathMatch<TDependency, TItem> endMatch;
                IPathMatch<TDependency, TItem>[] innerMatches;
                var n = expectedPathMatches.Length;
                if (n == 0) {
                    endMatch = null;
                    innerMatches = expectedPathMatches;
                } else {
                    endMatch = expectedPathMatches[n - 1];
                    innerMatches = expectedPathMatches.Take(n - 1).ToArray();
                }

                foreach (var i1 in startItems.OrderBy(i => i.Name)) {
                    if (_seenPathStarts.Add(i1)) {
                        HandleFirstItemBeforeTraverse(i1);
                        Traverse(root: i1, ignoreCyclesInThisRecursion: false, incidentDependencies: incidentDependencies, maxLength: int.MaxValue,
                            expectedInnerPathMatches: innerMatches, endMatch: endMatch);
                    }
                }
            }

            protected abstract void HandleFirstItemBeforeTraverse(TItem firstItem);

            protected override DownInfo AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, DownInfo down) {
                _seenPathStarts.Add(currentPath.Peek().UsedItem);
                return _countMatch == null 
                    ? new DownInfo() 
                    : new DownInfo(down.BehindCountMatch || _countMatch == dependencyMatchOrNull || _countMatch == itemMatchOrNull);
            }

            protected override UpInfo BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd, UpInfo up) {
                return isEnd ? new UpInfo(up.NumberOfReachedEnds + 1) : up;
            }

            protected override UpInfo OnFoundCycleToRoot(Stack<TDependency> currentPath) {
                return new UpInfo(0);
            }

            protected string GetItemAsString(TItem item, bool showMarkers, bool markAsEnd) {
                return (showMarkers ? item.AsFullString() : item.AsString()) + (markAsEnd ? " $" : "");
            }

            protected override UpInfo AggregateUpInfo(UpInfo sum, UpInfo next) {
                return new UpInfo(sum.NumberOfReachedEnds + next.NumberOfReachedEnds);
            }
        }

        private abstract class TreePathWriterTraverser<TDependency, TItem> : AbstractPathWriterTraverser<TDependency, TItem>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            // For advanced ideas, see e.g. https://www.informatik.uni-rostock.de/~ct/pub_files/Tominski06GraphLenses.pdf

            private readonly int _manyPopsLimit;
            private readonly Stack<string> _indents = new Stack<string>();
            private int _popsAfterLastPush;

            protected TreePathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch,
                                           int manyPopsLimit = 3) : base(tw, showItemMarkers, backwards, countMatch) {
                _manyPopsLimit = manyPopsLimit;
                _indents.Push("");
            }

            protected override void HandleFirstItemBeforeTraverse(TItem firstItem) {
                _tw.WriteLine(GetItemAsString(firstItem, _showItemMarkers, false));
            }

            protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
                _tw.WriteLine(_indents.Peek() + GetIndent(true) + "<= " + GetItemAsString(tail, false, false));
            }

            protected override DownInfo AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, DownInfo down) {
                DownInfo result = base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, dependencyMatchOrNull, itemMatchOrNull, isEnd, down);

                TItem usedItem = currentPath.Peek().UsedItem;

                if (alreadyVisitedLastUsedItemInCurrentPath) {
                    _tw.Write(_indents.Peek() + GetLead(incidentIndex == incidentCount - 1));
                    _tw.Write("<= ");
                    _tw.WriteLine(GetItemAsString(usedItem, false, isEnd));
                    _indents.Push(_indents.Peek() + GetIndent(incidentIndex == incidentCount - 1));
                } else {
                    _tw.Write(_indents.Peek());
                    if (_popsAfterLastPush > _manyPopsLimit) {
                        _tw.Write(GetIndent(false));
                        _tw.WriteLine();
                        _tw.Write(_indents.Peek());
                    }
                    _tw.Write(GetLead(incidentIndex == incidentCount - 1));
                    _tw.WriteLine(GetItemAsString(usedItem, _showItemMarkers, isEnd));
                    _popsAfterLastPush = 0;
                    _indents.Push(_indents.Peek() + GetIndent(incidentIndex == incidentCount - 1));
                }
                return result;
            }

            protected abstract string GetLead(bool isLastIncidentDependency);

            protected abstract string GetIndent(bool isLastIncidentDependency);

            protected override UpInfo BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd, UpInfo up) {
                _indents.Pop();
                _popsAfterLastPush++;
                return base.BeforePopDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, isEnd, up);
            }
        }

        private class TreePathWriterTraverserWithFixedIndent<TDependency, TItem> : TreePathWriterTraverser<TDependency, TItem>
                            where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly string _indent;

            public TreePathWriterTraverserWithFixedIndent(TextWriter tw, string indent, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch, int manyPopsLimit = 3) 
                : base(tw, showItemMarkers, backwards, countMatch, manyPopsLimit) {
                _indent = indent;
            }

            protected override string GetLead(bool isLastIncidentDependency) {
                return _indent;
            }

            protected override string GetIndent(bool isLastIncidentDependency) {
                return _indent;
            }
        }

        private class TreePathWriterTraverserWithLineIndent<TDependency, TItem> : TreePathWriterTraverser<TDependency, TItem>
                            where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly char _spaceChar;

            public TreePathWriterTraverserWithLineIndent(TextWriter tw, char spaceChar, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch, int manyPopsLimit = 3)
                : base(tw, showItemMarkers, backwards, countMatch, manyPopsLimit) {
                _spaceChar = spaceChar;
            }

            protected override string GetLead(bool isLastIncidentDependency) {
                return isLastIncidentDependency ? @"\-" : "+-";
            }

            protected override string GetIndent(bool isLastIncidentDependency) {
                return "" + (isLastIncidentDependency ? _spaceChar : '|') + _spaceChar;
            }
        }

        private class FlatPathWriterTraverser<TDependency, TItem> : AbstractPathWriterTraverser<TDependency, TItem>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private bool _dontWriteBeforeNextPush = false;
            private readonly List<bool> _isEndStack = new List<bool>();

            public FlatPathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards, IPathMatch<Dependency, Item> countMatch) : base(tw, showItemMarkers, backwards, countMatch) {
                _isEndStack.Add(false);
            }

            protected override void HandleFirstItemBeforeTraverse(TItem firstItem) {
                // empty
            }

            protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
                WritePath(currentPath);
                _tw.WriteLine("<= " + GetItemAsString(tail, false, false));
                _tw.WriteLine();
            }

            protected override DownInfo AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, DownInfo down) {
                DownInfo result = base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, dependencyMatchOrNull, itemMatchOrNull, isEnd, down);
                _isEndStack.Add(isEnd);
                _dontWriteBeforeNextPush = false;
                if (alreadyVisitedLastUsedItemInCurrentPath) {
                    WritePath(currentPath);
                    _tw.WriteLine("<= " + GetItemAsString(currentPath.Peek().UsedItem, false, isEnd));
                    _tw.WriteLine();
                    _dontWriteBeforeNextPush = true;
                }
                return result;
            }

            protected override UpInfo BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd, UpInfo up) {
                if (isEnd && !_dontWriteBeforeNextPush) {
                    WritePath(currentPath);
                    _tw.WriteLine(GetItemAsString(currentPath.Peek().UsedItem, _showItemMarkers, true));
                    _tw.WriteLine();
                    _dontWriteBeforeNextPush = true;
                }
                _isEndStack.RemoveAt(_isEndStack.Count - 1);
                return base.BeforePopDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount, isEnd, up);
            }

            private void WritePath(Stack<TDependency> currentPath) {
                int i = 0;
                foreach (var d in currentPath.Reverse()) {
                    _tw.WriteLine(GetItemAsString(d.UsingItem, _showItemMarkers, _isEndStack[i++]));
                }
            }
        }

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

        private static void Write(bool withHeader, IEnumerable<Dependency> dependencies, TextWriter sw,
                AbstractPathWriterTraverser<Dependency, Item> traverser, ItemMatch pathAnchor,
                [NotNull] IPathMatch<Dependency, Item>[] expectedInnerPathMatches) {
            if (withHeader) {
                sw.WriteLine($"// Written {DateTime.Now} by {typeof(PathWriter).Name} in NDepCheck {Program.VERSION}");
            }
            traverser.Traverse(dependencies, pathAnchor, expectedInnerPathMatches);
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
                AbstractPathWriterTraverser<Dependency, Item> traverser;
                switch (styleOption.ToUpperInvariant()) {
                    case "SPACEINDENT":
                    case "SI":
                        traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw.Writer, " ", showItemMarkers: showItemMarkers, backwards: backwards, countMatch: countMatch);
                        break;
                    case "TABINDENT":
                    case "TI":
                        traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw.Writer, "\t", showItemMarkers: showItemMarkers, backwards: backwards, countMatch: countMatch);
                        break;
                    case "LINEINDENT":
                    case "LI":
                        traverser = new TreePathWriterTraverserWithLineIndent<Dependency, Item>(sw.Writer, ' ', showItemMarkers: showItemMarkers, backwards: backwards, countMatch: countMatch);
                        break;
                    case "FLAT":
                    case "F":
                        traverser = new FlatPathWriterTraverser<Dependency, Item>(sw.Writer, showItemMarkers: showItemMarkers, backwards: backwards, countMatch: countMatch);
                        break;
                    default:
                        throw new ArgumentException($"Style '{styleOption}' not supported for PathWriter");
                }

                Write(true, dependencies, sw.Writer, traverser, pathAnchor, expectedPathMatches.ToArray());
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
                AbstractPathWriterTraverser<Dependency, Item> traverser;
                switch (option) {
                    case "SI":
                        traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw, ".", showItemMarkers: false, backwards: false, countMatch: null);
                        break;
                    case "LI":
                        traverser = new TreePathWriterTraverserWithLineIndent<Dependency, Item>(sw, '.', showItemMarkers: false, backwards: false, countMatch: null);
                        break;
                    case "F":
                        traverser = new FlatPathWriterTraverser<Dependency, Item>(sw, showItemMarkers: false, backwards: false, countMatch: null);
                        break;
                    default:
                        throw new ArgumentException($"option '{option}' not supported");
                }

                Write(false, dependencies, sw, traverser,
                    pathAnchor: new ItemMatch("a:", true),
                    expectedInnerPathMatches: new IPathMatch<Dependency, Item>[] {
                          new ItemPathMatch<Dependency, Item>("~c:", true, multipleOccurrencesAllowed: true, mayContinue: true)
                      });
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

            return new[] {
                FromTo(a, b),
                FromTo(b, c),
                FromTo(c, d),
                FromTo(d, e),
                FromTo(d, b),
                FromTo(e, f),
                FromTo(b, g),
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
