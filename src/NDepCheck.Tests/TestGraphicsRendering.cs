using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.ConstraintSolving;
using NDepCheck.Rendering.GraphicsRendering;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestGraphicsRendering {

        #region Simple tests

        internal class LambdaTestRenderer : GraphicsRenderer {
            private readonly Action<LambdaTestRenderer> _placeObjects;

            public LambdaTestRenderer(Action<LambdaTestRenderer> placeObjects) {
                _placeObjects = placeObjects;
            }

            protected override Color GetBackGroundColor => Color.Yellow;

            protected override void PlaceObjects(IEnumerable<Dependency> dependencies) {
                _placeObjects(this);
            }

            public override IEnumerable<Dependency> CreateSomeTestDependencies() {
                ItemType simple = ItemType.New("SIMPLE(Name)");
                Item i1 = Item.New(simple, "I1");
                Item i2 = Item.New(simple, "I2");
                return new[] { new Dependency(i1, i1, new TextFileSourceLocation("Test", 1), "Test", ct: 1),
                                       new Dependency(i1, i2, new TextFileSourceLocation("Test", 2), "Test", ct: 1) };
            }

            public override string GetHelp(bool extensiveHelp, string filter) {
                return "DelegteTestRenderer";
            }
        }

        private static void CreateAndRender(Action<LambdaTestRenderer> placeObjects) {
            new LambdaTestRenderer(placeObjects).Render(new GlobalContext(), Enumerable.Empty<Dependency>(), 0,
                                    "", new WriteTarget(Path.GetTempFileName(), append: false, limitLinesForConsole: 100), ignoreCase: false);
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
            const int N = 13;
            const int ANCHORS = 10;

            CreateAndRender(r => {
                var b = r.Box(r.F(0, 0), "A long text", r.F(null, 40), borderWidth: 10,
                    textFont: new Font(FontFamily.GenericSansSerif, 30), connectors: ANCHORS - 2, name: "BOX");

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
                    borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 20));
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

        internal class SomewhatComplexTestRenderer : GraphicsRenderer {
            private readonly int _boxHeight;
            protected override Color GetBackGroundColor => Color.Yellow;

            public SomewhatComplexTestRenderer(int boxHeight) {
                _boxHeight = boxHeight;
            }

            protected override void PlaceObjects(IEnumerable<Dependency> dependencies) {
                IEnumerable<Item> items = dependencies.SelectMany(e => new[] { e.UsingItem, e.UsedItem }).Distinct();
                double deltaAngle = 2 * Math.PI / items.Count();
                Func<int, double> r =
                      items.Any(i => i.Name.StartsWith("star")) ? i => 100.0 + (i % 2 == 0 ? 60 : 0)
                    : items.Any(i => i.Name.StartsWith("spiral")) ? (Func<int, double>) (i => 80.0 + 20 * i)
                    : /*circle*/ i => 100;

                int n = 0;
                var boxes = new Dictionary<Item, IBox>();
                foreach (var i in items) {
                    int k = n++;
                    double phi = k * deltaAngle;
                    // Define position in polar coordinates with origin, radius (r) and angle (ohi).
                    var pos = new VariableVector("pos_" + k, Solver, r(k) * Math.Sin(phi), r(k) * Math.Cos(phi));

                    boxes[i] = Box(pos, i.Name, B(i.Name).Restrict(F(null, _boxHeight), F(null, _boxHeight)), borderWidth: 2);
                }

                foreach (var d in dependencies) {
                    IBox from = boxes[d.UsingItem];
                    IBox to = boxes[d.UsedItem];

                    if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                        Arrow(from.GetBestConnector(to.Center), to.GetBestConnector(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                    }
                }
            }

            public static IEnumerable<Dependency> CreateSomeTestItems(int n, string prefix) {
                ItemType simple = ItemType.New("SIMPLE(Name)");
                var localItems = Enumerable.Range(0, n).Select(i => Item.New(simple, prefix + i)).ToArray();
                return localItems.SelectMany(
                        (from, i) => localItems.Skip(i).Select(to => new Dependency(from, to, new TextFileSourceLocation(prefix, i), "Use", 10 * i))).ToArray();
            }

            public override IEnumerable<Dependency> CreateSomeTestDependencies() {
                return CreateSomeTestItems(5, "spiral");
            }

            public override string GetHelp(bool extensiveHelp, string filter) {
                return "SomewhatComplexTestRenderer";
            }
        }

        private void CreateAndRender(int n, string prefix, int boxHeight = 15) {
            ItemType simple = ItemType.New("SIMPLE(Name)");
            Item[] items = Enumerable.Range(0, n).Select(i => Item.New(simple, prefix + i)).ToArray();
            Dependency[] dependencies =
                items.SelectMany(
                    (from, i) => items.Skip(i).Select(to => new Dependency(from, to, new TextFileSourceLocation(prefix, i), "Use", 10 * i))).ToArray();

            new SomewhatComplexTestRenderer(boxHeight).Render(new GlobalContext(), dependencies, dependencies.Length,
                argsAsString: "", target: new WriteTarget(Path.GetTempFileName(), append: false, limitLinesForConsole: 100),ignoreCase: false);
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
            ItemType amo = ItemType.New("AMO(Assembly:Module:Order)");
            Item i = Item.New(amo, "VKF", "VKF", "01");

            CreateAndRender(r => {
                VariableVector pos = r.F(0, 0);

                string name = i.Values[0];

                IBox mainBox = r.Box(pos, text: name, boxAnchoring: BoxAnchoring.LowerLeft,
                    borderWidth: 5, boxColor: Color.Coral, name: name);
                VariableVector interfacePos = mainBox.UpperLeft;
                r.Box(interfacePos, text: name + ".I", minDiagonal: r.F(10, 200),
                    boxAnchoring: BoxAnchoring.LowerLeft, borderWidth: 1, boxColor: Color.Coral, name: name + ".I");
            });
        }

        #endregion Somewhat complex tests
    }
}
