using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering.TextWriting;
using NDepCheck.Transforming.PathFinding;
using NDepCheck.Transforming.Projecting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestProjectItems {
        private static void SmallTestForPrefixOptimizedProjector(Func<Projection[], bool, ProjectItems.IProjector> createProjector) {
            var pi = new ProjectItems(createProjector);
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            pi.Configure(gc, @"{ -pl
    $ (Ignore:Name) ---% SIMPLE

    ! :a* ---% A
    > :ab ---% AB
    ! :b* ---% B
    ! :c* ---% C
    ! :** ---% 
}", forceReload: false);

            ItemType generic2 = ItemType.Generic(2, ignoreCase: false);
            Item a = graph.CreateItem(generic2, "x:a");
            Item ab = graph.CreateItem(generic2, "x:ab");
            Item ac = graph.CreateItem(generic2, "x:ac");
            Item ca = graph.CreateItem(generic2, "x:ca");
            Item cb = graph.CreateItem(generic2, "x:cb");
            Item s = graph.CreateItem(generic2, "m:s");
            Item t = graph.CreateItem(generic2, "m:t");

            var result = new List<Dependency>();
            pi.Transform(gc, new[] {
                graph.CreateDependency(a, a, null, "a_a", 1), // the first surviving dependency
                graph.CreateDependency(a, s, null, "a_s", 1), // vanishes, because s is not mapped
                graph.CreateDependency(ab, s, null, "ab_s", 1), // same
                graph.CreateDependency(ca, s, null, "ca_s", 1), // etc.
                graph.CreateDependency(cb, cb, null, "cb_cb", 1), // the second surviving dependency
                graph.CreateDependency(cb, t, null, "cb_t", 1), // vanishes, because t is not mapped
                graph.CreateDependency(a, t, null, "a_t", 1),
                graph.CreateDependency(ac, t, null, "ac_t", 1),
                graph.CreateDependency(a, s, null, "a_s", 1),

                // Counts: 
                // !a  5
                // >ab 0
                // !ac 1
                // !b  0
                // !ca 1
                // !cb 3
                // !s  4
                // !t  3
            }, "", result);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("A", result[0].UsingItem.Values[0]);
            Assert.AreEqual("A", result[0].UsedItem.Values[0]);
            Assert.AreEqual("C", result[1].UsingItem.Values[0]);
            Assert.AreEqual("C", result[1].UsedItem.Values[0]);
        }

        [TestMethod]
        public void TestSmallSimpleProjector() {
            SmallTestForPrefixOptimizedProjector((p, i) => new ProjectItems.SimpleProjector(p, "simple"));
        }

        [TestMethod]
        public void TestSmallSelfOptimizingFirstLetterProjector() {
            ProjectItems.SelfOptimizingFirstLetterProjector usedProjector = null;
            SmallTestForPrefixOptimizedProjector((p, i) => usedProjector = new ProjectItems.SelfOptimizingFirstLetterProjector(p, i, 4, "1stLetter"));
            // Summed match counts: 
            // !a+>ab+!ac   6 _ 2nd
            // !b           0 _ 4th
            // !ca+!cb      4 _ 3rd
            // !s+!t        7 _ 1st
            // ... hoever, because of the "forgetting" feature, the order is not exactly as above
            ProjectItems.FirstLetterMatchProjector[] projectors = usedProjector.ProjectorsForTesting.ToArray();
            Assert.AreEqual("=a", projectors[0].Name);
            Assert.AreEqual("#abc", projectors[1].Name);
            Assert.AreEqual("=c", projectors[2].Name);
            Assert.AreEqual("=b", projectors[3].Name);
        }

        [TestMethod]
        public void TestSmallSelfOptimizingPrefixTrieProjector() {
            ProjectItems.SelfOptimizingPrefixTrieProjector usedProjector = null;
            SmallTestForPrefixOptimizedProjector((p, i) => usedProjector = new ProjectItems.SelfOptimizingPrefixTrieProjector(p, i, 2, "prefixTrie"));
            ProjectItems.TrieNodeProjector[] projectors = usedProjector.ProjectorsForTesting.ToArray();

            // Both projectors (for fieldNr 0 and fieldNr 1) actually ran - because we have only 5 rules, 
            // a single run should reduce the cost per projection to below 6
            Assert.IsTrue(projectors.All(p => p.CostPerProjection < 6));

            // And the projector for field 1 should have gained the lead.
            Assert.AreEqual("PrefixTrieNodeProjector$1", projectors[0].Name);
            Assert.AreEqual("PrefixTrieNodeProjector$0", projectors[1].Name);
        }

        [TestMethod]
        public void TestSmallSelfOptimizingPrefixTrieProjector2() {
            // This test found a problem in TrieNode.SetProjectors.
            var pi = new ProjectItems((p, i) => new ProjectItems.SelfOptimizingPrefixTrieProjector(p, i, 2, "prefixTrie"));
            var gc = new GlobalContext();
            pi.Configure(gc, @"{ -pl
    $ (:Name) ---% SIMPLE

    ! :abc ---% ADetail
    ! :a*  ---% A
    ! :t   ---% T
    ! :**  ---% 
}", forceReload: false);

            ItemType generic2 = ItemType.Generic(2, ignoreCase: false);
            WorkingGraph graph = gc.CurrentGraph;
            Item a = graph.CreateItem(generic2, "x:a");
            Item ab = graph.CreateItem(generic2, "x:ab");
            Item abc = graph.CreateItem(generic2, "x:abc");
            Item abcd = graph.CreateItem(generic2, "x:abcd");
            Item t = graph.CreateItem(generic2, "m:t");

            var result = new List<Dependency>();
            pi.Transform(gc, new[] {
                graph.CreateDependency(a, t, null, "a_t", 1), // A_T
                graph.CreateDependency(ab, t, null, "ab_t", 1), // A_ T
                graph.CreateDependency(abc, t, null, "abc_t", 1), // ADetail _T
                graph.CreateDependency(abcd, t, null, "abcd_t", 1), // A _ T
            }, "", result);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("A", result[0].UsingItem.Values[0]);
            Assert.AreEqual(3, result[0].Ct);
            Assert.AreEqual("ADetail", result[1].UsingItem.Values[0]);
            Assert.AreEqual(1, result[1].Ct);
        }


        [TestMethod]
        public void TestToTooFewFields() {
            var pi = new ProjectItems((p, i) => new ProjectItems.SelfOptimizingPrefixTrieProjector(p, i, 2, "prefixTrie"));
            var gc = new GlobalContext();
            const string THREE_FIELDS = "THREE_FIELDS";
            pi.Configure(gc, $@"{{ -pl
    $ {THREE_FIELDS}(F1:F2:F3) ---% {THREE_FIELDS}

    ! a:b:c ---% ::
    ! a:b   ---% : // this threw an exception before a fix
    ! a    ---%
}}", forceReload: false);

            ItemType threeFields = ItemType.Find(THREE_FIELDS);
            WorkingGraph graph = gc.CurrentGraph;
            Item abc = graph.CreateItem(threeFields, "a:b:c");
            Item ab = graph.CreateItem(threeFields, "a:b:-");
            Item a = graph.CreateItem(threeFields, "a:-:-");

            var result = new List<Dependency>();
            pi.Transform(gc, new[] {
                graph.CreateDependency(abc, abc, null, "abc", 1),
                graph.CreateDependency(ab, ab, null, "ab", 1),
                graph.CreateDependency(a, a, null, "a", 1),
            }, "", result);

            Assert.AreEqual(0, result.Count);
        }

        private Dependency Dep(WorkingGraph graph, Item from, Item to, int ct = 1, int questionable = 0) {
            return graph.CreateDependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }

        [TestMethod]
        public void TestBackProjectCycle() {
            ItemType generic2 = ItemType.Generic(2, ignoreCase: false);

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item a1 = graph.CreateItem(generic2, "a:1");
            Item b1 = graph.CreateItem(generic2, "b:1");
            Item c1 = graph.CreateItem(generic2, "c:1");
            Item d1 = graph.CreateItem(generic2, "d:1");
            Item e1 = graph.CreateItem(generic2, "e:1");
            Item a2 = graph.CreateItem(generic2, "a:2");
            Item b2 = graph.CreateItem(generic2, "b:2");
            Item c2 = graph.CreateItem(generic2, "c:2");
            Item d2 = graph.CreateItem(generic2, "d:2");
            Item e2 = graph.CreateItem(generic2, "e:2");
            Item x2 = graph.CreateItem(generic2, "x:2");

            var deps = new[] {
                Dep(graph, a1, b1), Dep(graph, b1, c1), Dep(graph, c1, d1), Dep(graph, d1, e1), Dep(graph, d1, b1),
                Dep(graph, a2, x2), Dep(graph, b2, x2), Dep(graph, c2, x2), Dep(graph, d2, x2), Dep(graph, e2, x2),
            };

            List<Dependency> backProjectedDeps = ProjectMarkCyclesAndBackProject(deps, gc);

            Assert.IsFalse(Find(backProjectedDeps, a1, b1).MarkersContain("C0"));
            Assert.IsTrue(Find(backProjectedDeps, b1, c1).MarkersContain("C0"));
            Assert.IsTrue(Find(backProjectedDeps, c1, d1).MarkersContain("C0"));
            Assert.IsFalse(Find(backProjectedDeps, d1, e1).MarkersContain("C0"));
            Assert.IsTrue(Find(backProjectedDeps, d1, b1).MarkersContain("C0"));

            Assert.IsFalse(Find(backProjectedDeps, a2, x2).MarkersContain("C0"));
            Assert.IsFalse(Find(backProjectedDeps, b2, x2).MarkersContain("C0"));
            Assert.IsFalse(Find(backProjectedDeps, c2, x2).MarkersContain("C0"));
            Assert.IsFalse(Find(backProjectedDeps, d2, x2).MarkersContain("C0"));
            Assert.IsFalse(Find(backProjectedDeps, e2, x2).MarkersContain("C0"));
        }

        [TestMethod]
        public void TestBackProjectSmallCycle() {
            ItemType generic2 = ItemType.Generic(2, ignoreCase: false);
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item a1 = graph.CreateItem(generic2, "a:1");
            Item b1 = graph.CreateItem(generic2, "b:1");
            Item a2 = graph.CreateItem(generic2, "a:2");
            Item b2 = graph.CreateItem(generic2, "b:2");
            Item b3 = graph.CreateItem(generic2, "b:3");

            var deps = new[] {
                Dep(graph, a1, b1), Dep(graph, b1, a2), Dep(graph, b2, a2), Dep(graph, a1, b3)
            };

            List<Dependency> backProjectedDeps = ProjectMarkCyclesAndBackProject(deps, gc);

            Assert.IsTrue(Find(backProjectedDeps, a1, b1).MarkersContain("C0"));
            Assert.IsTrue(Find(backProjectedDeps, a1, b3).MarkersContain("C0"));
            Assert.IsTrue(Find(backProjectedDeps, b1, a2).MarkersContain("C0"));
            Assert.IsTrue(Find(backProjectedDeps, b2, a2).MarkersContain("C0"));

            var pw = new FlatPathWriter();
            using (var t = DisposingFile.CreateTempFileWithTail(".txt")) {
                pw.Render(gc, backProjectedDeps, $"{{ {FlatPathWriter.PathMarkerOption} C* }}".Replace(" ", Environment.NewLine), new WriteTarget(t.FileName, append: false, limitLinesForConsole: 100), ignoreCase: false);

                using (var sr = new StreamReader(t.FileName)) {
                    var o = sr.ReadToEnd();

                    //Console.WriteLine(o);
                    Assert.IsTrue(o.Contains("a:1"));
                    Assert.IsTrue(o.Contains("<= a:2 $"));
                }
            }
        }

        private static List<Dependency> ProjectMarkCyclesAndBackProject(Dependency[] deps, GlobalContext globalContext) {
            var backProjectedDeps = new List<Dependency>();

            globalContext.RenameCurrentGraph("INPUT");
            globalContext.CurrentGraph.AddDependencies(deps);

            var pi = new ProjectItems();
            pi.Configure(globalContext,
                $"{{ {ProjectItems.ProjectionsOption} $GENERIC_2---%SIMPLE !(**) }}".Replace(" ", Environment.NewLine),
                false);
            var projectedDeps = new List<Dependency>();
            pi.Transform(globalContext, deps, "", projectedDeps);

            var mc = new MarkCycleDeps();
            mc.Transform(globalContext, projectedDeps,
                $"{{ {MarkCycleDeps.ConsiderSelfCyclesOption} {MarkCycleDeps.AddIndexedMarkerOption} C }}".Replace(" ", Environment.NewLine), new List<Dependency>());

            pi.Transform(globalContext, projectedDeps,
                $"{{ {ProjectItems.BackProjectionGraphOption} INPUT }}".Replace(" ", Environment.NewLine),
                backProjectedDeps);

            return backProjectedDeps;
        }

        private Dependency Find(List<Dependency> deps, Item from, Item to) {
            return deps.FirstOrDefault(
                    d => d.UsingItemAsString == from.AsString() && d.UsedItemAsString == to.AsString());
        }


        //[TestMethod]
        //public void TestDamned() {
        //    var gc = new GlobalContext();

        //    ProjectItems.SimpleProjector simpleProjector = null;
        //    var piSimple = new ProjectItems((p, i) => simpleProjector = new ProjectItems.SimpleProjector(p, "simple"));
        //    piSimple.Configure(gc, @"{ -pf C:\PT\github\NDepCheck\examples\puf\PUF.dep }".Replace(" ", "\r\n"), true);

        //    ProjectItems.SelfOptimizingPrefixTrieProjector trieProjector = null;
        //    var piTrie = new ProjectItems((p, i) => trieProjector = new ProjectItems.SelfOptimizingPrefixTrieProjector(p, i, 1000, "prefixTrie"));
        //    piTrie.Configure(gc, @"{ -pf C:\PT\github\NDepCheck\examples\puf\PUF.dep }".Replace(" ", "\r\n"), true);

        //    AbstractDependencyReader rd = new DipReaderFactory().CreateReader(@"C:\PT\github\NDepCheck\examples\puf\PUF.dip", gc, false);
        //    var deps = rd.ReadDependencies(0).Dependencies;

        //    int n = 0;
        //    foreach (var d in deps) {
        //        {
        //            var s = simpleProjector.Project(d.UsingItem, true);
        //            var t = trieProjector.Project(d.UsingItem, true);

        //            Assert.AreEqual(s, t);
        //        }
        //        {
        //            var s = simpleProjector.Project(d.UsedItem, false);
        //            var t = trieProjector.Project(d.UsedItem, false);

        //            Assert.AreEqual(s, t);
        //        }
        //        n++;
        //    }
        //}
    }
}
