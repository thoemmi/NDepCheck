using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering.TextWriting;
using NDepCheck.Transforming.PathFinding;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestMarkCycleDeps {
        [TestInitialize]
        public void TestInitialize() {
            new GlobalContext().ResetAll();
        }

        private void AssertEdgeCount(int expected, string o) {
            string[] lines = o.Split('\n');
            int actual = lines.Count(line => line.Contains(Dependency.DIP_ARROW));
            Assert.AreEqual(expected, actual, $"Number of deps is different: '{o}'");
        }

        [TestMethod]
        public void TestMarkSmallCycle() {
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(ItemType.SIMPLE, "a");
            var b = env.NewItem(ItemType.SIMPLE, "b");
            var deps = new[] { env.CreateDependency(a, b, null, "", 1), env.CreateDependency(b, a, null, "", 1), };
            var result = new List<Dependency>();

            new MarkCycleDeps().Transform(gc, deps, "", result);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].BadCt);
            Assert.AreEqual(1, result[1].BadCt);
        }

        [TestMethod]
        public void TestMarkLaterCycleWithExplicitAsserts() {
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(ItemType.SIMPLE, "a");
            var b = env.NewItem(ItemType.SIMPLE, "b");
            var c = env.NewItem(ItemType.SIMPLE, "c");
            var d = env.NewItem(ItemType.SIMPLE, "d");
            var e = env.NewItem(ItemType.SIMPLE, "e");
            var deps = new[] {
                env.CreateDependency(a, b, null, "", 1),
                env.CreateDependency(b, c, null, "", 1),
                env.CreateDependency(c, d, null, "", 1),
                env.CreateDependency(d, e, null, "", 1),
                env.CreateDependency(e, c, null, "", 1),
            };
            var result = new List<Dependency>();

            new MarkCycleDeps().Transform(gc, deps, MarkCycleDeps.KeepOnlyCyclesOption.Opt, result);

            result.Sort((x, y) => string.Compare(x.UsingItemAsString, y.UsingItemAsString, StringComparison.Ordinal));

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
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(ItemType.SIMPLE, "a");
            var b = env.NewItem(ItemType.SIMPLE, "b");
            var c = env.NewItem(ItemType.SIMPLE, "c");
            var d = env.NewItem(ItemType.SIMPLE, "d");
            var e = env.NewItem(ItemType.SIMPLE, "e");

            List<Dependency> result = CreateDependenciesAndFindCycles(gc, a, b, c, d, e, keepOnlyCyclesOption: true, markerPrefix: "Kreis");

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

        private static List<Dependency> CreateDependenciesAndFindCycles(GlobalContext gc, Item a, Item b, Item c, Item d, Item e, bool keepOnlyCyclesOption, string markerPrefix) {
            Environment env = gc.CurrentEnvironment;

            var deps = new[] {
                // "Confusing" edges to sink b
                env.CreateDependency(a, b, null, "", 1),
                env.CreateDependency(b, b, null, "", 1),
                env.CreateDependency(c, b, null, "", 1),
                env.CreateDependency(d, b, null, "", 1),

                // a->c->d->e
                env.CreateDependency(a, c, null, "", 1),
                env.CreateDependency(c, d, null, "", 1),
                env.CreateDependency(d, e, null, "", 1),

                // Cycle edges c<-d and a<-e
                env.CreateDependency(d, c, null, "", 1),
                env.CreateDependency(e, a, null, "", 1),
            };
            var result = new List<Dependency>();

            new MarkCycleDeps().Transform(gc, deps,
                $"{{ {MarkCycleDeps.AddIndexedMarkerOption} {markerPrefix} {(keepOnlyCyclesOption ? MarkCycleDeps.KeepOnlyCyclesOption.Opt : "")} {MarkCycleDeps.IgnoreSelfCyclesOption}  }}"
                    .Replace(" ", "\r\n"), result);

            result.Sort((x, y) => string.Compare(x.UsingItemAsString, y.UsingItemAsString, StringComparison.Ordinal));
            return result;
        }

        [TestMethod]
        public void TestMaxLengthOfCycles() {
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(ItemType.SIMPLE, "a");
            var b = env.NewItem(ItemType.SIMPLE, "b");
            var c = env.NewItem(ItemType.SIMPLE, "c");
            var d = env.NewItem(ItemType.SIMPLE, "d");
            var e = env.NewItem(ItemType.SIMPLE, "e");
            var deps = new[] {
                env.CreateDependency(a, b, null, "", 1),
                env.CreateDependency(b, c, null, "", 1),
                env.CreateDependency(c, d, null, "", 1),
                env.CreateDependency(d, e, null, "", 1),

                // Long cycle
                env.CreateDependency(e, a, null, "", 1),

                // Short cycle
                env.CreateDependency(c, b, null, "", 1),
            };
            var result = new List<Dependency>();

            const string marker = "Cycle";
            new MarkCycleDeps().Transform(gc, deps,
            ($"{{ {MarkCycleDeps.KeepOnlyCyclesOption} " +
             $"{MarkCycleDeps.MaxCycleLengthOption} 3 " +
             $"{MarkCycleDeps.EffectOptions.AddMarkerOption} {marker} }}").Replace(" ", "\r\n"), result);

            result.Sort((x, y) => string.Compare(x.UsingItemAsString, y.UsingItemAsString, StringComparison.Ordinal));

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(b, result[0].UsingItem);
            Assert.IsTrue(result[0].MarkerSet.AsFullString().Contains(marker));
            Assert.AreEqual(c, result[1].UsingItem);
            Assert.IsTrue(result[1].MarkerSet.AsFullString().Contains(marker));
        }

        [TestMethod]
        public void TestKeepOnlyCycles() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(MarkCycleDeps).Name, "{", MarkCycleDeps.KeepOnlyCyclesOption.Opt, "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                AssertEdgeCount(9, o);
            }
        }

        [TestMethod]
        public void TestMarkLaterCycle() {
            const string cycleMarkerPrefix = "C";
            List<Dependency> result = FindLaterCycle(cycleMarkerPrefix);
            AssertIsMarkedAsCycle(cycleMarkerPrefix + "0", result.Skip(2), result.Take(2));
        }

        private static List<Dependency> FindLaterCycle(string cycleMarkerPrefix) {
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(ItemType.SIMPLE, "a");
            var b = env.NewItem(ItemType.SIMPLE, "b");
            var c = env.NewItem(ItemType.SIMPLE, "c");
            var d = env.NewItem(ItemType.SIMPLE, "d");
            var e = env.NewItem(ItemType.SIMPLE, "e");
            var deps = new[] {
                env.CreateDependency(a, b, null, "", 1),
                env.CreateDependency(b, c, null, "", 1),
                env.CreateDependency(c, d, null, "", 1),
                env.CreateDependency(d, e, null, "", 1),
                env.CreateDependency(e, c, null, "", 1),
            };
            var result = new List<Dependency>();

            new MarkCycleDeps().Transform(gc, deps,
                $"{{ {MarkCycleDeps.AddIndexedMarkerOption} {cycleMarkerPrefix} }}".Replace(" ", System.Environment.NewLine),
                result);

            result.Sort((x, y) => string.Compare(x.UsingItemAsString, y.UsingItemAsString, StringComparison.Ordinal));
            return result;
        }

        private void AssertIsMarkedAsCycle(string marker, IEnumerable<Dependency> cycle, IEnumerable<Dependency> notCycle) {
            {
                Dependency[] cycleAsArray = cycle.ToArray();
                for (var i = 0; i < cycleAsArray.Length; i++) {
                    int markerValue = cycleAsArray[i].MarkerSet.GetValue(marker, false);
                    Assert.IsTrue(markerValue > 0, $"Wrong marker {markerValue} @ {i}");
                    Assert.AreEqual(i == 0 ? PathSupport.IS_START : 0, markerValue & PathSupport.IS_START, $"Wrong IS_START @ {i}");
                    Assert.AreEqual(i == cycleAsArray.Length - 1 ? PathSupport.IS_END : 0, markerValue & PathSupport.IS_END,
                        $"Wrong IS_END @ {i}");
                    Assert.AreEqual(i == cycleAsArray.Length - 1 ? PathSupport.IS_LOOPBACK : 0, markerValue & PathSupport.IS_LOOPBACK,
                        $"Wrong IS_LOOPBACK @ {i}");
                }
            }
            {
                Dependency[] notCycleAsArray = notCycle.ToArray();
                for (var i = 0; i < notCycleAsArray.Length; i++) {
                    int markerValue = notCycleAsArray[i].MarkerSet.GetValue(marker, false);
                    Assert.AreEqual(0, markerValue, $"Wrong marker @ {i}");
                }
            }
        }


        [TestMethod]
        public void TestWriteLaterCycle() {
            const string cycleMarkerPrefix = "C";
            List<Dependency> dependencies = FindLaterCycle(cycleMarkerPrefix);
            var gc = new GlobalContext();
            using (var s = new MemoryStream()) {
                var w = new FlatPathWriter();
                w.RenderToStreamForUnitTests(gc, dependencies, s, cycleMarkerPrefix + "* -sm");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"-- C0
SIMPLE:c'C0
SIMPLE:d
SIMPLE:e
<= SIMPLE:c'C0 $", result.Trim());
            }
        }

        [TestMethod]
        public void TestWriteOverlappingCycles() {
            const string cycleMarkerPrefix = "X";

            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(ItemType.SIMPLE, "a");
            var b = env.NewItem(ItemType.SIMPLE, "b");
            var c = env.NewItem(ItemType.SIMPLE, "c");
            var d = env.NewItem(ItemType.SIMPLE, "d");
            var e = env.NewItem(ItemType.SIMPLE, "e");

            List<Dependency> dependencies = CreateDependenciesAndFindCycles(gc, a, b, c, d, e, keepOnlyCyclesOption: false, markerPrefix: cycleMarkerPrefix);

            using (var s = new MemoryStream()) {
                var w = new FlatPathWriter();
                w.RenderToStreamForUnitTests(gc, dependencies, s, cycleMarkerPrefix + "* -sm");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"-- X0
SIMPLE:a'X0
SIMPLE:c'X1
SIMPLE:d
SIMPLE:e
<= SIMPLE:a'X0 $

-- X1
SIMPLE:c'X1
SIMPLE:d
<= SIMPLE:c'X1 $", result.Trim());
            }

        }
    }
}