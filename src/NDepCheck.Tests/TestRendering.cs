using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.ConstraintSolving;
using NDepCheck.Rendering;

namespace NDepCheck.Tests {

    [TestClass]
    public class TestRendering {

        #region Simple tests

        internal class DelegteTestRenderer : GraphicsRenderer<Item, Dependency> {
            private readonly Action<DelegteTestRenderer> _placeObjects;

            public DelegteTestRenderer(Action<DelegteTestRenderer> placeObjects) {
                _placeObjects = placeObjects;
            }

            protected override Color GetBackGroundColor => Color.Yellow;

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                _placeObjects(this);
            }

            public override void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
                ItemType simple = ItemType.New("Simple:Name");
                Item i1 = Item.New(simple, "I1");
                Item i2 = Item.New(simple, "I2");
                items = new[] { i1, Item.New(simple, "I2") };
                dependencies = new[] { new Dependency(i1, i1, "Test", 0, 0), new Dependency(i1, i2, "Test", 0, 0) };
            }
        }

        private static void CreateAndRender(Action<DelegteTestRenderer> placeObjects) {
            string tempFile = Path.GetTempFileName();
            Console.WriteLine(Path.ChangeExtension(tempFile, ".gif"));
            new DelegteTestRenderer(placeObjects).Render(Enumerable.Empty<Item>(), Enumerable.Empty<Dependency>(), tempFile);
        }

        [TestMethod]
        public void TestSingleBox() {
            CreateAndRender(r => r.Box(r.F(100, 100), "B", r.F(70, 200),
                borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 30)));
        }

        [TestMethod]
        public void TestSingleBoxWithText() {
            CreateAndRender(r => r.Box(r.F(100, 100), "A long text", r.F(null, 200),
                borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 30)));
        }

        [TestMethod]
        public void TestBoxesWithText() {
            CreateAndRender(r => {
                var pos = r.F(0, 0);
                foreach (BoxTextPlacement e in typeof(BoxTextPlacement).GetEnumValues()) {
                    r.Box(pos, e.ToString(), r.F(90, 200), borderWidth: 1,
                        boxTextPlacement: e, textFont: new Font(FontFamily.GenericSansSerif, 8));
                    pos += r.F(100, 5);
                }
            });
        }

        [TestMethod]
        public void TestLinesWithText() {
            CreateAndRender(r => {
                var tail = r.F(0, 0);
                var head = r.F(100, 120);
                var delta = head;
                foreach (LineTextPlacement e in typeof(LineTextPlacement).GetEnumValues()) {
                    r.Arrow(tail, head, 3, placement: e, text: e.ToString(), textFont: new Font(FontFamily.GenericSansSerif, 8), textPadding: 0.5);
                    tail = head;
                    delta = !delta;
                    head += delta;
                }
            });
        }

        [TestMethod]
        public void TestLinesWithMovedText() {
            CreateAndRender(r => {
                r.Arrow(r.F(0, 0, "Leg0"), r.F(100, 0, "Leg100"), 1, color: Color.Chartreuse);

                VariableVector tail = r.F(0, 0, "V0");
                VariableVector head = r.F(100, 120, "V1");
                VariableVector delta = head;
                foreach (var e in typeof(LineTextPlacement).GetEnumValues().OfType<LineTextPlacement>()) {
                    r.Arrow(tail, head, 3, placement: e, text: e.ToString(), textFont: new Font(FontFamily.GenericSansSerif, 8), textLocation: 0.3, name: "text at " + e);
                    tail = head;
                    delta = !delta;
                    head += delta;
                }
            });
        }

        [TestMethod]
        public void TestSingleArrow() {
            CreateAndRender(r => r.Arrow(r.F(30, -100), r.F(170, 300), 10, Color.Red));
        }

        [TestMethod]
        public void TestThreeArrows() {
            CreateAndRender(r => {
                r.Arrow(r.F(0, 0), r.F(100, 0), 2, Color.Green);
                r.Arrow(r.F(0, 0), r.F(0, 100), 2, Color.Green);
                r.Arrow(r.F(30, -100), r.F(170, 300), 10, Color.Red);
            });
        }

        [TestMethod]
        public void TestSingleBoxWithAnchors() {
            const int N = 1;//13;
            const int ANCHORS = 5; //10;

            CreateAndRender(r => {
                var b = r.Box(r.F(0, 0), "A long text", r.F(null, 40), borderWidth: 10,
                    textFont: new Font(FontFamily.GenericSansSerif, 30), connectors: ANCHORS-2, name: "BOX");

                for (int i = 0; i < N; i++) {
                    var angle = 2 * Math.PI * i / N;
                    var farAway = r.F(300 * Math.Sin(angle), 300 * Math.Cos(angle));
                    r.Arrow(farAway, b.GetBestConnector(farAway), 2 + i, name: "LINE_" + i);
                }
            });
        }

        [TestMethod]
        public void TestSingleAnchor() {
            CreateAndRender(r => {
                IBox box = r.Box(r.F(100, 100), "B", r.F(70, 200),
                    borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 100));
                VariableVector far = r.F(300, 400);
                r.Arrow(box.Center, far, width: 5, color: Color.Blue); // center to far point
                r.Arrow(box.Center, r.F(400, 400), width: 2, color: Color.Green); // 45° diagonal from center
                r.Arrow(box.GetBestConnector(far), far, width: 10, color: Color.Brown); // anchor to far point
            });
        }

        [TestMethod]
        public void TestWindroseCenter() {
            TestWindrose(BoxAnchoring.Center);
        }

        [TestMethod]
        public void TestWindroseLowerLeft() {
            TestWindrose(BoxAnchoring.LowerLeft);
        }

        [TestMethod]
        public void TestWindroseCenterLeft() {
            TestWindrose(BoxAnchoring.CenterLeft);
        }

        [TestMethod]
        public void TestWindroseUpperLeft() {
            TestWindrose(BoxAnchoring.UpperLeft);
        }

        [TestMethod]
        public void TestWindroseCenterTop() {
            TestWindrose(BoxAnchoring.CenterTop);
        }

        [TestMethod]
        public void TestWindroseUpperRight() {
            TestWindrose(BoxAnchoring.UpperRight);
        }

        [TestMethod]
        public void TestWindroseCenterRight() {
            TestWindrose(BoxAnchoring.CenterRight);
        }

        [TestMethod]
        public void TestWindroseLowerRight() {
            TestWindrose(BoxAnchoring.LowerRight);
        }

        [TestMethod]
        public void TestWindroseCenterBottom() {
            TestWindrose(BoxAnchoring.CenterBottom);
        }

        private static void TestWindrose(BoxAnchoring boxAnchoring) {
            CreateAndRender(r => {
                var b = r.Box(r.F(125, 125), "B", r.F(100, 100), borderWidth: 10, boxAnchoring: boxAnchoring,
                    textFont: new Font(FontFamily.GenericSansSerif, 30));
                r.Arrow(b.CenterBottom, r.F(150, 0), 4, text: "CenterBottom");
                r.Arrow(b.LowerLeft, r.F(0, 0), 4, text: "LowerLeft");
                r.Arrow(b.CenterLeft, r.F(0, 150), 4, text: "CenterLeft");
                r.Arrow(b.UpperLeft, r.F(0, 230), 4, text: "UpperLeft");
                r.Arrow(b.CenterTop, r.F(150, 230), 4, text: "CenterTop");
                r.Arrow(b.UpperRight, r.F(230, 230), 4, text: "UpperRight");
                r.Arrow(b.CenterRight, r.F(250, 150), 4, text: "CenterRight");
                r.Arrow(b.LowerRight, r.F(250, 0), 4, text: "LowerRight");
            });
        }

        [TestMethod]
        public void TestTwoBoxes() {
            CreateAndRender(r => {
                var b = r.Box(r.F(100, 100), "B", r.F(100, 100), borderWidth: 10,
                    boxAnchoring: BoxAnchoring.LowerLeft,
                    textFont: new Font(FontFamily.GenericSansSerif, 30));

                r.Box(b.UpperLeft, "C", r.F(50, 200), borderWidth: 5,
                    boxAnchoring: BoxAnchoring.LowerLeft,
                    textFont: new Font(FontFamily.GenericSansSerif, 30));
            });
        }

        #endregion Simple tests

        #region Somewhat complex tests

        internal class SomewhatComplexTestRenderer : GraphicsRenderer<Item, Dependency> {
            private readonly int _boxHeight;
            protected override Color GetBackGroundColor => Color.Yellow;

            public SomewhatComplexTestRenderer(int boxHeight) {
                _boxHeight = boxHeight;
            }

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                double deltaAngle = 2 * Math.PI / items.Count();
                Func<int, double> r =
                      items.Any(i => i.Name.StartsWith("star")) ? i => 100.0 + (i % 2 == 0 ? 60 : 0)
                    : items.Any(i => i.Name.StartsWith("spiral")) ? (Func<int, double>)(i => 80.0 + 20 * i)
                    : /*circle*/ i => 100;

                int n = 0;
                foreach (var i in items) {
                    int k = n++;
                    double phi = k * deltaAngle;
                    // Define position in polar coordinates with origin, radius (r) and angle (ohi).
                    var pos = new VariableVector("pos_" + k, Solver, r(k) * Math.Sin(phi), r(k) * Math.Cos(phi));

                    i.DynamicData.Box = Box(pos, i.Name, B(i.Name).Restrict(F(null, _boxHeight), F(null, _boxHeight)), borderWidth: 2);
                }

                foreach (var d in dependencies) {
                    IBox from = d.UsingItem.DynamicData.Box;
                    IBox to = d.UsedItem.DynamicData.Box;

                    if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                        Arrow(from.GetBestConnector(to.Center), to.GetBestConnector(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                    }
                }
            }

            public static void CreateSomeTestItems(int n, string prefix, out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
                ItemType simple = ItemType.New("Simple:Name");
                var localItems = Enumerable.Range(0, n).Select(i => Item.New(simple, prefix + i)).ToArray();
                dependencies =
                    localItems.SelectMany(
                        (from, i) => localItems.Skip(i).Select(to => new Dependency(from, to, prefix, i, 0, i, 100, 10 * i))).ToArray();
                items = localItems;
            }

            public override void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
                CreateSomeTestItems(5, "spiral", out items, out dependencies);
            }
        }

        private void CreateAndRender(int n, string prefix, int boxHeight = 15) {
            ItemType simple = ItemType.New("Simple:Name");
            var items = Enumerable.Range(0, n).Select(i => Item.New(simple, prefix + i)).ToArray();
            var dependencies =
                items.SelectMany(
                    (from, i) => items.Skip(i).Select(to => new Dependency(from, to, prefix, i, 0, i, 100, 10 * i))).ToArray();

            string tempFile = Path.GetTempFileName();
            Console.WriteLine(Path.ChangeExtension(tempFile, ".gif"));
            new SomewhatComplexTestRenderer(boxHeight).Render(items, dependencies, tempFile);
        }

        [TestMethod]
        public void TestBox() {
            CreateAndRender(1, "single");
        }

        [TestMethod]
        public void TestTriangle() {
            CreateAndRender(3, "circle");
        }

        [TestMethod]
        public void TestSquare() {
            CreateAndRender(4, "circle");
        }

        [TestMethod]
        public void TestSquareWithLargerBoxHeightWorks() {
            CreateAndRender(4, "circle", 30);
        }

        [TestMethod]
        public void TestPentagon() {
            CreateAndRender(5, "circle");
        }

        [TestMethod]
        public void TestSpiral() {
            CreateAndRender(7, "spiral");
        }

        [TestMethod]
        public void TestStar() {
            CreateAndRender(12, "star");
        }

        [TestMethod]
        public void TestLargeSpiral() {
            CreateAndRender(20, "spiral");
        }

        [TestMethod]
        public void TwoItemBoxes() {
            ItemType amo = ItemType.New("AMO:Assembly:Module:Order");
            Item i = Item.New(amo, "VKF", "VKF", "01");

            CreateAndRender(r => {
                VariableVector pos = r.F(0, 0);

                string name = i.Values[0];

                IBox mainBox = r.Box(pos, text: name, boxAnchoring: BoxAnchoring.LowerLeft,
                    borderWidth: 5, boxColor: Color.Coral, name: name);
                VariableVector interfacePos = mainBox.UpperLeft;
                i.DynamicData.InterfaceBox = r.Box(interfacePos, text: name + ".I", minDiagonal: r.F(10, 200),
                    boxAnchoring: BoxAnchoring.LowerLeft, borderWidth: 1, boxColor: Color.Coral, name: name + ".I");
            });
        }

        #endregion Somewhat complex tests

        #region IXOS-Rendering

        internal class IXOSApplicationRenderer : GraphicsDependencyRenderer {
            private static string GetName(Item i) {
                return i.Values[0];
            }

            private static string GetModule(Item i) {
                return i.Values[1];
            }

            private static string GetOrder(Item i) {
                return i.Values[2];
            }

            private static readonly Font _boxFont = new Font(FontFamily.GenericSansSerif, 10);
            private static readonly Font _interfaceFont = new Font(FontFamily.GenericSansSerif, 7);
            private static readonly Font _lineFont = new Font(FontFamily.GenericSansSerif, 5);

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                // ASCII-art sketch of what I want to accomplish:
                //
                //    |         |         |             |          |        |<--------+-----+
                //    |         |         |             |          |<-----------------|     |
                //    |         |         |             |<----------------------------| Top |
                //    |         |         |<------------------------------------------|     |
                //    |         |<----------------------------------------------------+-----+
                //    |         |         |             |          |        |
                //    |         |         |             |          |<-------+-----+
                //    |         |         |             |<------------------|     |
                //    |         |         |<--------------------------------| IMP |
                //    |         |<------------------------------------------|     |
                //    |<----------------------------------------------------+-----+
                //    |         |         |             |          |          |
                //    |         |         |             |<---------+-----+    |
                //    |         |         |<-----------------------|     |    |
                //    |         |<---------------------------------| WLG |    |
                //    |<-------------------------------------------+-----+    |
                //    |         |         |             |          | | |      |
                //    |         |         |<------------|          | | |      |
                //    |         |         | |<::::::::::+-----+    | | |      |
                //    |         |<----------|-----------| VKF |----|--------->|
                //    |<--------------------|-----------+-----+    | | |      |
                //    |         |         | |             | |      | | |      Imp.MI
                //    |<--------------------|---------------|      | | |
                //    |<--------------------|-------------| |      | | |
                //    |         |         | |     ...     | |      | | |
                //    |         |         | |             | |      | | |
                //    |         |         +-----+=================>| | |
                //    |         |         | KAH |---------->|        | |
                //    |         |         +-----+-------->| |        | |
                //    |         |           |             | |        | |
                //    |         +-----+------------------------------->|
                //    |         |     |----------------------------->| |
                //    |         | KST |-------------------->|        | |
                //    |         |     |------------------>| |        | |
                //    |<--------+-----+---->|             | |        | |
                //    |                     |             | |        | |
                //    |        ...          |             | |        | |
                //    |                    Kah         Vkf1 Vkf2  Wlg1 Wlg2
                //    +-----+              .MI          .MI .MI    .MI .MI
                //    | BAC |
                //    +-----+
                //
                // ===> is a dependency from a "lower" to a "higher" module
                // that circumvents the MI. It should most probably be flagged
                // as incorrect and then red in the diagram.
                // :::> is a dependency from a "higher" to a "lower" module
                // via an MI ("module interface"). This is ok, it is only
                // highlighted to show that the Renderer must be able to deal
                // with this.

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

                VariableVector itemDistance = new VariableVector(nameof(itemDistance), Solver);
                VariableVector pos = F(0, 30);

                //itemDistance.MaxY(80);
                //itemDistance.SetX(300);

                Arrow(F(0, 0), F(100, 0), 1, Color.Chartreuse, "100px", textFont: _lineFont);
                Box(F(200, 0), "IXOS-Architektur A (generiert " + DateTime.Now + ")", boxAnchoring: BoxAnchoring.LowerLeft);

                const int DELTA_Y_MAIN = 8;

                // Main modules along diagonal, separated by itemDistance
                foreach (var i in items.Where(i => !IsMI(i)).OrderBy(GetOrder)) {
                    string name = GetName(i);

                    pos.AlsoNamed(name);
                    IBox mainBox = Box(pos, boxAnchoring: BoxAnchoring.LowerLeft, text: name, borderWidth: 3, boxColor: Color.Coral,
                                       textFont: _boxFont, drawingOrder: 1, fixingOrder: 4);
                    //mainBox.Diagonal.Y.Set(100);
                    mainBox.Diagonal.Y.Max(30 + dependencies.Count(d => Equals(d.UsingItem, i)) * DELTA_Y_MAIN); // Help for solving
                    mainBox.Diagonal.Y.Min(10 + dependencies.Count(d => Equals(d.UsingItem, i)) * DELTA_Y_MAIN); // Help for solving
                    i.DynamicData.MainBox = mainBox;
                    i.DynamicData.MainBoxNextFreePos = mainBox.LowerLeft;
                    {
                        IBox interfaceBox = Box(new VariableVector(name + ".I", Solver).SetX(mainBox.LowerLeft.X),
                                                text: "", boxAnchoring: BoxAnchoring.LowerLeft, borderWidth: 1, boxColor: Color.Coral, fixingOrder: 3);
                        interfaceBox.Diagonal.SetX(10);

                        interfaceBox.UpperLeft.MinY(mainBox.UpperLeft.Y + 7);
                        interfaceBox.LowerLeft.MaxY(mainBox.LowerLeft.Y - 7);

                        i.DynamicData.InterfaceBox = interfaceBox;

                        i.DynamicData.InterfaceBoxNextFreePos = mainBox.LowerLeft - F(0, 10);
                    }

                    NumericVariable interfacePos = Solver.CreateConstant("", 18);

                    foreach (var mi in items.Where(mi => IsMI(mi) && GetModule(mi) == GetModule(i)).OrderBy(GetOrder)) {
                        VariableVector miPos = new VariableVector(name + ".MI", Solver).SetX(mainBox.CenterLeft.X + interfacePos);

                        var miBox = Box(miPos, text: GetName(mi), boxAnchoring: BoxAnchoring.UpperLeft,
                            boxTextPlacement: BoxTextPlacement.LeftUp, borderWidth: 1, boxColor: Color.LemonChiffon,
                            textFont: _interfaceFont, fixingOrder: 3);
                        mi.DynamicData.MainItem = i;
                        mi.DynamicData.InterfaceBox = miBox;

                        miBox.UpperLeft.MinY(mainBox.UpperLeft.Y + 7);
                        miBox.LowerLeft.MaxY(mainBox.LowerLeft.Y - miBox.TextBox.Y);

                        interfacePos += 18;
                    }

                    mainBox.Diagonal.MinX(interfacePos);
                    itemDistance.MinX(mainBox.Diagonal.X + 12);
                    itemDistance.MinY(mainBox.Diagonal.Y + 15);

                    pos += itemDistance;
                }

                foreach (var d in dependencies) {
                    Item from = d.UsingItem;
                    Item to = d.UsedItem;
                    if (IsMI(from)) {
                        IBox fromBox = from.DynamicData.InterfaceBox;
                        Item mainItem = from.DynamicData.MainItem;
                        VariableVector nextFreePos = mainItem.DynamicData.InterfaceBoxNextFreePos;

                        VariableVector fromPos = new VariableVector(from + "->" + to, fromBox.LowerLeft.X, nextFreePos.Y);
                        ArrowToInterfaceBox(fromBox, fromPos, to, d, "(MI)");

                        mainItem.DynamicData.InterfaceBoxNextFreePos -= F(0, 15);
                    } else {
                        IBox mainBox = from.DynamicData.MainBox;
                        VariableVector fromPos = from.DynamicData.MainBoxNextFreePos;

                        ArrowToInterfaceBox(mainBox, fromPos, to, d, "");

                        from.DynamicData.MainBoxNextFreePos += F(0, DELTA_Y_MAIN);

                        itemDistance.MinY(fromPos.Y - mainBox.LowerLeft.Y);

                        // mainBox.Diagonal.MinY(fromPos.Y - mainBox.LowerLeft.Y); ==> NO SOLUTION; therefore explcit computation above
                    }
                }
            }

            private void ArrowToInterfaceBox(IBox fromBox, VariableVector fromPos, Item to, Dependency d, string prefix) {
                IBox toBox = to.DynamicData.InterfaceBox;
                VariableVector toPos = toBox.GetBestConnector(fromPos).WithYOf(fromPos);
                fromPos = fromBox.GetBestConnector(toPos).WithYOf(fromPos);
                Arrow(fromPos, toPos, 1, color: d.NotOkCt > 0 ? Color.Red : Color.Black, text: prefix + "#=" + d.Ct, 
                    textLocation: -20, textFont: _lineFont, fixingOrder: 2);

                toBox.UpperLeft.MinY(fromPos.Y + 5);
                toBox.LowerLeft.MaxY(fromPos.Y - toBox.TextBox.Y);
            }

            private static bool IsMI(Item mi) {
                return GetName(mi).Contains(".MI");
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
            }

            private Dependency FromTo(Item from, Item to, int ct = 1, int notok = 0) {
                return new Dependency(from, to, "Test", 0, 0, ct: ct, notOkCt:notok);
            }
        }

        #endregion IXOS-Rendering
    }
}
