using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using NDepCheck.Markers;
using NDepCheck.Matching;
using NDepCheck.Transforming.SpecialDependencyMarking;

namespace NDepCheck.Tests {
    public static class TestUtils {
        public static bool MarkersContain(this IWithMarkerSet d, string s) {
            CountPattern<IMatcher>.Eval eval = MarkerMatch.CreateEval(s + ">0", ignoreCase: false);
            return d.MarkerSet.IsMatch(new[] {eval});
        }
    }

    [TestClass]
    public class TestMarkMinimalCutDeps {
        private static IEnumerable<Dependency> Run(GlobalContext gc, string options, IEnumerable<Dependency> dependencies) {
            try {
                var mmc = new MarkMinimalCutDeps();
                var result = new List<Dependency>();
                mmc.Transform(gc, dependencies ?? mmc.CreateSomeTestDependencies(gc.CurrentGraph), options.Replace(" ", "\r\n"), result, s => null);
                return result;
            } finally {
                // Also static caches must be reset, as "Mark" modifies Items
                gc.ResetAll();
            }
        }

        [TestMethod]
        public void TestMarkTrivialCut() {
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            Item a = graph.CreateItem(ItemType.SIMPLE, "a");
            Item b = graph.CreateItem(ItemType.SIMPLE, "b");
            Item c = graph.CreateItem(ItemType.SIMPLE, "c");
            Item d = graph.CreateItem(ItemType.SIMPLE, "d");
            var dependencies = new[] {
                graph.CreateDependency(a, b, null, "D10", 10, 0, 4, notOkReason: "test data"),
                graph.CreateDependency(b, c, null, "D20", 20, 0, 2, notOkReason: "test data"), // critical edge
                graph.CreateDependency(c, d, null, "D30", 30, 0, 1, notOkReason: "test data"),
                graph.CreateDependency(c, d, null, "D40", 40, 0, 3, notOkReason: "test data"),
            };

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run(gc, $"{{ {MarkMinimalCutDeps.MatchSourceOption} a " +
                                                     $"{MarkMinimalCutDeps.MatchTargetOption} d " +
                                                     $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == (z.Ct == 20)), string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkCutWithBackflow() {
            // Backflow problem from http://www.cs.princeton.edu/courses/archive/spring06/cos226/lectures/maxflow.pdf
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            Item s = graph.CreateItem(ItemType.SIMPLE, "s");
            Item n2 = graph.CreateItem(ItemType.SIMPLE, "2");
            Item n3 = graph.CreateItem(ItemType.SIMPLE, "3");
            Item n4 = graph.CreateItem(ItemType.SIMPLE, "4");
            Item n5 = graph.CreateItem(ItemType.SIMPLE, "5");
            Item t = graph.CreateItem(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                graph.CreateDependency(s, n2, null, "s_2", 10, 0, 10, notOkReason: "test data"),
                graph.CreateDependency(s, n4, null, "s_4", 20, 0, 4, notOkReason: "test data"),

                graph.CreateDependency(n2, n3, null, "2_3", 30, 0, 13, notOkReason: "test data"),
                graph.CreateDependency(n2, n5, null, "2_5", 40, 0, 4, notOkReason: "test data"),

                graph.CreateDependency(n3, t, null, "3_t", 50, 0, 10, notOkReason: "test data"),

                graph.CreateDependency(n4, n3, null, "4_3", 60, 0, 4, notOkReason: "test data"),

                graph.CreateDependency(n5, t, null, "5_t", 70, 0, 4, notOkReason: "test data"),
            };

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run(gc, $"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
                                                     $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                     $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == (Equals(z.UsingItem, s))),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        private static Dependency[] CreateExampleGraph(GlobalContext gc) {
            // First graph (not the Soviet railways ...) from 
            // http://www.cs.princeton.edu/courses/archive/spring06/cos226/lectures/maxflow.pdf
            var graph = gc.CurrentGraph;
            Item s = graph.CreateItem(ItemType.SIMPLE, "s");
            Item n2 = graph.CreateItem(ItemType.SIMPLE, "2");
            Item n3 = graph.CreateItem(ItemType.SIMPLE, "3");
            Item n4 = graph.CreateItem(ItemType.SIMPLE, "4");
            Item n5 = graph.CreateItem(ItemType.SIMPLE, "5");
            Item n6 = graph.CreateItem(ItemType.SIMPLE, "6");
            Item n7 = graph.CreateItem(ItemType.SIMPLE, "7");
            Item t = graph.CreateItem(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                graph.CreateDependency(s, n2, null, "s_2", 12, 0, 10, notOkReason: "test data"),
                graph.CreateDependency(s, n3, null, "s_3", 13, 0, 5, notOkReason: "test data"),
                graph.CreateDependency(s, n4, null, "s_4", 14, 0, 15, notOkReason: "test data"),
                graph.CreateDependency(n2, n3, null, "2_3", 23, 0, 4, notOkReason: "test data"),
                graph.CreateDependency(n2, n5, null, "2_5", 25, 0, 15, notOkReason: "test data"),
                graph.CreateDependency(n2, n6, null, "2_6", 26, 0, 9, notOkReason: "test data"),
                graph.CreateDependency(n3, n4, null, "3_4", 34, 0, 4, notOkReason: "test data"),
                graph.CreateDependency(n3, n6, null, "3_6", 36, 0, 8, notOkReason: "test data"),
                graph.CreateDependency(n4, n7, null, "4_7", 47, 0, 30, notOkReason: "test data"),
                graph.CreateDependency(n5, n6, null, "5_6", 56, 0, 15, notOkReason: "test data"),
                graph.CreateDependency(n5, t, null, "5_t", 58, 0, 10, notOkReason: "test data"),
                graph.CreateDependency(n6, n7, null, "6_7", 67, 0, 15, notOkReason: "test data"),
                graph.CreateDependency(n6, t, null, "6_t", 68, 0, 10, notOkReason: "test data"),
                graph.CreateDependency(n7, n3, null, "7_3", 73, 0, 6, notOkReason: "test data"),
                graph.CreateDependency(n7, t, null, "7_t", 78, 0, 10, notOkReason: "test data"),
            };
            return dependencies;
        }

        [TestMethod]
        public void TestMarkCut() {
            var gc = new GlobalContext();
            Dependency[] dependencies = CreateExampleGraph(gc);

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run(gc, $"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
                                                     $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                     $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == new[] { 12, 36, 78 }.Contains(z.Ct)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkCutFromMultipleSources() {
            var gc = new GlobalContext();
            Dependency[] exampleDependencies = CreateExampleGraph(gc);
            Item s = exampleDependencies[0].UsingItem;
            WorkingGraph graph = gc.CurrentGraph;
            Item r0 = graph.CreateItem(ItemType.SIMPLE, "r0");
            Item r1 = graph.CreateItem(ItemType.SIMPLE, "r1");
            Item r2 = graph.CreateItem(ItemType.SIMPLE, "r2");
            Dependency[] dependencies = exampleDependencies.Concat(new[] {
                graph.CreateDependency(r0, s, null, "r0_s", 1000, 0, 1000, notOkReason: "test data"),
                graph.CreateDependency(r1, s, null, "r1_s", 1000, 0, 1000, notOkReason: "test data"),
                graph.CreateDependency(r2, s, null, "r2_s", 1000, 0, 1000, notOkReason: "test data"),
            }).ToArray();

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run(gc, $"{{ {MarkMinimalCutDeps.MatchSourceOption} r* " +
                                                     $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                     $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == new[] { 12, 36, 78 }.Contains(z.Ct)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkAnotherCut() {
            // Graph from http://www.cs.princeton.edu/courses/archive/spring06/cos226/lectures/maxflow.pdf p.30 (and Wikipedia)
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            Item s = graph.CreateItem(ItemType.SIMPLE, "s");
            Item n2 = graph.CreateItem(ItemType.SIMPLE, "2");
            Item n4 = graph.CreateItem(ItemType.SIMPLE, "4");
            Item t = graph.CreateItem(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                graph.CreateDependency(s, n2, null, "s_2", 102, 0, 100, notOkReason: "test data"),
                graph.CreateDependency(s, n4, null, "s_4", 104, 0, 100, notOkReason: "test data"),

                graph.CreateDependency(n2, t, null, "2_t", 203, 0, 100, notOkReason: "test data"),

                graph.CreateDependency(n4, n2, null, "4_7", 402, 0, 1, notOkReason: "test data"),
                graph.CreateDependency(n4, t, null, "4_7", 403, 0, 100, notOkReason: "test data"),
            };

            const string mark = "CUT";
            const string source = "SOURCE";

            IEnumerable<Dependency> result = Run(gc, $"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
                                                     $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                     $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} " +
                                                     $"{MarkMinimalCutDeps.SourceMarkerOption} {source} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == new[] { 102, 104 }.Contains(z.Ct)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
            Assert.IsTrue(s.MarkersContain(source));
            Assert.IsFalse(n2.MarkersContain(source));
            Assert.IsFalse(n4.MarkersContain(source));
            Assert.IsFalse(t.MarkersContain(source));
        }


        [TestMethod]
        public void TestMarkYetAnotherCut() {
            const string mark = "CUT";
            IEnumerable<Dependency> result = Run(new GlobalContext(), $"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
                                                     $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                     $"{MarkMinimalCutDeps.UseQuestionableCountOption} " +
                                                     $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", null);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == new[] { 112, 142, 145 }.Contains(z.Ct)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkZeroCut() {
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            Item a = graph.CreateItem(ItemType.SIMPLE, "a");
            Item b = graph.CreateItem(ItemType.SIMPLE, "b");
            Item c = graph.CreateItem(ItemType.SIMPLE, "c");
            Item d = graph.CreateItem(ItemType.SIMPLE, "d");
            var dependencies = new[] {
                graph.CreateDependency(a, b, null, "D10", 10, 0, 4, notOkReason: "test data"),
                graph.CreateDependency(c, d, null, "D30", 30, 0, 1, notOkReason: "test data"),
                graph.CreateDependency(c, d, null, "D40", 40, 0, 3, notOkReason: "test data"),
            };

            const string mark = "CUT";
            const string source = "SOURCE";

            IEnumerable<Dependency> result = Run(gc, $"{{ {MarkMinimalCutDeps.MatchSourceOption} a " +
                                                     $"{MarkMinimalCutDeps.MatchTargetOption} d " +
                                                     $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} " +
                                                     $"{MarkMinimalCutDeps.SourceMarkerOption} {source} }}", dependencies);
            Assert.IsTrue(result.All(z => !z.MarkersContain(mark)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
            Assert.IsTrue(a.MarkersContain(source));
            Assert.IsTrue(b.MarkersContain(source));
            Assert.IsFalse(c.MarkersContain(source));
            Assert.IsFalse(d.MarkersContain(source));
        }
    }
}
