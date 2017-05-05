using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.TextWriting {
    public class PathWriter : IRenderer {
        private abstract class AbstractPathWriterTraverser : AbstractDepthFirstPathTraverser {
            protected readonly TextWriter _tw;
            private readonly bool _showMarkers;

            protected AbstractPathWriterTraverser(TextWriter tw, bool showMarkers) : base(retraverseItems: true) {
                _tw = tw;
                _showMarkers = showMarkers;
            }

            public void Traverse(IEnumerable<Dependency> dependencies, ItemMatch pathAnchorsMatch) {
                Dictionary<Item, IEnumerable<Dependency>> outgoing = Item.CollectOutgoingDependenciesMap(dependencies);

                foreach (var i in outgoing.Keys.Where(i => ItemMatch.IsMatch(pathAnchorsMatch, i)).OrderBy(i => i.Name)) {
                    var visitedItem2CheckedPathLengthBehindVisitedItem = new Dictionary<Item, int>();
                    WriteItem(i);
                    Traverse(i, i, false, outgoing, visitedItem2CheckedPathLengthBehindVisitedItem, 100000,
                             new HashSet<int>(), WithAddedItemHash(0, i));
                }
            }

            protected void WriteItem(Item item) {
                _tw.WriteLine(_showMarkers ? item.AsFullString() : item.AsString());
            }

            protected override void OnFoundCycleToRoot(Stack<Dependency> currentPath) {
                // empty???????
            }
        }

        private class TreePathWriterTraverser : AbstractPathWriterTraverser {
            private readonly string _indent;
            private readonly int _manyPopsLimit;

            private string _currentIndent = "";
            private int _popsAfterLastPush;

            public TreePathWriterTraverser(TextWriter tw, string indent, bool showMarkers, int manyPopsLimit = 3) : base(tw, showMarkers) {
                _indent = indent;
                _manyPopsLimit = manyPopsLimit;
            }

            protected override void OnTailLoopsBack(Stack<Dependency> currentPath, Item tail) {
                _tw.WriteLine(_currentIndent + _indent + "<= " + tail.AsString());
            }

            protected override void AfterPushDependency(Stack<Dependency> currentPath) {
                _currentIndent += _indent;
                _tw.Write(_currentIndent);
                if (_popsAfterLastPush > _manyPopsLimit) {
                    _tw.WriteLine(_currentIndent);
                }
                WriteItem(currentPath.Peek().UsedItem);
                _popsAfterLastPush = 0;
            }

            protected override void BeforePopDependency(Stack<Dependency> currentPath) {
                _currentIndent = _currentIndent.Substring(_indent.Length);
                _popsAfterLastPush++;
            }

            protected override void OnPathEnd(Stack<Dependency> currentPath) {
                // empty
            }
        }

        private class FlatPathWriterTraverser : AbstractPathWriterTraverser {
            public FlatPathWriterTraverser(TextWriter tw, bool showMarkers) : base(tw, showMarkers) {
                // empty
            }

            protected override void OnTailLoopsBack(Stack<Dependency> currentPath, Item tail) {
                // empty
            }

            protected override void AfterPushDependency(Stack<Dependency> currentPath) {
                // empty
            }

            protected override void BeforePopDependency(Stack<Dependency> currentPath) {
                // empty
            }

            protected override void OnPathEnd(Stack<Dependency> currentPath) {
                _tw.WriteLine();
                foreach (var d in currentPath) {
                    WriteItem(d.UsedItem);
                }
            }
        }

        public static readonly Option PathAnchorsOption = new Option("pa", "path-anchors", "itempattern", "sources from which paths are written", @default: "all items are path sources");
        //public static readonly Option PathEndOption = new Option("pe", "path-ends", "itempattern", "targets from which paths are written", @default: "all items are path sources");
        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions();
        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);
        public static readonly Option StyleOption = new Option("ps", "path-style", "&", "One of: SpaceIndent or SI, TabIndent or TI, LineIndent or LI, Flat or F", @default: "SI");

        private static readonly Option[] _allOptions = DependencyMatchOptions.WithOptions(PathAnchorsOption, NoExampleInfoOption, StyleOption);

        private static void Write(IEnumerable<Dependency> dependencies, TextWriter sw, AbstractPathWriterTraverser traverser, ItemMatch pathAnchorsMatch) {
            sw.WriteLine($"// Written {DateTime.Now} by {typeof(PathWriter).Name} in NDepCheck {Program.VERSION}");

            traverser.Traverse(dependencies, pathAnchorsMatch);
        }

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount,
                           string argsAsString, string baseFileName, bool ignoreCase) {
            //bool showmarkers = false;
            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();
            //int maxCycleLength = int.MaxValue;
            ItemMatch pathAnchorsMatch = null;
            string styleOption = "SI";

            DependencyMatchOptions.Parse(globalContext, argsAsString, globalContext.IgnoreCase, matches, excludes,
                //NoExampleInfoOption.Action((args, j) => {
                //    noExampleInfo = true;
                //    return j;
                //}),
                PathAnchorsOption.Action((args, j) => {
                    pathAnchorsMatch = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"), globalContext.IgnoreCase);
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
                AbstractPathWriterTraverser traverser;
                switch (styleOption.ToUpperInvariant()) {
                    case "SPACEINDENT":
                    case "SI":
                        traverser = new TreePathWriterTraverser(sw.Writer, " ", showMarkers: false);
                        break;
                    case "TABINDENT":
                    case "TI":
                        traverser = new TreePathWriterTraverser(sw.Writer, "\t", showMarkers: false);
                        break;
                    case "LINEINDENT":
                    case "LI":
                        throw new NotImplementedException("LineIndent style not yet implemented");
                    case "FLAT":
                    case "F":
                        traverser = new FlatPathWriterTraverser(sw.Writer, showMarkers: false);
                        break;
                    default:
                        throw new ArgumentException($"Style '{styleOption}' not supported for PathWriter");
                }

                Write(dependencies, sw.Writer, traverser, pathAnchorsMatch);
                ////Log.WriteInfo($"... written {n} dependencies");
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            using (var sw = new StreamWriter(output)) {
                Write(dependencies, sw, traverser: new TreePathWriterTraverser(sw, " ", showMarkers: false), pathAnchorsMatch: null);
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
