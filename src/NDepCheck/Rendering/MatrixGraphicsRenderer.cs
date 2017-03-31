using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.ConstraintSolving;

namespace NDepCheck.Rendering {
    public class MatrixGraphicsRenderer : GraphicsDependencyRenderer {
        private static string GetName(Item i) {
            return i.Values[0];
        }

        private static string GetXHtmlRef(Item i) {
            return i.Values.Length > 1 ? i.Values[1] : null;
        }

        private static string GetYHtmlRef(Item i) {
            return i.Values.Length > 2 ? i.Values[2] : null;
        }

        [NotNull]
        private string GetOrder(Item i) {
            return (_orderField < 0 || i.Values.Length <= _orderField ? i.Order : i.Values[_orderField]) ?? "";
        }

        private static readonly Font _boxFont = new Font(FontFamily.GenericSansSerif, 10);
        private static readonly Font _lineFont = new Font(FontFamily.GenericSansSerif, 3);

        private Regex _bottomRegex = null;
        private string _title = typeof(ModulesAndInterfacesRenderer).Name;
        private int _orderField = 0;
        private bool _showOnlyReferencedOnBottom = false;
        private bool _showOnlyReferencingOnLeft = false;

        protected override void PlaceObjects(IEnumerable<Dependency> dependencies) {
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
                Where(i => _bottomRegex == null || _bottomRegex.IsMatch(GetName(i))).
                ToList();

            Dependency[] relevantDependencies = dependencies.Where(d => yItems.Contains(d.UsingItem) && xItems.Contains(d.UsedItem)).ToArray();
            if (_showOnlyReferencedOnBottom) {
                xItems.RemoveAll(ix => !relevantDependencies.Any(d => d.UsedItem.Equals(ix)));
            }
            if (_showOnlyReferencingOnLeft) {
                yItems.RemoveAll(iy => !relevantDependencies.Any(d => d.UsingItem.Equals(iy)));
            }

            Sort(xItems, relevantDependencies, (i, d) => d.UsedItem.Equals(i));
            Sort(yItems, relevantDependencies, (i, d) => d.UsingItem.Equals(i));

            double x = 50;
            foreach (var ix in xItems) {
                string name = GetName(ix);
                var xPos = new VariableVector(name + ".POS", Solver);
                IBox box = Box(xPos, boxAnchoring: BoxAnchoring.LowerLeft, text: name, borderWidth: 3, boxColor: Color.LemonChiffon, boxTextPlacement: BoxTextPlacement.LeftUp,
                                   textFont: _boxFont, drawingOrder: 1, fixingOrder: 4, htmlRef: GetXHtmlRef(ix));
                xPos.SetX(x).SetY(-box.TextBox.Y);
                ix.DynamicData.XBox = box;

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

                double minBoxHeight = 30;
                foreach (var d in relevantDependencies.Where(d => iy.Equals(d.UsingItem))) {
                    IBox usedBox = d.UsedItem.DynamicData.XBox;
                    yPos += F(0, 3);
                    Arrow(yPos, new VariableVector(name + "->...", usedBox.LowerLeft.X, yPos.Y), width: 2,
                        color: d.NotOkCt > 0 ? Color.Red : d.QuestionableCt > 0 ? Color.Blue : Color.Black,
                        text: "#=" + d.Ct, placement: LineTextPlacement.Left, textLocation: -40,
                        edgeInfo: d.ExampleInfo, drawingOrder: 1);
                    usedBox.UpperRight.MinY(yPos.Y + 5);
                    yPos += F(0, DELTA_Y_MAIN);
                    minBoxHeight += DELTA_Y_MAIN;
                }
                box.Diagonal.MinY(minBoxHeight);
                //Console.WriteLine(name + ".H>=" + minBoxHeight);
                y += minBoxHeight + 10;
            }

            //string countText = "\n<" + SumAsString(dependencies, d => d.UsingItem.Equals(i) && !d.UsedItem.Equals(i))
            //                   + " =" + SumAsString(dependencies, d => d.UsingItem.Equals(i) && d.UsedItem.Equals(i))
            //                   + " >" + SumAsString(dependencies, d => !d.UsingItem.Equals(i) && d.UsedItem.Equals(i));
            // TODO: Add option and computation to split this into .,?,!
        }

        private void Sort(List<Item> list, Dependency[] relevantDependencies, Func<Item, Dependency, bool> filter) {
            list.Sort((i1, i2) => GetOrder(i1) != GetOrder(i2)
                        ? string.Compare(GetOrder(i1), GetOrder(i2), StringComparison.Ordinal)
                        : Sum(relevantDependencies, d => filter(i2, d)) - Sum(relevantDependencies, d => filter(i1, d)));
        }

        //private string SumAsString(IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
        //    int ct = Sum(dependencies, filter);
        //    return ct >= 1000000 ? ct / 1000 + "M" : ct >= 1000 ? ct / 1000 + "K" : "" + ct;
        //}

        private static int Sum(IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
            return dependencies.Where(filter).Sum(d => d.Ct);
        }

        public override void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType ar = ItemType.New("AR:Assembly:Ref");

            var wlg = Item.New(ar, "WLG:1".Split(':'));
            var kst = Item.New(ar, "KST:2".Split(':'));
            var vkf = Item.New(ar, "VKF:3".Split(':'));
            var impA = Item.New(ar, "IMP.A:4".Split(':'));
            var impB = Item.New(ar, "IMP.B:5".Split(':'));
            var impC = Item.New(ar, "IMP.C:6".Split(':'));
            var impD = Item.New(ar, "IMP.D:".Split(':'));

            items = new[] { vkf, kst, wlg, impA, impB, impC, impD };

            dependencies = new[] {
                    FromTo(vkf, impA), FromTo(vkf, impD),
                    FromTo(wlg, impB), FromTo(wlg, impD),
                    FromTo(kst, impA), FromTo(kst, impB), FromTo(kst, impC)
                };

            // Put vkf on x axis, rest on y
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionableCt = 0) {
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionableCt, exampleInfo: questionableCt > 0 ? from + "==>" + to : "");
        }

        public override void Render(IEnumerable<Dependency> dependencies, string argsAsString, string baseFileName) {
            DoRender(dependencies, argsAsString, baseFileName,
                new OptionAction('b', (args, j) => {
                    _bottomRegex = new Regex(Options.ExtractOptionValue(args, ref j));
                    return j;
                }),
                new OptionAction('t', (args, j) => {
                    _title = Options.ExtractOptionValue(args, ref j);
                    return j;
                }),
                new OptionAction('o', (args, j) => {
                    string orderField = Options.ExtractOptionValue(args, ref j);
                    if (!int.TryParse(orderField, out _orderField) || _orderField < 0) {
                        Options.Throw("No valid field index after -o", args);
                    }
                    return j;
                }),
                new OptionAction('x', (args, j) => {
                    _showOnlyReferencedOnBottom = true;
                    return j;
                }),
                new OptionAction('y', (args, j) => {
                    _showOnlyReferencingOnLeft = true;
                    return j;
                }));
        }

        public override string GetHelp(bool detailedHelp) {
            return @"  A GIF renderer that depicts modules and their interfaces as
  vertical bars that are connected by horizontal arrows.
  Item format: Name[:XHtmlRef[:YHtmlRef]]
  XHtmlRef and YHtmlRef, if present, are placed behind the boxes as links to click.

  Options: [-b &] [-t &] [-o #] [-x] [-y] " + GetHelpUsage() + @"
    -b &          regexp for items on bottom; default: all items on both axes
    -t &          title text shown in diagram; default: 'ModulesAndInterfacesRenderer'
    -o #          field on which items are sorted, counted from 1 up.
                  Items with equal order are sorted by edge count.
                  Default: internal order field, then edge count.
    -x            do not show non-referenced items on bottom; default: show all
    -y            do not show non-referencing items on left side; default: show all
" + GetHelpExplanations();
        }
    }
}
