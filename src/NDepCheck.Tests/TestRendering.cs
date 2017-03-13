using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;

namespace NDepCheck.Tests {

    [TestClass]
    public class TestRendering {

        #region Simple tests

        internal class DelegteTestRenderer : GraphicsRenderer<Item, Dependency> {
            private readonly Action<DelegteTestRenderer> _placeObjects;
            private readonly int _width;
            private readonly int _height;

            public DelegteTestRenderer(Action<DelegteTestRenderer> placeObjects, int width, int height) {
                _placeObjects = placeObjects;
                _width = width;
                _height = height;
            }

            protected override Color GetBackGroundColor => Color.Yellow;

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                _placeObjects(this);
            }

            protected override Size GetSize() {
                return new Size(_width, _height);
            }

            public override void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
                ItemType simple = ItemType.New("Simple:Name");
                Item i1 = Item.New(simple, "I1");
                Item i2 = Item.New(simple, "I2");
                items = new[] { i1, Item.New(simple, "I2") };
                dependencies = new[] { new Dependency(i1, i1, "Test", 0, 0), new Dependency(i1, i2, "Test", 0, 0) };
            }
        }

        private static void CreateAndRender(Action<DelegteTestRenderer> placeObjects, int width = 300, int height = 400) {
            string tempFile = Path.GetTempFileName();
            Console.WriteLine(Path.ChangeExtension(tempFile, ".gif"));
            new DelegteTestRenderer(placeObjects, width, height).RenderToFile(Enumerable.Empty<Item>(), Enumerable.Empty<Dependency>(), tempFile, null);
        }

        [TestMethod]
        public void TestSingleBox() {
            CreateAndRender(r => r.Box(Vector.Fixed(100, 100), "B", Vector.Fixed(70, 200),
                borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 30)));
        }

        [TestMethod]
        public void TestSingleBoxWithText() {
            CreateAndRender(r => r.Box(Vector.Fixed(100, 100), "A long text", Vector.Fixed(null, 200),
                borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 30)));
        }

        [TestMethod]
        public void TestBoxesWithText() {
            CreateAndRender(r => {
                var pos = Vector.Fixed(0, 0);
                foreach (BoxTextPlacement e in typeof(BoxTextPlacement).GetEnumValues()) {
                    r.Box(pos, e.ToString(), Vector.Fixed(90, 200), borderWidth: 1,
                        boxTextPlacement: e, textFont: new Font(FontFamily.GenericSansSerif, 8));
                    pos += Vector.Fixed(100, 0);
                }
            }, 2000, 1500);
        }

        [TestMethod]
        public void TestLinesWithText() {
            CreateAndRender(r => {
                var tail = Vector.Fixed(0, 0);
                var head = Vector.Fixed(100, 120);
                var delta = head;
                foreach (LineTextPlacement e in typeof(LineTextPlacement).GetEnumValues()) {
                    r.Arrow(tail, head, 3, placement: e, text: e.ToString(), textFont: new Font(FontFamily.GenericSansSerif, 8), textPadding: 0.5);
                    tail = head;
                    delta = ~delta;
                    head += delta;
                }
            }, 2000, 1500);
        }

        [TestMethod]
        public void TestLinesWithMovedText() {
            CreateAndRender(r => {
                var tail = Vector.Fixed(0, 0);
                var head = Vector.Fixed(100, 120);
                var delta = head;
                foreach (LineTextPlacement e in typeof(LineTextPlacement).GetEnumValues()) {
                    r.Arrow(tail, head, 3, placement: e, text: e.ToString(), textFont: new Font(FontFamily.GenericSansSerif, 8), textLocation: 0.2);
                    tail = head;
                    delta = ~delta;
                    head += delta;
                }
            }, 2000, 1500);
        }

        [TestMethod]
        public void TestSingleLine() {
            CreateAndRender(r => r.Arrow(Vector.Fixed(30, -100), Vector.Fixed(170, 300), 10, Color.Red));
        }

        [TestMethod]
        public void TestSingleBoxWithAnchors() {
            const int N = 13;
            const int ANCHORS = 10;

            CreateAndRender(r => {
                var b = r.Box(Vector.Fixed(0, 0), "A long text", Vector.Fixed(null, 40), borderWidth: 10,
                    textFont: new Font(FontFamily.GenericSansSerif, 30), connectors: ANCHORS);

                for (int i = 0; i < N; i++) {
                    var angle = 2 * Math.PI * i / N;
                    var farAway = Vector.Fixed(300 * Math.Sin(angle), 300 * Math.Cos(angle));
                    r.Arrow(farAway, b.GetBestConnector(farAway), 2 + i);
                }
            });
        }

        [TestMethod]
        public void TestSingleAnchor() {
            CreateAndRender(r => {
                IBox box = r.Box(Vector.Fixed(100, 100), "B", Vector.Fixed(70, 200),
                    borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 100));
                Vector far = Vector.Fixed(300, 400);
                r.Arrow(box.Center, far, width: 5, color: Color.Blue); // center to far point
                r.Arrow(box.Center, Vector.Fixed(400, 400), width: 2, color: Color.Green); // 45° diagonal from center
                r.Arrow(box.GetBestConnector(far), far, width: 10, color: Color.Brown); // anchor to far point
            });
        }

        [TestMethod]
        public void TestWindrose() {
            CreateAndRender(r => {
                var b = r.Box(Vector.Fixed(100, 100), "B", Vector.Fixed(100, 100), borderWidth: 10,
                    boxAnchoring: BoxAnchoring.LowerLeft,
                    textFont: new Font(FontFamily.GenericSansSerif, 30));
                r.Arrow(b.CenterBottom, Vector.Fixed(150, 0), 4, text: "CenterBottom");
                r.Arrow(b.LowerLeft, Vector.Fixed(0, 0), 4, text: "LowerLeft");
                r.Arrow(b.CenterLeft, Vector.Fixed(0, 150), 4, text: "CenterLeft");
                r.Arrow(b.UpperLeft, Vector.Fixed(0, 230), 4, text: "UpperLeft");
                r.Arrow(b.CenterTop, Vector.Fixed(150, 230), 4, text: "CenterTop");
                r.Arrow(b.UpperRight, Vector.Fixed(230, 230), 4, text: "UpperRight");
                r.Arrow(b.CenterRight, Vector.Fixed(250, 150), 4, text: "CenterRight");
                r.Arrow(b.LowerRight, Vector.Fixed(250, 0), 4, text: "LowerRight");
            });
        }

        [TestMethod]
        public void TestTwoBoxes() {
            CreateAndRender(r => {
                var b = r.Box(Vector.Fixed(100, 100), "B", Vector.Fixed(100, 100), borderWidth: 10,
                    boxAnchoring: BoxAnchoring.LowerLeft,
                    textFont: new Font(FontFamily.GenericSansSerif, 30));

                r.Box(b.UpperLeft, "C", Vector.Fixed(50, 200), borderWidth: 5,
                    boxAnchoring: BoxAnchoring.LowerLeft,
                    textFont: new Font(FontFamily.GenericSansSerif, 30));
            });
        }

        #endregion Simple tests

        #region Somewhat complex tests

        internal class SomewhatComplexTestRenderer : GraphicsRenderer<Item, Dependency> {
            private readonly Size _size;

            public SomewhatComplexTestRenderer(Size size) {
                _size = size;
            }

            protected override Size GetSize() {
                return _size;
            }

            protected override Color GetBackGroundColor => Color.Yellow;

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                var origin = new BoundedVector("origin");
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
                    var pos = new DependentVector(
                        () => origin.X() + r(k) * Math.Sin(phi),
                        () => origin.X() + r(k) * Math.Cos(phi), "pos_" + k);

                    i.DynamicData.Box = Box(pos, i.Name, B(i.Name).Set(null, 15), borderWidth: 2);
                }

                foreach (var d in dependencies) {
                    IBox from = d.UsingItem.DynamicData.Box;
                    IBox to = d.UsedItem.DynamicData.Box;

                    if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                        Arrow(from.GetBestConnector(to.Center), to.GetBestConnector(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                    }
                }

                origin.Set(0, 0);
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

        private void CreateAndRender(int n, string prefix, int width = 300, int height = 400) {
            ItemType simple = ItemType.New("Simple:Name");
            var items = Enumerable.Range(0, n).Select(i => Item.New(simple, prefix + i)).ToArray();
            var dependencies =
                items.SelectMany(
                    (from, i) => items.Skip(i).Select(to => new Dependency(from, to, prefix, i, 0, i, 100, 10 * i))).ToArray();

            string tempFile = Path.GetTempFileName();
            Console.WriteLine(Path.ChangeExtension(tempFile, ".gif"));
            new SomewhatComplexTestRenderer(new Size(width, height)).RenderToFile(items, dependencies, tempFile, null);
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
            CreateAndRender(20, "spiral", 2000, 2000);
        }

        [TestMethod]
        public void TwoItemBoxes() {
            ItemType amo = ItemType.New("AMO:Assembly:Module:Order");
            Item i = Item.New(amo, "VKF", "VKF", "01");

            CreateAndRender(r => {
                Vector pos = Vector.Fixed(0, 0);

                string name = i.Values[0];

                IBox mainBox = r.Box(pos, text: name, boxAnchoring: BoxAnchoring.LowerLeft,
                    borderWidth: 5, boxColor: Color.Coral);
                Vector interfacePos = mainBox.UpperLeft;
                i.DynamicData.InterfaceBox = r.Box(interfacePos, text: "", minDiagonal: Vector.Fixed(10, 200),
                    boxAnchoring: BoxAnchoring.LowerLeft, borderWidth: 1, boxColor: Color.Coral);
            });
        }

        #endregion Somewhat complex tests

        #region IXOS-Rendering

        internal class IXOSApplicationRenderer : GraphicsDependencyRenderer {
            protected override Size GetSize() {
                return new Size(2000, 1600);
            }

            private static string GetName(Item i) {
                return i.Values[0];
            }

            private static string GetModule(Item i) {
                return i.Values[1];
            }

            private static string GetOrder(Item i) {
                return i.Values[2];
            }

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
                //    |         |         |             |            | |      |
                //    |         |         |<------------+-----+      | |      |
                //    |         |<----------------------| VKF |-------------->|
                //    |<--------------------------------+-----+      | |      |
                //    |         |         |               | |        | |      Imp.MI
                //    |<------------------------------------|        | |
                //    |<----------------------------------| |        | |
                //    |         |         |       ...     | |        | |
                //    |         |         |               | |        | |
                //    |         |         +-----+         | |        | |
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

                BoundedVector itemDistance = new BoundedVector(nameof(itemDistance));
                Vector pos = F(0, 0);

                // Hauptmodule auf Diagonale
                foreach (var i in items.Where(i => !GetName(i).Contains(".MI")).OrderBy(GetOrder)) {
                    string name = GetName(i);

                    IBox mainBox = Box(pos, boxAnchoring: BoxAnchoring.LowerLeft, text: name, borderWidth: 5, boxColor: Color.Coral);
                    IBox interfaceBox = Box(mainBox.UpperLeft, text: "", boxAnchoring: BoxAnchoring.LowerLeft, borderWidth: 1, boxColor: Color.Coral);
                    interfaceBox.Diagonal.Set(5, 20);
                    i.DynamicData.InterfaceBox = interfaceBox;

                    Vector interfacePos = mainBox.LowerLeft;
                    foreach (var mi in items.Where(mi => GetName(mi).Contains(".MI") && GetModule(mi) == GetModule(i)).OrderBy(GetOrder)) {
                        interfaceBox = Box(interfacePos, text: GetName(mi), boxAnchoring: BoxAnchoring.UpperLeft,
                                           boxTextPlacement: BoxTextPlacement.LeftUp, borderWidth: 1, boxColor: Color.LemonChiffon);
                        interfaceBox.Diagonal.Set(5, 200); // for Tests only!!!!!!
                        mi.DynamicData.InterfaceBox = interfaceBox;
                        interfacePos += F(15, 0);
                    }

                    mainBox.Diagonal.Restrict(minX: () => interfacePos.X() - mainBox.LowerLeft.X());
                    itemDistance.Restrict(minX: () => mainBox.Diagonal.X() + 15);

                    // Testing only:
                    itemDistance.Restrict(minY: () => mainBox.Diagonal.Y() + 15);

                    pos += itemDistance;
                }

                foreach (var d in dependencies) {



                }


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
                var wlg = Item.New(amo, "WLG:WLG:0500".Split(':'));
                var wlg1_mi = Item.New(amo, "Wlg1.MI:WLG:0501".Split(':'));
                var wlg2_mi = Item.New(amo, "Wlg2.MI:WLG:0502".Split(':'));
                var imp = Item.New(amo, "IMP:IMP:0600".Split(':'));
                var imp_mi = Item.New(amo, "Imp.MI:IMP:0601".Split(':'));
                var top = Item.New(amo, "Top:TOP:0700".Split(':'));

                items = new[] { bac, kst, kah, kah_mi, vkf, vkf1_mi, vkf2_mi, wlg, wlg1_mi, wlg2_mi, imp, imp_mi, top };

                dependencies = new[] {
                    FromTo(kst, bac), FromTo(kst, kah_mi), FromTo(kst, vkf1_mi), FromTo(kst, vkf2_mi), FromTo(kst, wlg1_mi), FromTo(kst, wlg2_mi),
                    FromTo(kah, bac), FromTo(kah, vkf1_mi), FromTo(kah, vkf2_mi),
                    FromTo(vkf, bac), FromTo(vkf, kst), FromTo(vkf, kah), FromTo(vkf, imp_mi), FromTo(vkf1_mi, bac), FromTo(vkf2_mi, bac),
                    // ... more to come
                };
            }

            private Dependency FromTo(Item from, Item to) {
                return new Dependency(from, to, "Test", 0, 0);
            }
        }

        #endregion IXOS-Rendering
    }
}
