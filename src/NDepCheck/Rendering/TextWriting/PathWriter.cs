using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.TextWriting {
    public class PathWriter : IRenderer {
        private abstract class AbstractPathWriterTraverser<TDependency, TItem> : AbstractDepthFirstPathTraverser<TDependency, TItem>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            protected readonly TextWriter _tw;
            protected readonly bool _showItemMarkers;
            private readonly bool _backwards;
            private readonly HashSet<TItem> _seenPathStarts = new HashSet<TItem>();

            protected AbstractPathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards) : base(retraverseItems: true) {
                _tw = tw;
                _showItemMarkers = showItemMarkers;
                _backwards = backwards;
            }

            public void Traverse(IEnumerable<TDependency> dependencies, List<ItemMatch> pathAnchorsMatches) {
                Dictionary<TItem, TDependency[]> incidentDependencies = _backwards
                    ? AbstractItem<TItem>.CollectIncomingDependenciesMap(dependencies)
                    : AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);
                _seenPathStarts.Clear();

                IEnumerable<TItem> items = pathAnchorsMatches == null
                    ? incidentDependencies.Keys
                    : incidentDependencies.Keys.Where(i => pathAnchorsMatches.Any(m => ItemMatch.IsMatch(m, i)));

                foreach (var i in items.OrderBy(i => i.Name)) {
                    var visitedItem2CheckedPathLengthBehindVisitedItem = new Dictionary<TItem, int>();
                    if (_seenPathStarts.Add(i)) {
                        HandleFirstItemBeforeTraverse(i);
                        Traverse(i, i, false, incidentDependencies, visitedItem2CheckedPathLengthBehindVisitedItem, int.MaxValue);
                    }
                }
            }

            protected abstract void HandleFirstItemBeforeTraverse(TItem firstItem);

            protected override void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount) {
                TItem usedItem = currentPath.Peek().UsedItem;
                _seenPathStarts.Add(usedItem);
            }

            protected void WriteLine(TItem item, bool showMarkers) {
                _tw.WriteLine(showMarkers ? item.AsFullString() : item.AsString());
            }

            protected override void OnFoundCycleToRoot(Stack<TDependency> currentPath) {
                // empty
            }
        }

        private abstract class TreePathWriterTraverser<TDependency, TItem> : AbstractPathWriterTraverser<TDependency, TItem>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            // For advanced ideas, see e.g. https://www.informatik.uni-rostock.de/~ct/pub_files/Tominski06GraphLenses.pdf

            private readonly int _manyPopsLimit;
            private readonly Stack<string> _indents = new Stack<string>();
            private int _popsAfterLastPush;

            protected TreePathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards, 
                                           int manyPopsLimit = 3) : base(tw, showItemMarkers, backwards) {
                _manyPopsLimit = manyPopsLimit;
                _indents.Push("");
            }

            protected override void HandleFirstItemBeforeTraverse(TItem firstItem) {
                WriteLine(firstItem, _showItemMarkers);
            }

            protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
                _tw.WriteLine(_indents.Peek() + GetIndent(true) + "<= " + tail.AsString());
            }

            protected override void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount) {
                base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount);

                TItem usedItem = currentPath.Peek().UsedItem;

                if (alreadyVisitedLastUsedItemInCurrentPath) {
                    _tw.Write(_indents.Peek() + GetLead(incidentIndex == incidentCount - 1));
                    _tw.Write("<= ");
                    WriteLine(usedItem, showMarkers: false);
                    _indents.Push(_indents.Peek() + GetIndent(incidentIndex == incidentCount - 1));
                } else {
                    _tw.Write(_indents.Peek());
                    if (_popsAfterLastPush > _manyPopsLimit) {
                        _tw.Write(GetIndent(false));
                        _tw.WriteLine();
                        _tw.Write(_indents.Peek());
                    }
                    _tw.Write(GetLead(incidentIndex == incidentCount - 1));
                    WriteLine(usedItem, _showItemMarkers);
                    _popsAfterLastPush = 0;
                    _indents.Push(_indents.Peek() + GetIndent(incidentIndex == incidentCount - 1));
                }
            }

            protected abstract string GetLead(bool isLastIncidentDependency);

            protected abstract string GetIndent(bool isLastIncidentDependency);

            protected override void BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount) {
                _indents.Pop();
                _popsAfterLastPush++;
            }

            protected override void OnPathEnd(Stack<TDependency> currentPath) {
                // empty
            }
        }

        private class TreePathWriterTraverserWithFixedIndent<TDependency, TItem> : TreePathWriterTraverser<TDependency, TItem>
                            where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly string _indent;

            public TreePathWriterTraverserWithFixedIndent(TextWriter tw, string indent, bool showItemMarkers, bool backwards, int manyPopsLimit = 3) : base(tw, showItemMarkers, backwards, manyPopsLimit) {
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

            public TreePathWriterTraverserWithLineIndent(TextWriter tw, char spaceChar, bool showItemMarkers, bool backwards, int manyPopsLimit = 3) : base(tw, showItemMarkers, backwards, manyPopsLimit) {
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
            public FlatPathWriterTraverser(TextWriter tw, bool showItemMarkers, bool backwards) : base(tw, showItemMarkers, backwards) {
                // empty
            }

            protected override void HandleFirstItemBeforeTraverse(TItem firstItem) {
                // empty
            }

            protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
                WritePath(currentPath);
                _tw.WriteLine("<= " + tail.AsString());
                _tw.WriteLine();
            }

            protected override void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount) {
                base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath, incidentIndex, incidentCount);

                if (alreadyVisitedLastUsedItemInCurrentPath) {
                    WritePath(currentPath);
                    _tw.WriteLine("<= " + currentPath.Peek().UsedItem.AsString());
                    _tw.WriteLine();
                }
            }

            protected override void BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount) {
                // empty
            }

            protected override void OnPathEnd(Stack<TDependency> currentPath) {
                WritePath(currentPath);
                WriteLine(currentPath.Peek().UsedItem, _showItemMarkers);
                _tw.WriteLine();
            }

            private void WritePath(Stack<TDependency> currentPath) {
                foreach (var d in currentPath.Reverse()) {
                    WriteLine(d.UsingItem, _showItemMarkers);
                }
            }
        }

        public static readonly Option PathAnchorsOption = new Option("pa", "path-anchors", "itempattern", "sources from which paths are written", @default: "all items are path sources", multiple: true);
        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions();
        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);
        public static readonly Option StyleOption = new Option("ps", "path-style", "&", "One of: SpaceIndent or SI, TabIndent or TI, LineIndent or LI, Flat or F", @default: "SI");
        public static readonly Option BackwardsOption = new Option("bw", "upwards", "", "Traverses dependencies in opposite direction", @default: false);
        public static readonly Option ShowItemMarkersOption = new Option("sm", "show-markers", "", "Shows markers on items", @default: false);

        private static readonly Option[] _allOptions = DependencyMatchOptions.WithOptions(PathAnchorsOption, NoExampleInfoOption, StyleOption, BackwardsOption);

        private static void Write(bool withHeader, IEnumerable<Dependency> dependencies, TextWriter sw,
                AbstractPathWriterTraverser<Dependency, Item> traverser, List<ItemMatch> pathAnchorsMatch) {
            if (withHeader) {
                sw.WriteLine($"// Written {DateTime.Now} by {typeof(PathWriter).Name} in NDepCheck {Program.VERSION}");
            }
            traverser.Traverse(dependencies, pathAnchorsMatch);
        }

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount,
                           string argsAsString, string baseFileName, bool ignoreCase) {
            bool showItemMarkers = false;
            bool backwards = false;
            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();
            //int maxCycleLength = int.MaxValue;
            var pathAnchorsMatches = new List<ItemMatch>();
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
                PathAnchorsOption.Action((args, j) => {
                    pathAnchorsMatches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase));
                    return j;
                }),
                StyleOption.Action((args, j) => {
                    styleOption = Option.ExtractRequiredOptionValue(args, ref j, "missing style");
                    return j;
                })
                //MaxCycleLengthOption.Action((args, j) => {
                //    maxCycleLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum cycle length");
                //    return j;
                //})
                );

            //while (pathAnchorsMatches.Count < 2) {
            //    pathAnchorsMatches.Add(new ItemMatch("** ---> **", globalContext.IgnoreCase));
            //}

            using (var sw = GlobalContext.CreateTextWriter(GetMasterFileName(globalContext, argsAsString, baseFileName))) {
                AbstractPathWriterTraverser<Dependency, Item> traverser;
                switch (styleOption.ToUpperInvariant()) {
                    case "SPACEINDENT":
                    case "SI":
                        traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw.Writer, " ", showItemMarkers: showItemMarkers, backwards: backwards);
                        break;
                    case "TABINDENT":
                    case "TI":
                        traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw.Writer, "\t", showItemMarkers: showItemMarkers, backwards: backwards);
                        break;
                    case "LINEINDENT":
                    case "LI":
                        traverser = new TreePathWriterTraverserWithLineIndent<Dependency, Item>(sw.Writer, ' ', showItemMarkers: showItemMarkers, backwards: backwards);
                        break;
                    case "FLAT":
                    case "F":
                        traverser = new FlatPathWriterTraverser<Dependency, Item>(sw.Writer, showItemMarkers: showItemMarkers, backwards: backwards);
                        break;
                    default:
                        throw new ArgumentException($"Style '{styleOption}' not supported for PathWriter");
                }

                Write(true, dependencies, sw.Writer, traverser, pathAnchorsMatches);
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output, string option) {
            using (var sw = new StreamWriter(output)) {
                AbstractPathWriterTraverser<Dependency, Item> traverser;
                switch (option) {
                    case "SI":
                        traverser = new TreePathWriterTraverserWithFixedIndent<Dependency, Item>(sw, ".", showItemMarkers: false, backwards: false);
                        break;
                    case "LI":
                        traverser = new TreePathWriterTraverserWithLineIndent<Dependency, Item>(sw, '.', showItemMarkers: false, backwards: false);
                        break;
                    case "F":
                        traverser = new FlatPathWriterTraverser<Dependency, Item>(sw, showItemMarkers: false, backwards: false);
                        break;
                    default:
                        throw new ArgumentException($"option '{option}' not supported");
                }

                Write(false, dependencies, sw, traverser, pathAnchorsMatch: null);
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
