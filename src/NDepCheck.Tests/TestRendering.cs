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

            public DelegteTestRenderer(Action<DelegteTestRenderer> placeObjects) {
                _placeObjects = placeObjects;
            }

            protected override Color GetBackGroundColor => Color.Yellow;

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                _placeObjects(this);
            }

            protected override Size GetSize() {
                return new Size(300, 400);
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
            Console.WriteLine(tempFile + ".gif");
            new DelegteTestRenderer(placeObjects).RenderToFile(Enumerable.Empty<Item>(), Enumerable.Empty<Dependency>(), tempFile, null);
        }

        [TestMethod]
        public void TestSingleBox() {
            CreateAndRender(r => r.Box(Vector.Fixed(100, 100), Vector.Fixed(70, 200), "B",
                borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 30)));
        }

        [TestMethod]
        public void TestSingleBoxWithText() {
            CreateAndRender(r => r.Box(Vector.Fixed(100, 100), Vector.Bounded("b").Set(null, 200),
                "A long text", borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 30)));
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
                var b = r.Box(Vector.Fixed(0, 0), Vector.Bounded("b").Set(null, 40), "A long text", borderWidth: 10,
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
                IBox box = r.Box(Vector.Fixed(100, 100), Vector.Fixed(70, 200), "B",
                    borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 10));
                Vector far = Vector.Fixed(300, 400);
                r.Arrow(box.Center, far, width: 5, color: Color.Blue); // center to far point
                r.Arrow(box.Center, Vector.Fixed(400, 400), width: 2, color: Color.Green); // 45° diagonal from center
                r.Arrow(box.GetBestConnector(far), far, width: 10, color: Color.Brown); // anchor to far point
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

                var diagonals = new Store<Item, Vector>();

                int n = 0;
                foreach (var i in items) {
                    int k = n++;
                    double angle = k * deltaAngle;
                    var pos = new DependentVector(() => origin.X() + r(k) * Math.Sin(angle), () => origin.X() + r(k) * Math.Cos(angle), "pos_" + k);

                    i.DynamicData.Box = Box(pos, diagonals.Put(i, B(i.Name).Set(null, 15)), i.Name, borderWidth: 2);
                }

                foreach (var d in dependencies) {
                    IBox from = d.UsingItem.DynamicData.Box;
                    IBox to = d.UsedItem.DynamicData.Box;

                    if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                        //Arrow(from.Center, to.Center, 1, text: "#=" + d.Ct, textLocation: 0.3);
                        Arrow(from.GetBestConnector(to.Center), to.GetBestConnector(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                        //Arrow(from.GetNearestAnchor(to.Center), to.Center, 1, text: "#=" + d.Ct);
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
            Console.WriteLine(tempFile + ".gif");
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

                    BoundedVector mainBoxDiagonal = new BoundedVector("/" + name);
                    IBox mainBox = Box(pos, mainBoxDiagonal, boxAnchoring: BoxAnchoring.LowerLeft, text: name, borderWidth: 5, color: Color.Coral);
                    Vector interfacePos = mainBox.UpperLeft;
                    i.DynamicData.InterfaceBox = Box(interfacePos, new BoundedVector(name).Set(5, null),
                        boxAnchoring: BoxAnchoring.LowerLeft, text: "", borderWidth: 1, color: Color.Coral);

                    foreach (var mi in items.Where(mi => GetName(mi).Contains(".MI") && GetModule(mi) == GetModule(i)).OrderBy(GetOrder)) {
                        interfacePos += F(15, 0);

                        mi.DynamicData.InterfaceBox = Box(interfacePos, new BoundedVector(name).Set(5, null),
                                                            boxAnchoring: BoxAnchoring.LowerLeft, text: GetName(mi),
                                                            placing: TextPlacing.LeftUp, borderWidth: 1, color: Color.LemonChiffon);
                    }

                    mainBoxDiagonal.Restrict(minX: () => interfacePos.X() - mainBox.LowerLeft.X());
                    itemDistance.Restrict(minX: () => mainBoxDiagonal.X() + 15);

                    // Testing only:
                    itemDistance.Restrict(minY: () => mainBoxDiagonal.Y() + 15);

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
