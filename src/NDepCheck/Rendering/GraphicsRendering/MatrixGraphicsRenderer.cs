using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.ConstraintSolving;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.GraphicsRendering {
    public class MatrixGraphicsRenderer : GraphicsRenderer {
        private static string GetName(Item i) {
            return i.Values[0];
        }

        private static string GetXHtmlRef(Item i) {
            return i.Values.Length > 1 ? i.Values[1] : null;
        }

        private static string GetYHtmlRef(Item i) {
            return i.Values.Length > 2 ? i.Values[2] : null;
        }

        private static readonly Font _boxFont = new Font(FontFamily.GenericSansSerif, 10);
        private static readonly Font _lineFont = new Font(FontFamily.GenericSansSerif, 3);

        [CanBeNull]
        private ItemMatch _bottomItemMatch = null;

        [NotNull]
        private OrderSupport _orderSupport = new OrderSupport(item => null);
        private bool _showOnlyReferencedOnBottom = false;
        private bool _showOnlyReferencingOnLeft = false;

        protected override void PlaceObjects([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies) {
            // ASCII-art sketch of what I want to accomplish:
            //   +-----+
            //   |     |--------------------------------------------->|
            //   | VKF |--------->|                                   |
            //   +-----+          |                                   |
            //                    |                                   |
            //   +-----+          |                                   |
            //   |     |--------------------------------------------->|
            //   | WLG |--------------------->|                       |
            //   +-----+          |           |                       |
            //                    |           |                       |
            //   +-----+          |           |                       |
            //   |     |--------------------------------->|           |
            //   | KST |--------------------->|           |           |
            //   |     |--------->|           |           |           |
            //   +-----+          |           |           |           |
            //                +-------+   +-------+   +-------+   +-------+
            //                | IMP.A |   | IMP.B |   | IMP.C |   | IMP.D |
            //                +-------+   +-------+   +-------+   +-------+
            //

            // The itemtype is expected to have 1 or 2 fields Name[:HtmlRef]
            // In the example diagram above, we would have items about like the following:
            //        BAC:
            //        WLG:
            //        KST:
            //        IMP.A:
            //        IMP.B:
            //        IMP.C:
            //        IMP.D:

            Arrow(F(0, 0), F(100, 0), 1, Color.Chartreuse, "100px", textFont: _lineFont);
            Box(F(-20, -20), _title + "(" + DateTime.Now + ")", boxAnchoring: BoxAnchoring.UpperRight);

            List<Item> yItems = dependencies.Select(e => e.UsingItem).Distinct().ToList();
            List<Item> xItems = dependencies.Select(e => e.UsedItem).Distinct().
                Where(i => _bottomItemMatch == null || _bottomItemMatch.Matches(i).Success).
                ToList();

            Dependency[] relevantDependencies = dependencies.Where(d => yItems.Contains(d.UsingItem) && xItems.Contains(d.UsedItem)).ToArray();
            if (_showOnlyReferencedOnBottom) {
                xItems.RemoveAll(ix => !relevantDependencies.Any(d => d.UsedItem.Equals(ix)));
            }
            if (_showOnlyReferencingOnLeft) {
                yItems.RemoveAll(iy => !relevantDependencies.Any(d => d.UsingItem.Equals(iy)));
            }

            _orderSupport.SortWithEdgeCount(xItems, relevantDependencies, (i, d) => d.UsedItem.Equals(i));
            _orderSupport.SortWithEdgeCount(yItems, relevantDependencies, (i, d) => d.UsingItem.Equals(i));

            double x = 100;
            var xBoxes = new Dictionary<Item, IBox>();

            foreach (var ix in xItems) {
                string name = GetName(ix);
                var xPos = new VariableVector(name + ".POS", Solver);
                IBox box = Box(xPos, boxAnchoring: BoxAnchoring.LowerLeft, text: name, borderWidth: 3, boxColor: Color.LemonChiffon, boxTextPlacement: BoxTextPlacement.LeftUp,
                                   textFont: _boxFont, drawingOrder: 1, fixingOrder: 4, htmlRef: GetXHtmlRef(ix));
                xPos.SetX(x).SetY(-box.TextBox.Y);
                xBoxes[ix] = box;

                x += 40;
            }

            const int DELTA_Y_MAIN = 12;
            NumericVariable y = Solver.CreateConstant("y", 10);
            foreach (var iy in yItems) {
                string name = GetName(iy);
                var yPos = new VariableVector(name + ".POS", Solver);
                IBox box = Box(yPos, boxAnchoring: BoxAnchoring.LowerRight, text: name, borderWidth: 3, boxColor: Color.Coral,
                    boxTextPlacement: BoxTextPlacement.Left, textFont: _boxFont, drawingOrder: 1, fixingOrder: 4,
                    htmlRef: GetYHtmlRef(iy) ?? GetXHtmlRef(iy));
                yPos.SetX(0).SetY(y);

                double minBoxHeight = 45;
                foreach (var d in relevantDependencies.Where(d => iy.Equals(d.UsingItem))) {
                    IBox usedBox = xBoxes[d.UsedItem];
                    yPos += F(0, 3);
                    Arrow(yPos, new VariableVector(name + "->...", usedBox.LowerLeft.X, yPos.Y), width: 2,
                        color: d.NotOkCt > 0 ? Color.Red : d.QuestionableCt > 0 ? Color.Blue : Color.Black,
                        text: "#=" + d.Ct, placement: LineTextPlacement.Left, textLocation: -85,
                        edgeInfo: d.ExampleInfo, drawingOrder: 1);
                    usedBox.UpperRight.MinY(yPos.Y + 5);
                    yPos += F(0, DELTA_Y_MAIN);
                    minBoxHeight += DELTA_Y_MAIN;
                    box.UpperRight.MinY(yPos.Y);
                }
                box.Diagonal.MinY(minBoxHeight);
                //Console.WriteLine(name + ".LR.Y=" + y + " .H>=" + minBoxHeight + "  H.ShortName=" + box.Diagonal.Y.ShortName);
                y += minBoxHeight + 10;
            }

            //string countText = "\n<" + SumAsString(dependencies, d => d.UsingItem.Equals(i) && !d.UsedItem.Equals(i))
            //                   + " =" + SumAsString(dependencies, d => d.UsingItem.Equals(i) && d.UsedItem.Equals(i))
            //                   + " >" + SumAsString(dependencies, d => !d.UsingItem.Equals(i) && d.UsedItem.Equals(i));
            // TODO: Add option and computation to split this into .,?,!
        }

        //private string SumAsString([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
        //    int ct = Sum(dependencies, filter);
        //    return ct >= 1000000 ? ct / 1000 + "M" : ct >= 1000 ? ct / 1000 + "K" : "" + ct;
        //}

        public override IEnumerable<Dependency> CreateSomeTestDependencies() {
            ItemType ar = ItemType.New("AR(Assembly:Ref)");

            var wlg = Item.New(ar, "WLG:1".Split(':'));
            var kst = Item.New(ar, "KST:2".Split(':'));
            var vkf = Item.New(ar, "VKF:3".Split(':'));
            var impA = Item.New(ar, "IMP.A:4".Split(':'));
            var impB = Item.New(ar, "IMP.B:5".Split(':'));
            var impC = Item.New(ar, "IMP.C:6".Split(':'));
            var impD = Item.New(ar, "IMP.D:".Split(':'));

            return new[] {
                    FromTo(vkf, impA), FromTo(vkf, impD),
                    FromTo(wlg, impB), FromTo(wlg, impD),
                    FromTo(kst, impA), FromTo(kst, impB), FromTo(kst, impC)
                };

            // Put vkf on x axis, rest on y
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionableCt = 0) {
            return new Dependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionableCt, exampleInfo: questionableCt > 0 ? from + "==>" + to : "");
        }

        public static readonly Option BottomRegexOption = new Option("pb", "place-on-bottom", "&", "Regex to select elements to place on bottom", @default: "all items on both axes");
        public static readonly Option OrderFieldOption = OrderSupport.CreateOption();
        public static readonly Option NoEmptiesOnBottomOption = new Option("nb", "no-empties-on-bottom", "", "Do not show non-referenced items on bottom", @default: "show all");
        public static readonly Option NoEmptiesOnLeftOption = new Option("nl", "no-empties-on-left", "", "Do not show non-referenced items on left side", @default: "show all");

        protected static readonly Option[] _allOptions = _allGraphicsRendererOptions
                        .Concat(new[] { BottomRegexOption, OrderFieldOption, NoEmptiesOnBottomOption, NoEmptiesOnLeftOption }).ToArray();

        public override void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, int? dependenciesCount, string argsAsString, [NotNull] WriteTarget target, bool ignoreCase) {
            DoRender(globalContext, dependencies, argsAsString, target,
                BottomRegexOption.Action((args, j) => {
                    string pattern = Option.ExtractRequiredOptionValue(args, ref j, "Pattern for selection of bottom items missing");
                    _bottomItemMatch = new ItemMatch(null, pattern, 0, ignoreCase);
                    return j;
                }),
                OrderFieldOption.Action((args, j) => {
                    string orderPattern = Option.ExtractRequiredOptionValue(args, ref j, "order field missing");
                    _orderSupport = OrderSupport.Create(orderPattern, ignoreCase);
                    return j;
                }),
                NoEmptiesOnBottomOption.Action((args, j) => {
                    _showOnlyReferencedOnBottom = true;
                    return j;
                }),
                NoEmptiesOnLeftOption.Action((args, j) => {
                    _showOnlyReferencingOnLeft = true;
                    return j;
                }));
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            return $@"  A GIF renderer that depicts modules and their interfaces as
  vertical bars that are connected by horizontal arrows.
  Item format: Name[:XHtmlRef[:YHtmlRef]]
  XHtmlRef and YHtmlRef, if present, are placed behind the boxes as links to click.

  {GetHelpExplanations()}

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }
    }
}
