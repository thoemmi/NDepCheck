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
        private static IEnumerable<Dependency> Run(string options, IEnumerable<Dependency> dependencies) {
            var globalContext = new GlobalContext();
            try {
                var mmc = new MarkMinimalCutDeps();
                var result = new List<Dependency>();
                mmc.Transform(globalContext, dependencies ?? mmc.CreateSomeTestDependencies(), options.Replace(" ", "\r\n"), result);
                return result;
            } finally {
                // Also static caches must be reset, as "Mark" modifies Items
                globalContext.ResetAll();
            }
        }

        [TestMethod]
        public void TestMarkTrivialCut() {
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");
            Item d = Item.New(ItemType.SIMPLE, "d");
            var dependencies = new[] {
                new Dependency(a, b, null, "D10", 10, 0, 4),
                new Dependency(b, c, null, "D20", 20, 0, 2), // critical edge
                new Dependency(c, d, null, "D30", 30, 0, 1),
                new Dependency(c, d, null, "D40", 40, 0, 3),
            };

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} a " +
                                                 $"{MarkMinimalCutDeps.MatchTargetOption} d " +
                                                 $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == (z.Ct == 20)), string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkCutWithBackflow() {
            // Backflow problem from http://www.cs.princeton.edu/courses/archive/spring06/cos226/lectures/maxflow.pdf
            Item s = Item.New(ItemType.SIMPLE, "s");
            Item n2 = Item.New(ItemType.SIMPLE, "2");
            Item n3 = Item.New(ItemType.SIMPLE, "3");
            Item n4 = Item.New(ItemType.SIMPLE, "4");
            Item n5 = Item.New(ItemType.SIMPLE, "5");
            Item t = Item.New(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                new Dependency(s, n2, null, "s_2", 10, 0, 10),
                new Dependency(s, n4, null, "s_4", 20, 0, 4),

                new Dependency(n2, n3, null, "2_3", 30, 0, 13),
                new Dependency(n2, n5, null, "2_5", 40, 0, 4),

                new Dependency(n3, t, null, "3_t", 50, 0, 10),

                new Dependency(n4, n3, null, "4_3", 60, 0, 4),

                new Dependency(n5, t, null, "5_t", 70, 0, 4),
            };

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
                                                 $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                 $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == (Equals(z.UsingItem, s))),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        private static Dependency[] CreateExampleGraph() {
            // First graph (not the Soviet railways ...) from 
            // http://www.cs.princeton.edu/courses/archive/spring06/cos226/lectures/maxflow.pdf
            Item s = Item.New(ItemType.SIMPLE, "s");
            Item n2 = Item.New(ItemType.SIMPLE, "2");
            Item n3 = Item.New(ItemType.SIMPLE, "3");
            Item n4 = Item.New(ItemType.SIMPLE, "4");
            Item n5 = Item.New(ItemType.SIMPLE, "5");
            Item n6 = Item.New(ItemType.SIMPLE, "6");
            Item n7 = Item.New(ItemType.SIMPLE, "7");
            Item t = Item.New(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                new Dependency(s, n2, null, "s_2", 12, 0, 10), new Dependency(s, n3, null, "s_3", 13, 0, 5),
                new Dependency(s, n4, null, "s_4", 14, 0, 15), new Dependency(n2, n3, null, "2_3", 23, 0, 4),
                new Dependency(n2, n5, null, "2_5", 25, 0, 15), new Dependency(n2, n6, null, "2_6", 26, 0, 9),
                new Dependency(n3, n4, null, "3_4", 34, 0, 4), new Dependency(n3, n6, null, "3_6", 36, 0, 8),
                new Dependency(n4, n7, null, "4_7", 47, 0, 30), new Dependency(n5, n6, null, "5_6", 56, 0, 15),
                new Dependency(n5, t, null, "5_t", 58, 0, 10), new Dependency(n6, n7, null, "6_7", 67, 0, 15),
                new Dependency(n6, t, null, "6_t", 68, 0, 10), new Dependency(n7, n3, null, "7_3", 73, 0, 6),
                new Dependency(n7, t, null, "7_t", 78, 0, 10),
            };
            return dependencies;
        }

        [TestMethod]
        public void TestMarkCut() {
            Dependency[] dependencies = CreateExampleGraph();

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
                                                 $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                 $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == new[] { 12, 36, 78 }.Contains(z.Ct)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkCutFromMultipleSources() {
            Dependency[] exampleDependencies = CreateExampleGraph();
            Item s = exampleDependencies[0].UsingItem;
            Item r0 = Item.New(ItemType.SIMPLE, "r0");
            Item r1 = Item.New(ItemType.SIMPLE, "r1");
            Item r2 = Item.New(ItemType.SIMPLE, "r2");
            Dependency[] dependencies = exampleDependencies.Concat(new[] {
                new Dependency(r0, s, null, "r0_s", 1000, 0, 1000),
                new Dependency(r1, s, null, "r1_s", 1000, 0, 1000),
                new Dependency(r2, s, null, "r2_s", 1000, 0, 1000),
            }).ToArray();

            const string mark = "CUT";
            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} r* " +
                                                 $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                 $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == new[] { 12, 36, 78 }.Contains(z.Ct)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkAnotherCut() {
            // Graph from http://www.cs.princeton.edu/courses/archive/spring06/cos226/lectures/maxflow.pdf p.30 (and Wikipedia)
            Item s = Item.New(ItemType.SIMPLE, "s");
            Item n2 = Item.New(ItemType.SIMPLE, "2");
            Item n4 = Item.New(ItemType.SIMPLE, "4");
            Item t = Item.New(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                new Dependency(s, n2, null, "s_2", 102, 0, 100),
                new Dependency(s, n4, null, "s_4", 104, 0, 100),

                new Dependency(n2, t, null, "2_t", 203, 0, 100),

                new Dependency(n4, n2, null, "4_7", 402, 0, 1),
                new Dependency(n4, t, null, "4_7", 403, 0, 100),
            };

            const string mark = "CUT";
            const string source = "SOURCE";

            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
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
            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} s " +
                                                 $"{MarkMinimalCutDeps.MatchTargetOption} t " +
                                                 $"{MarkMinimalCutDeps.UseQuestionableCountOption} " +
                                                 $"{MarkMinimalCutDeps.DepsMarkerOption} {mark} }}", null);
            Assert.IsTrue(result.All(z => z.MarkersContain(mark) == new[] { 112, 142, 145 }.Contains(z.Ct)),
                          string.Join("\r\n", result.Select(z => z.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: false))));
        }

        [TestMethod]
        public void TestMarkZeroCut() {
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");
            Item d = Item.New(ItemType.SIMPLE, "d");
            var dependencies = new[] {
                new Dependency(a, b, null, "D10", 10, 0, 4),
                new Dependency(c, d, null, "D30", 30, 0, 1),
                new Dependency(c, d, null, "D40", 40, 0, 3),
            };

            const string mark = "CUT";
            const string source = "SOURCE";

            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} a " +
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
