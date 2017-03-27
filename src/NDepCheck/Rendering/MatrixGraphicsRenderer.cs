using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using NDepCheck.ConstraintSolving;

namespace NDepCheck.Rendering {
    public class MatrixGraphicsRenderer : GraphicsDependencyRenderer {
        private static string GetName(Item i) {
            return i.Values[0];
        }

        //private static string GetModule(Item i) {
        //    return i.Values[1];
        //}

        //private static string GetOrder(Item i) {
        //    return i.Values[2];
        //}

        private static readonly Font _boxFont = new Font(FontFamily.GenericSansSerif, 10);
        private static readonly Font _lineFont = new Font(FontFamily.GenericSansSerif, 3);

        private Regex _bottomRegex = null;
        private string _title = typeof(ModulesAndInterfacesRenderer).Name;
        private bool _showOnlyReferencedOnBottom = false;
        private bool _showOnlyReferencingOnLeft = false;

        protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
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

            // The itemtype is expected to have 3 fields Name:Module:Order.
            // In the example diagram above, we would have items about like the following:
            //        BAC    :BAC:0100
            //        KST    :KST:0200
            //        KAH    :KAH:0300
            //        Kah.MI :KAH:0301
            //        VKF    :VKF:0400
            //        Vkf1.MI:VKF:0401
            //        Vkf2.MI:VKF:0402
            //        WLG    :WLG:0500
            //        Wlg1.MI:WLG:0501
            //        Wlg2.MI:WLG:0502
            //        IMP    :IMP:0600
            //        Imp.MI :IMP:0601
            //        Top    :TOP:0700

            if (_bottomRegex == null) {
                throw new ApplicationException(nameof(MatrixGraphicsRenderer) + ": -x missing");
            }

            Arrow(F(0, 0), F(100, 0), 1, Color.Chartreuse, "100px", textFont: _lineFont);
            Box(F(-20, -20), _title + "(" + DateTime.Now + ")", boxAnchoring: BoxAnchoring.UpperRight);

            var yItems = new List<Item>();
            var xItems = new List<Item>();

            foreach (var i in items) {
                (_bottomRegex.IsMatch(GetName(i)) ? xItems : yItems).Add(i);
            }

            Dependency[] relevantDependencies = dependencies.Where(d => yItems.Contains(d.UsingItem) && xItems.Contains(d.UsedItem)).ToArray();
            if (_showOnlyReferencedOnBottom) {
                xItems.RemoveAll(ix => !relevantDependencies.Any(d => d.UsedItem.Equals(ix)));
            }
            if (_showOnlyReferencingOnLeft) {
                yItems.RemoveAll(iy => !relevantDependencies.Any(d => d.UsingItem.Equals(iy)));
            }

            xItems.Sort((i1, i2) => Sum(relevantDependencies, d => d.UsedItem.Equals(i2)) - Sum(relevantDependencies, d => d.UsedItem.Equals(i1)));
            yItems.Sort((i1, i2) => Sum(relevantDependencies, d => d.UsingItem.Equals(i1)) - Sum(relevantDependencies, d => d.UsingItem.Equals(i2)));

            double x = 50;
            foreach (var ix in xItems) {
                string name = GetName(ix);
                var xPos = new VariableVector(name + ".POS", Solver);
                IBox box = Box(xPos, boxAnchoring: BoxAnchoring.LowerLeft, text: name, borderWidth: 3, boxColor: Color.LemonChiffon, boxTextPlacement: BoxTextPlacement.LeftUp,
                                   textFont: _boxFont, drawingOrder: 1, fixingOrder: 4);
                xPos.SetX(x).SetY(-box.TextBox.Y);
                ix.DynamicData.Box = box;

                x += 40;
            }

            const int DELTA_Y_MAIN = 12;
            NumericVariable y = Solver.CreateConstant("y", 10);
            foreach (var iy in yItems) {
                string name = GetName(iy);
                var yPos = new VariableVector(name + ".POS", Solver);
                IBox box = Box(yPos, boxAnchoring: BoxAnchoring.LowerRight, text: name, borderWidth: 3, boxColor: Color.Coral,
                    boxTextPlacement: BoxTextPlacement.Left,
                    textFont: _boxFont, drawingOrder: 1, fixingOrder: 4);
                yPos.SetX(0).SetY(y);

                double minBoxHeight = 30;
                foreach (var d in relevantDependencies.Where(d => iy.Equals(d.UsingItem))) {
                    IBox usedBox = d.UsedItem.DynamicData.Box;
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

        //private string SumAsString(IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
        //    int ct = Sum(dependencies, filter);
        //    return ct >= 1000000 ? ct / 1000 + "M" : ct >= 1000 ? ct / 1000 + "K" : "" + ct;
        //}

        private static int Sum(IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
            return dependencies.Where(filter).Sum(d => d.Ct);
        }

        public override void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType amo = ItemType.New("AMO:Assembly:Module:Order");

            var bac = Item.New(amo, "BAC:BAC:0100".Split(':'));
            var kst = Item.New(amo, "KST:KST:0200".Split(':'));
            var kah = Item.New(amo, "KAH:KAH:0300".Split(':'));
            var kah_mi = Item.New(amo, "Kah.MI:KAH:0301".Split(':'));
            var vkf = Item.New(amo, "VKF:VKF:0400".Split(':'));
            var vkf1_mi = Item.New(amo, "Vkf1.MI:VKF:0401".Split(':'));
            var vkf2_mi = Item.New(amo, "Vkf2.MI:VKF:0402".Split(':'));
            var vkf3_mi = Item.New(amo, "Vkf3.MI:VKF:0402".Split(':'));
            var vkf4_mi = Item.New(amo, "Vkf4.MI:VKF:0402".Split(':'));
            var wlg = Item.New(amo, "WLG:WLG:0500".Split(':'));
            var wlg1_mi = Item.New(amo, "Wlg1.MI:WLG:0501".Split(':'));
            var wlg2_mi = Item.New(amo, "Wlg2.MI:WLG:0502".Split(':'));
            var imp = Item.New(amo, "IMP:IMP:0600".Split(':'));
            var imp_mi = Item.New(amo, "Imp.MI:IMP:0601".Split(':'));
            var top = Item.New(amo, "Top:TOP:0700".Split(':'));

            items = new[] { bac, kst, kah, kah_mi, vkf, vkf1_mi, vkf2_mi, vkf3_mi, vkf4_mi, wlg, wlg1_mi, wlg2_mi, imp, imp_mi, top };

            dependencies = new[] {
                    FromTo(kst, bac), FromTo(kst, kah_mi), FromTo(kst, vkf1_mi), FromTo(kst, vkf2_mi), FromTo(kst, wlg1_mi), FromTo(kst, wlg2_mi),
                    FromTo(kah, bac), FromTo(kah, vkf1_mi), FromTo(kah, vkf2_mi), FromTo(kah, wlg, 4, 3) /* ===> */,
                    FromTo(vkf, bac), FromTo(vkf, kst), FromTo(vkf, kah, 3), FromTo(vkf, kah_mi, 2, 2) /* <:: */, FromTo(vkf, imp_mi), FromTo(vkf1_mi, bac), FromTo(vkf2_mi, bac),
                    // ... more to come
                };

            // Put vkf on x axis, rest on y
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionableCt = 0) {
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionableCt, exampleInfo: questionableCt > 0 ? from + "==>" + to : "");
        }

        public override void Render(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string argsAsString, string baseFilename) {
            DoRender(items, dependencies, argsAsString, baseFilename,
                new OptionAction('b', (args, j) => {
                    _bottomRegex = new Regex(Options.ExtractOptionValue(args, ref j));
                    return j;
                }),
                new OptionAction('t', (args, j) => {
                    _title = Options.ExtractOptionValue(args, ref j);
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

  Options: -b & [-t &] [-x] [-y] " + GetHelpUsage() + @"
    -b &          regexp for items on bottom
    -t &          title text shown in diagram
    -x            do not show non-referenced items on bottom
    -y            do not show non-referencing items on left side
" + GetHelpExplanations();
        }
    }
}
