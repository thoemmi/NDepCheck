//using System.Collections.Generic;
//using System.Linq;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using NDepCheck.Transforming.GraphTransformations;

//namespace NDepCheck.Tests {
//    [TestClass]
//    public class TestUnhideCycles {
//        private static readonly ItemType TEST = ItemType.New("TEST", new[] { "NAME" }, new[] { "" });

//        [TestMethod]
//        public void TestSimpleUnhideCycles() {
//            // 1 --> 2, 2 --> 3, 3 --> 4, 4 --> 1, 1 --> 5
//            var n1 = new TestNode("n1", true, TEST);
//            var n2 = new TestNode("n2", true, TEST);
//            var n3 = new TestNode("n3", true, TEST);
//            var n4 = new TestNode("n4", true, TEST);
//            var n5 = new TestNode("n5", true, TEST);

//            n1.AddEdgeTo(n2);
//            n2.AddEdgeTo(n3);
//            n3.AddEdgeTo(n4);
//            n4.AddEdgeTo(n1);
//            n1.AddEdgeTo(n5);

//            UnhideCycles(new[] { n1, n2, n3, n4, n5 }.SelectMany(n => n.Edges));

//            Assert.IsFalse(n1.Edges.First().Hidden);
//            Assert.IsFalse(n2.Edges.First().Hidden);
//            Assert.IsFalse(n3.Edges.First().Hidden);
//            Assert.IsFalse(n4.Edges.First().Hidden);
//            Assert.IsTrue(n1.Edges.Skip(1).First().Hidden);
//        }

//        private static void UnhideCycles(IEnumerable<TestEdge> edges) {
//            foreach (var e in edges) {
//                e.Hidden = true;
//            }

//            new UnhideCycles<TestEdge>(new string[0]).Run(edges);
//        }

//        [TestMethod]
//        public void TestSmallUnhideCycles() {
//            // 1 --> 2, 2 --> 3, 3 --> 2, 3 --> 4
//            var n1 = new TestNode("n1", true, TEST);
//            var n2 = new TestNode("n2", true, TEST);
//            var n3 = new TestNode("n3", true, TEST);
//            var n4 = new TestNode("n4", true, TEST);

//            n1.AddEdgeTo(n2);
//            n2.AddEdgeTo(n3);
//            n3.AddEdgeTo(n2);
//            n3.AddEdgeTo(n4);

//            UnhideCycles(new[] { n1, n2, n3, n4 }.SelectMany(n => n.Edges));

//            Assert.IsTrue(n1.Edges.First().Hidden);
//            Assert.IsFalse(n2.Edges.First().Hidden);
//            Assert.IsFalse(n3.Edges.First().Hidden);
//            Assert.IsTrue(n3.Edges.Skip(1).First().Hidden);
//        }

//        [TestMethod]
//        public void TestAcyclicLattice3() {
//            TestAcyclicLattice(3);
//        }

//        [TestMethod]
//        public void TestAcyclicLattice100() {
//            TestAcyclicLattice(100);
//        }

//        private static void TestAcyclicLattice(int N) {
//            List<TestNode> nodes = CreateTriangularLattice(N);
//            UnhideCycles(nodes.SelectMany(n => n.Edges));
//            int ct = 0;
//            foreach (var n in nodes) {
//                foreach (var e in n.Edges) {
//                    ct++;
//                    Assert.IsTrue(e.Hidden);
//                }
//            }
//            Assert.AreEqual(N * (N - 1) / 2, ct);
//        }

//        private static List<TestNode> CreateTriangularLattice(int N) {
//            var nodes = Enumerable.Range(0, N).Select(i => new TestNode("n" + i, true, TEST)).ToList();
//            foreach (var src in nodes) {
//                TestNode src1 = src;
//                IEnumerable<TestNode> testNodes = nodes.SkipWhile(n => n != src1).Skip(1).ToArray();
//                foreach (var tgt in testNodes) {
//                    src.AddEdgeTo(tgt);
//                }
//            }
//            return nodes;
//        }

//        [TestMethod]
//        public void TestCyclicLattice3() {
//            TestCyclicLattice(3);
//        }

//        [TestMethod]
//        public void TestCyclicLattice100() {
//            TestCyclicLattice(100);
//        }

//        private static void TestCyclicLattice(int N) {
//            List<TestNode> nodes = CreateTriangularLattice(N);
//            nodes.Last().AddEdgeTo(nodes.First());
//            var sink = new TestNode("sink", true, TEST);
//            foreach (var n in nodes) {
//                n.AddEdgeTo(sink);
//            }
//            nodes.Add(sink);

//            UnhideCycles(nodes.SelectMany(n => n.Edges));

//            foreach (var n in nodes) {
//                foreach (var e in n.Edges) {
//                    if (e.UsedNode.Equals(sink)) {
//                        Assert.IsTrue(e.Hidden, "Edge " + n + e + " is not hidden");
//                    } else {
//                        Assert.IsFalse(e.Hidden, "Edge " + n + e + " is still hidden");
//                    }
//                }
//            }
//        }
//    }
//}
