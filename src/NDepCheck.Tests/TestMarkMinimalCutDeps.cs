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
                mmc.Transform(gc, dependencies ?? mmc.CreateSomeTestDependencies(gc.CurrentEnvironment), options.Replace(" ", "\r\n"), result);
                return result;
            } finally {
                // Also static caches must be reset, as "Mark" modifies Items
                gc.ResetAll();
            }
        }

        [TestMethod]
        public void TestMarkTrivialCut() {
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;
            Item a = env.NewItem(ItemType.SIMPLE, "a");
            Item b = env.NewItem(ItemType.SIMPLE, "b");
            Item c = env.NewItem(ItemType.SIMPLE, "c");
            Item d = env.NewItem(ItemType.SIMPLE, "d");
            var dependencies = new[] {
                env.CreateDependency(a, b, null, "D10", 10, 0, 4),
                env.CreateDependency(b, c, null, "D20", 20, 0, 2), // critical edge
                env.CreateDependency(c, d, null, "D30", 30, 0, 1),
                env.CreateDependency(c, d, null, "D40", 40, 0, 3),
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
            Environment env = gc.CurrentEnvironment;
            Item s = env.NewItem(ItemType.SIMPLE, "s");
            Item n2 = env.NewItem(ItemType.SIMPLE, "2");
            Item n3 = env.NewItem(ItemType.SIMPLE, "3");
            Item n4 = env.NewItem(ItemType.SIMPLE, "4");
            Item n5 = env.NewItem(ItemType.SIMPLE, "5");
            Item t = env.NewItem(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                env.CreateDependency(s, n2, null, "s_2", 10, 0, 10),
                env.CreateDependency(s, n4, null, "s_4", 20, 0, 4),

                env.CreateDependency(n2, n3, null, "2_3", 30, 0, 13),
                env.CreateDependency(n2, n5, null, "2_5", 40, 0, 4),

                env.CreateDependency(n3, t, null, "3_t", 50, 0, 10),

                env.CreateDependency(n4, n3, null, "4_3", 60, 0, 4),

                env.CreateDependency(n5, t, null, "5_t", 70, 0, 4),
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
            var env = gc.CurrentEnvironment;
            Item s = env.NewItem(ItemType.SIMPLE, "s");
            Item n2 = env.NewItem(ItemType.SIMPLE, "2");
            Item n3 = env.NewItem(ItemType.SIMPLE, "3");
            Item n4 = env.NewItem(ItemType.SIMPLE, "4");
            Item n5 = env.NewItem(ItemType.SIMPLE, "5");
            Item n6 = env.NewItem(ItemType.SIMPLE, "6");
            Item n7 = env.NewItem(ItemType.SIMPLE, "7");
            Item t = env.NewItem(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                env.CreateDependency(s, n2, null, "s_2", 12, 0, 10), env.CreateDependency(s, n3, null, "s_3", 13, 0, 5),
                env.CreateDependency(s, n4, null, "s_4", 14, 0, 15), env.CreateDependency(n2, n3, null, "2_3", 23, 0, 4),
                env.CreateDependency(n2, n5, null, "2_5", 25, 0, 15), env.CreateDependency(n2, n6, null, "2_6", 26, 0, 9),
                env.CreateDependency(n3, n4, null, "3_4", 34, 0, 4), env.CreateDependency(n3, n6, null, "3_6", 36, 0, 8),
                env.CreateDependency(n4, n7, null, "4_7", 47, 0, 30), env.CreateDependency(n5, n6, null, "5_6", 56, 0, 15),
                env.CreateDependency(n5, t, null, "5_t", 58, 0, 10), env.CreateDependency(n6, n7, null, "6_7", 67, 0, 15),
                env.CreateDependency(n6, t, null, "6_t", 68, 0, 10), env.CreateDependency(n7, n3, null, "7_3", 73, 0, 6),
                env.CreateDependency(n7, t, null, "7_t", 78, 0, 10),
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
            Environment env = gc.CurrentEnvironment;
            Item r0 = env.NewItem(ItemType.SIMPLE, "r0");
            Item r1 = env.NewItem(ItemType.SIMPLE, "r1");
            Item r2 = env.NewItem(ItemType.SIMPLE, "r2");
            Dependency[] dependencies = exampleDependencies.Concat(new[] {
                env.CreateDependency(r0, s, null, "r0_s", 1000, 0, 1000),
                env.CreateDependency(r1, s, null, "r1_s", 1000, 0, 1000),
                env.CreateDependency(r2, s, null, "r2_s", 1000, 0, 1000),
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
            Environment env = gc.CurrentEnvironment;
            Item s = env.NewItem(ItemType.SIMPLE, "s");
            Item n2 = env.NewItem(ItemType.SIMPLE, "2");
            Item n4 = env.NewItem(ItemType.SIMPLE, "4");
            Item t = env.NewItem(ItemType.SIMPLE, "t");
            var dependencies = new[] {
                env.CreateDependency(s, n2, null, "s_2", 102, 0, 100),
                env.CreateDependency(s, n4, null, "s_4", 104, 0, 100),

                env.CreateDependency(n2, t, null, "2_t", 203, 0, 100),

                env.CreateDependency(n4, n2, null, "4_7", 402, 0, 1),
                env.CreateDependency(n4, t, null, "4_7", 403, 0, 100),
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
            Environment env = gc.CurrentEnvironment;
            Item a = env.NewItem(ItemType.SIMPLE, "a");
            Item b = env.NewItem(ItemType.SIMPLE, "b");
            Item c = env.NewItem(ItemType.SIMPLE, "c");
            Item d = env.NewItem(ItemType.SIMPLE, "d");
            var dependencies = new[] {
                env.CreateDependency(a, b, null, "D10", 10, 0, 4),
                env.CreateDependency(c, d, null, "D30", 30, 0, 1),
                env.CreateDependency(c, d, null, "D40", 40, 0, 3),
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
