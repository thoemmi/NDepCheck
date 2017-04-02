using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;
using NDepCheck.Transforming;
using NDepCheck.Transforming.Ordering;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestTopologicalOrder {
        [TestMethod]
        public void TestSmallMatrixDictionary() {
            var d = new MatrixDictionary<string, int>((s, i) => s + i, (s, i) => s - i);
            //    |  A  B  #
            // ------------#----
            //  A |  1  2  # 3
            //  B |  4  8  # 12
            //  ========================
            //    |  5 10  #
            d.Add("A", "A", 1);
            d.Add("A", "B", 2);
            d.Add("B", "A", 4);
            d.Add("B", "B", 8);

            Assert.AreEqual(3, d.GetFromSum("A"));
            Assert.AreEqual(12, d.GetFromSum("B"));
            Assert.AreEqual(5, d.GetToSum("A"));
            Assert.AreEqual(10, d.GetToSum("B"));

            //    |  A  B  #
            // ------------#----
            //  A |  1  2  # 3
            //  B |  -  -  # 0
            //  ========================
            //    |  1  2  #

            for (int i = 0; i < 3; i++) {
                d.RemoveFrom("B");

                Assert.AreEqual(3, d.GetFromSum("A"));
                Assert.AreEqual(0, d.GetFromSum("B"));
                Assert.AreEqual(1, d.GetToSum("A"));
                Assert.AreEqual(2, d.GetToSum("B"));
            }
        }

        [TestMethod]
        public void TestMatrixDictionary() {
            var d = new MatrixDictionary<string, int>((s, i) => s + i, (s, i) => s - i);
            //    |   A   B   C   D #
            // ---------------------#----
            //  A |  15 100   0   . # 115
            //  B |   3  20 101   . # 124
            //  C |   7   5  30   . #  42
            //  D |   .   2 300   . # 302
            //  =========================
            //    |  25 127 431   0 #

            d.Add("A", "A", 15);
            d.Add("A", "B", 100);
            d.Add("A", "C", 0);
            d.Add("B", "A", 3);
            d.Add("B", "B", 20);
            d.Add("B", "C", 101);
            d.Add("C", "A", 7);
            d.Add("C", "B", 5);
            d.Add("C", "C", 30);
            d.Add("D", "B", 2);
            d.Add("D", "C", 300);

            Assert.AreEqual(115, d.GetFromSum("A"));
            Assert.AreEqual(124, d.GetFromSum("B"));
            Assert.AreEqual(42, d.GetFromSum("C"));
            Assert.AreEqual(302, d.GetFromSum("D"));

            Assert.AreEqual(25, d.GetToSum("A"));
            Assert.AreEqual(127, d.GetToSum("B"));
            Assert.AreEqual(431, d.GetToSum("C"));
            Assert.AreEqual(0, d.GetToSum("D"));

            CollectionAssert.IsSubsetOf(new[] { "A", "B", "C", "D" }, d.FromKeys.ToList());
            CollectionAssert.IsSubsetOf(new[] { "A", "B", "C" }, d.ToKeys.ToList());

            for (int i = 0; i < 2; i++) {
                Assert.IsTrue(d.RemoveFrom("B"));

                //    |   A   B   C   D #
                // ---------------------#----
                //  A |  15 100   0   . # 115
                //  B |   -   -   -   . #   -
                //  C |   7   5  30   . #  42
                //  D |   .   2 300   . # 302
                //  =========================
                //    |  22 107 330   0 #

                Assert.AreEqual(115, d.GetFromSum("A"));
                Assert.AreEqual(0, d.GetFromSum("B"));
                Assert.AreEqual(42, d.GetFromSum("C"));
                Assert.AreEqual(302, d.GetFromSum("D"));

                Assert.AreEqual(22, d.GetToSum("A"));
                Assert.AreEqual(107, d.GetToSum("B"));
                Assert.AreEqual(330, d.GetToSum("C"));
                Assert.AreEqual(0, d.GetToSum("D"));

                CollectionAssert.IsSubsetOf(new[] { "A", "C", "D" }, d.FromKeys.ToList());
                CollectionAssert.IsSubsetOf(new[] { "A", "B", "C" }, d.ToKeys.ToList());
            }
            Assert.IsTrue(d.RemoveFrom("B"));
            Assert.IsFalse(d.RemoveFrom("B"));

            for (int i = 0; i < 2; i++) {
                Assert.IsTrue(d.RemoveTo("A"));

                //    |   A   B   C   D #
                // ---------------------#----
                //  A |   = 100   0   . # 100
                //  B |   -   -   -   . #   -
                //  C |   =   5  30   . #  35
                //  D |   .   2 300   . # 302
                //  =========================
                //    |   0 107 330   0 #

                Assert.AreEqual(100, d.GetFromSum("A"));
                Assert.AreEqual(0, d.GetFromSum("B"));
                Assert.AreEqual(35, d.GetFromSum("C"));
                Assert.AreEqual(302, d.GetFromSum("D"));

                Assert.AreEqual(0, d.GetToSum("A"));
                Assert.AreEqual(107, d.GetToSum("B"));
                Assert.AreEqual(330, d.GetToSum("C"));
                Assert.AreEqual(0, d.GetToSum("D"));

                CollectionAssert.IsSubsetOf(new[] { "A", "C", "D" }, d.FromKeys.ToList());
                CollectionAssert.IsSubsetOf(new[] { "B", "C" }, d.ToKeys.ToList());
            }
            Assert.IsTrue(d.RemoveTo("A"));
            Assert.IsFalse(d.RemoveTo("A"));
        }

        //private class Edge : IEdge {
        //    public Edge(INode usingNode, INode usedNode) {
        //        UsingNode = usingNode;
        //        UsedNode = usedNode;
        //    }

        //    public int Ct => 1;
        //    public int NotOkCt => 0;
        //    public INode UsingNode { get; }
        //    public INode UsedNode { get; }
        //    public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
        //        throw new NotImplementedException();
        //    }

        //    public string AsDipStringWithTypes(bool withExampleInfo) {
        //        throw new NotImplementedException();
        //    }
        //}

        //[TestMethod]
        //public void TestEdgeMatrixDictionary() {
        //    var d = new MatrixDictionary<Edge, int>((s, i) => s + i, (s, i) => s - i);

        //    //d.Add("a", "b", 1);
        //}
        [TestMethod]
        public void TestOneTopologicalSortStep() {
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");
            var dependencies = new[] {
                new Dependency(a, b, null, null, 1),
                new Dependency(c, a, null, null, 100),
                new Dependency(c, b, null, null, 100)
            };

            var aggregated = new Dictionary<FromTo, Dependency>();
            foreach (var d in dependencies.Where(d => !Equals(d.UsingItem, d.UsedItem))) {
                new FromTo(d.UsingItem, d.UsedItem).AggregateEdge(d, aggregated);
            }

            var aggregatedCounts = new MatrixDictionary<Item, int>((s, i) => s + i, (s, i) => s - i);
            foreach (var kvp in aggregated) {
                aggregatedCounts.Add(kvp.Key.From, kvp.Key.To, kvp.Value.Ct);
            }

            var itemsToRatios =
                aggregatedCounts.ToKeys.Select(
                    k => new {
                        Item = k,
                        Ratio = aggregatedCounts.GetFromSum(k) / (aggregatedCounts.GetToSum(k) + aggregatedCounts.GetFromSum(k) + 0.001m)
                    });
            decimal minToRatio = itemsToRatios.Min(ir => ir.Ratio);
            Item minItem = itemsToRatios.First(ir => ir.Ratio == minToRatio).Item;

            Assert.AreEqual(b, minItem);
        }

        [TestMethod]
        public void TestTopologicalSortTestData() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                // transform testdata of AddTopologicalOrder
                "-s", ".", typeof(AddItemOrder).Name,
                // write them as dip file
                "-r", typeof(DipWriter).Name, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains("SIMPLE;0000:C"));
                Assert.IsTrue(o.Contains("SIMPLE;0001:B"));
                Assert.IsTrue(o.Contains("SIMPLE;0002:A"));
                Assert.IsTrue(o.Contains("SIMPLE:D"));
            }
        }
    }
}
