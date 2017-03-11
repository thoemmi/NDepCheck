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

        internal class DelegteTestRenderer : Renderer {
            private readonly Action<DelegteTestRenderer> _placeObjects;

            public DelegteTestRenderer(Action<DelegteTestRenderer> placeObjects) {
                _placeObjects = placeObjects;
            }

            protected override Color GetBackGroundColor => Color.Yellow;

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                _placeObjects(this);
            }
        }

        private static void CreateAndRender(Action<DelegteTestRenderer> placeObjects) {
            string tempFile = Path.GetTempFileName();
            Console.WriteLine(tempFile + ".gif");
            new DelegteTestRenderer(placeObjects).RenderToFile(Enumerable.Empty<Item>(), Enumerable.Empty<Dependency>(),
                tempFile, 300, 400);
        }

        [TestMethod]
        public void TestSingleBox() {
            CreateAndRender(r => r.Box(Vector.Fixed(100, 100), Vector.Fixed(70, 200), "B",
                borderWidth: 10, textFont: new Font(FontFamily.GenericSansSerif, 30)));
        }

        [TestMethod]
        public void TestSingleBoxWithText() {
            CreateAndRender(r => r.Box(Vector.Fixed(100, 100), Vector.Variable("b", null, 200),
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
                var b = r.Box(Vector.Fixed(0, 0), Vector.Variable("b", null, 40), "A long text", borderWidth: 10,
                    textFont: new Font(FontFamily.GenericSansSerif, 30), anchorNr: ANCHORS);

                for (int i = 0; i < N; i++) {
                    var angle = 2 * Math.PI * i / N;
                    var farAway = Vector.Fixed(300 * Math.Sin(angle), 300 * Math.Cos(angle));
                    r.Arrow(farAway, b.GetBestAnchor(farAway), 2 + i);
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
                r.Arrow(box.GetBestAnchor(far), far, width: 10, color: Color.Brown); // anchor to far point
            });
        }

        #endregion Simple tests

        #region Somewhat complex tests

        internal class SomewhatComplexTestRenderer : Renderer {
            protected override Color GetBackGroundColor => Color.Yellow;

            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                var origin = new VariableVector("origin");
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
                    var pos = new DependentVector(() => origin.X() + r(k) * Math.Sin(angle), () => origin.X() + r(k) * Math.Cos(angle));
                    ItemBoxes.Put(i, Box(pos, diagonals.Put(i, V(i.Name, null, 15)), i.Name, borderWidth: 2));
                }

                foreach (var d in dependencies) {
                    IBox from = ItemBoxes.Get(d.UsingItem);
                    IBox to = ItemBoxes.Get(d.UsedItem);

                    if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                        //Arrow(from.Center, to.Center, 1, text: "#=" + d.Ct, textLocation: 0.3);
                        Arrow(from.GetBestAnchor(to.Center), to.GetBestAnchor(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                        //Arrow(from.GetNearestAnchor(to.Center), to.Center, 1, text: "#=" + d.Ct);
                    }
                }

                origin.Set(0, 0);
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
            new SomewhatComplexTestRenderer().RenderToFile(items, dependencies, tempFile, width, height);
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

        internal class IXOSApplicationRenderer : Renderer {
            protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
                // ItemType Name:Order

                VariableVector itemDistance = new VariableVector(nameof(itemDistance), null, 40);
                Vector pos = C(0, 0);
                
                // Hauptmodule auf Diagonale
                foreach (var i in items.Where(i => !i.Values[0].Contains(".MI")).OrderBy(i => i.Values[1])) {
                    var name = i.Values[0];
                    Box(pos, null, name, borderWidth: 5);
                    pos += itemDistance;
                }

                // MIs ____

                // Abhängigkeiten ___
                throw new NotImplementedException();
            }
        }

        #endregion IXOS-Rendering
        }
    }
