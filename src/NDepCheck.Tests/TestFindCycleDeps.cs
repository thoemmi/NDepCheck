using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;
using NDepCheck.Transforming.CycleChecking;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestFindCycleDeps {
        private void AssertEdgeCount(int expected, string o) {
            string[] lines = o.Split('\n');
            int actual = lines.Count(line => line.Contains(EdgeConstants.DIP_ARROW));
            Assert.AreEqual(expected, actual, $"Number of deps is different: '{o}'");
        }

        [TestMethod]
        public void TestFindSmallCycle() {
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var deps = new[] { new Dependency(a, b, null, "", 1), new Dependency(b, a, null, "", 1), };
            var result = new List<Dependency>();

            new FindCycleDeps().Transform(new GlobalContext(), "test", deps, "", "test", result);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].BadCt);
            Assert.AreEqual(1, result[1].BadCt);
        }

        [TestMethod]
        public void TestFindLaterCycle() {
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var c = Item.New(ItemType.SIMPLE, "c");
            var d = Item.New(ItemType.SIMPLE, "d");
            var e = Item.New(ItemType.SIMPLE, "e");
            var deps = new[] {
                new Dependency(a, b, null, "", 1),
                new Dependency(b, c, null, "", 1),
                new Dependency(c, d, null, "", 1),
                new Dependency(d, e, null, "", 1),
                new Dependency(e, c, null, "", 1),
            };
            var result = new List<Dependency>();

            new FindCycleDeps().Transform(new GlobalContext(), "test", deps, "-k", "test", result);

            result.Sort((x, y) => String.Compare(x.UsingItemAsString, y.UsingItemAsString, StringComparison.Ordinal));

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(c, result[0].UsingItem);
            Assert.AreEqual(1, result[0].BadCt);
            Assert.AreEqual(d, result[1].UsingItem);
            Assert.AreEqual(1, result[1].BadCt);
            Assert.AreEqual(e, result[2].UsingItem);
            Assert.AreEqual(1, result[2].BadCt);
        }

        [TestMethod]
        public void TestOverlappingCycles() {
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var c = Item.New(ItemType.SIMPLE, "c");
            var d = Item.New(ItemType.SIMPLE, "d");
            var e = Item.New(ItemType.SIMPLE, "e");
            var deps = new[] {
                // "Confusing" edges to sink b
                new Dependency(a, b, null, "", 1),
                new Dependency(b, b, null, "", 1),
                new Dependency(c, b, null, "", 1),
                new Dependency(d, b, null, "", 1),

                // a->c->d->e
                new Dependency(a, c, null, "", 1),
                new Dependency(c, d, null, "", 1),
                new Dependency(d, e, null, "", 1),

                // Cycle edges c<-d and a<-e
                new Dependency(d, c, null, "", 1),
                new Dependency(e, a, null, "", 1),
            };
            var result = new List<Dependency>();

            new FindCycleDeps().Transform(new GlobalContext(), "test", deps, "{ -k -i }", "test", result);

            result.Sort((x, y) => String.Compare(x.UsingItemAsString, y.UsingItemAsString, StringComparison.Ordinal));

            Assert.AreEqual(5, result.Count);
            Assert.AreEqual(a, result[0].UsingItem);
            Assert.AreEqual(1, result[0].BadCt);
            Assert.AreEqual(c, result[1].UsingItem);
            Assert.AreEqual(1, result[1].BadCt);
            Assert.AreEqual(d, result[2].UsingItem);
            Assert.AreEqual(1, result[2].BadCt);
            Assert.AreEqual(d, result[3].UsingItem);
            Assert.AreEqual(1, result[3].BadCt);
            Assert.AreEqual(e, result[4].UsingItem);
            Assert.AreEqual(1, result[4].BadCt);
        }

        [TestMethod]
        public void TestMaxLengthOfCycles() {
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var c = Item.New(ItemType.SIMPLE, "c");
            var d = Item.New(ItemType.SIMPLE, "d");
            var e = Item.New(ItemType.SIMPLE, "e");
            var deps = new[] {
                new Dependency(a, b, null, "", 1),
                new Dependency(b, c, null, "", 1),
                new Dependency(c, d, null, "", 1),
                new Dependency(d, e, null, "", 1),

                // Long cycle
                new Dependency(e, a, null, "", 1),

                // Short cycle
                new Dependency(c, b, null, "", 1),
            };
            var result = new List<Dependency>();

            new FindCycleDeps().Transform(new GlobalContext(), "test", deps, "{ -k -n 3 -q }", "test", result);

            result.Sort((x, y) => String.Compare(x.UsingItemAsString, y.UsingItemAsString, StringComparison.Ordinal));

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(b, result[0].UsingItem);
            Assert.AreEqual(0, result[0].BadCt);
            Assert.AreEqual(1, result[0].QuestionableCt);
            Assert.AreEqual(c, result[1].UsingItem);
            Assert.AreEqual(0, result[1].BadCt);
            Assert.AreEqual(1, result[1].QuestionableCt);
        }

        [TestMethod]
        public void TestAddReverseWithRemove() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(FindCycleDeps).Name, "{", "-k", "}",                
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                AssertEdgeCount(9, o);
            }
        }
    }
}
