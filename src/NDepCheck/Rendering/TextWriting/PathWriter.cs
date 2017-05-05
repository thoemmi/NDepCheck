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
            protected readonly bool _showMarkers;
            private readonly HashSet<TItem> _seenPathStarts = new HashSet<TItem>();

            protected AbstractPathWriterTraverser(TextWriter tw, bool showMarkers) : base(retraverseItems: true) {
                _tw = tw;
                _showMarkers = showMarkers;
            }

            public void Traverse(IEnumerable<TDependency> dependencies, List<ItemMatch> pathAnchorsMatches) {
                Dictionary<TItem, IEnumerable<TDependency>> outgoing = AbstractItem<TItem>.CollectOutgoingDependenciesMap(dependencies);
                _seenPathStarts.Clear();

                IEnumerable<TItem> items = pathAnchorsMatches == null
                    ? outgoing.Keys
                    : outgoing.Keys.Where(i => pathAnchorsMatches.Any(m => ItemMatch.IsMatch(m, i)));

                foreach (var i in items.OrderBy(i => i.Name)) {
                    var visitedItem2CheckedPathLengthBehindVisitedItem = new Dictionary<TItem, int>();
                    if (_seenPathStarts.Add(i)) {
                        HandleFirstItemBeforeTraverse(i);
                        Traverse(i, i, false, outgoing, visitedItem2CheckedPathLengthBehindVisitedItem, int.MaxValue);
                    }
                }
            }

            protected abstract void HandleFirstItemBeforeTraverse(TItem firstItem);

            protected override void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath) {
                TItem usedItem = currentPath.Peek().UsedItem;
                _seenPathStarts.Add(usedItem);
            }

            protected void WriteItem(TItem item, bool showMarkers) {
                _tw.WriteLine(showMarkers ? item.AsFullString() : item.AsString());
            }

            protected override void OnFoundCycleToRoot(Stack<TDependency> currentPath) {
                // empty
            }
        }

        private class TreePathWriterTraverser<TDependency, TItem> : AbstractPathWriterTraverser<TDependency, TItem>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            private readonly string _indent;
            private readonly int _manyPopsLimit;

            private string _currentIndent = "";
            private int _popsAfterLastPush;

            public TreePathWriterTraverser(TextWriter tw, string indent, bool showMarkers, int manyPopsLimit = 3) : base(tw, showMarkers) {
                _indent = indent;
                _manyPopsLimit = manyPopsLimit;
            }

            protected override void HandleFirstItemBeforeTraverse(TItem firstItem) {
                WriteItem(firstItem, _showMarkers);
            }

            protected override void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail) {
                _tw.WriteLine(_currentIndent + _indent + "<= " + tail.AsString());
            }

            protected override void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath) {
                base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath);

                TItem usedItem = currentPath.Peek().UsedItem;
                _currentIndent += _indent;
                _tw.Write(_currentIndent);
                if (alreadyVisitedLastUsedItemInCurrentPath) {
                    _tw.Write("<= ");
                    WriteItem(usedItem, showMarkers: false);
                } else {
                    if (_popsAfterLastPush > _manyPopsLimit) {
                        _tw.WriteLine();
                        _tw.Write(_currentIndent);
                    }
                    WriteItem(usedItem, _showMarkers);
                    _popsAfterLastPush = 0;
                }
            }

            protected override void BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath) {
                _currentIndent = _currentIndent.Substring(_indent.Length);
                _popsAfterLastPush++;
            }

            protected override void OnPathEnd(Stack<TDependency> currentPath) {
                // empty
            }
        }

        private class FlatPathWriterTraverser<TDependency, TItem> : AbstractPathWriterTraverser<TDependency, TItem>
                where TDependency : AbstractDependency<TItem>
                where TItem : AbstractItem<TItem> {
            public FlatPathWriterTraverser(TextWriter tw, bool showMarkers) : base(tw, showMarkers) {
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

            protected override void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath) {
                base.AfterPushDependency(currentPath, alreadyVisitedLastUsedItemInCurrentPath);

                if (alreadyVisitedLastUsedItemInCurrentPath) {
                    WritePath(currentPath);
                    _tw.WriteLine("<= " + currentPath.Peek().UsedItem.AsString());
                    _tw.WriteLine();
                }
            }

            protected override void BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath) {
                // empty
            }

            protected override void OnPathEnd(Stack<TDependency> currentPath) {
                WritePath(currentPath);
                WriteItem(currentPath.Peek().UsedItem, _showMarkers);
                _tw.WriteLine();
            }

            private void WritePath(Stack<TDependency> currentPath) {
                foreach (var d in currentPath.Reverse()) {
                    WriteItem(d.UsingItem, _showMarkers);
                }
            }
        }

        public static readonly Option PathAnchorsOption = new Option("pa", "path-anchors", "itempattern", "sources from which paths are written", @default: "all items are path sources");
        //public static readonly Option PathEndOption = new Option("pe", "path-ends", "itempattern", "targets from which paths are written", @default: "all items are path sources");
        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions();
        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);
        public static readonly Option StyleOption = new Option("ps", "path-style", "&", "One of: SpaceIndent or SI, TabIndent or TI, LineIndent or LI, Flat or F", @default: "SI");

        private static readonly Option[] _allOptions = DependencyMatchOptions.WithOptions(PathAnchorsOption, NoExampleInfoOption, StyleOption);

        private static void Write(bool withHeader, IEnumerable<Dependency> dependencies, TextWriter sw,
                AbstractPathWriterTraverser<Dependency, Item> traverser, List<ItemMatch> pathAnchorsMatch) {
            if (withHeader) {
                sw.WriteLine($"// Written {DateTime.Now} by {typeof(PathWriter).Name} in NDepCheck {Program.VERSION}");
            }
            traverser.Traverse(dependencies, pathAnchorsMatch);
        }

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount,
                           string argsAsString, string baseFileName, bool ignoreCase) {
            //bool showmarkers = false;
            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();
            //int maxCycleLength = int.MaxValue;
            var pathAnchorsMatches = new List<ItemMatch>();
            string styleOption = "SI";

            DependencyMatchOptions.Parse(globalContext, argsAsString, globalContext.IgnoreCase, matches, excludes,
                //NoExampleInfoOption.Action((args, j) => {
                //    noExampleInfo = true;
                //    return j;
                //}),
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
            using (var sw = GlobalContext.CreateTextWriter(GetMasterFileName(globalContext, argsAsString, baseFileName))) {
                AbstractPathWriterTraverser<Dependency, Item> traverser;
                switch (styleOption.ToUpperInvariant()) {
                    case "SPACEINDENT":
                    case "SI":
                        traverser = new TreePathWriterTraverser<Dependency, Item>(sw.Writer, " ", showMarkers: false);
                        break;
                    case "TABINDENT":
                    case "TI":
                        traverser = new TreePathWriterTraverser<Dependency, Item>(sw.Writer, "\t", showMarkers: false);
                        break;
                    case "LINEINDENT":
                    case "LI":
                        throw new NotImplementedException("LineIndent style not yet implemented");
                    case "FLAT":
                    case "F":
                        traverser = new FlatPathWriterTraverser<Dependency, Item>(sw.Writer, showMarkers: false);
                        break;
                    default:
                        throw new ArgumentException($"Style '{styleOption}' not supported for PathWriter");
                }

                Write(true, dependencies, sw.Writer, traverser, pathAnchorsMatches);
                ////Log.WriteInfo($"... written {n} dependencies");
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output, string option) {
            using (var sw = new StreamWriter(output)) {
                AbstractPathWriterTraverser<Dependency, Item> traverser;
                switch (option) {
                    case "SI":
                        traverser = new TreePathWriterTraverser<Dependency, Item>(sw, " ", showMarkers: false);
                        break;
                    case "F":
                        traverser = new FlatPathWriterTraverser<Dependency, Item>(sw, showMarkers: false);
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
