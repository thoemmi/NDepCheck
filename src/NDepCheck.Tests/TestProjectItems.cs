using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Projecting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestProjectItems {
        private static void SmallTestForPrefixOptimizedProjector(Func<Projection[], bool, ProjectItems.IProjector> createProjector) {
            var pi = new ProjectItems(createProjector);
            var gc = new GlobalContext();
            pi.Configure(gc, @"{ -pl
    $ IgnoreName(Ignore:Name) ---% SIMPLE

    ! :a* ---% A
    > :ab ---% AB
    ! :b* ---% B
    ! :c* ---% C
    ! :** ---% 
}", forceReload: false);

            Item a = Item.New(ItemType.Generic(2), "x:a");
            Item ab = Item.New(ItemType.Generic(2), "x:ab");
            Item ac = Item.New(ItemType.Generic(2), "x:ac");
            Item ca = Item.New(ItemType.Generic(2), "x:ca");
            Item cb = Item.New(ItemType.Generic(2), "x:cb");
            Item s = Item.New(ItemType.Generic(2), "m:s");
            Item t = Item.New(ItemType.Generic(2), "m:t");

            var result = new List<Dependency>();
            pi.Transform(gc, "test", new[] {
                new Dependency(a, a, null, "a->a", 1), // the first surviving dependency
                new Dependency(a, s, null, "a->s", 1), // vanishes, because s is not mapped
                new Dependency(ab, s, null, "ab->s", 1), // same
                new Dependency(ca, s, null, "ca->s", 1), // etc.
                new Dependency(cb, cb, null, "cb->cb", 1), // the second surviving dependency
                new Dependency(cb, t, null, "cb->t", 1), // vanishes, because t is not mapped
                new Dependency(a, t, null, "a->t", 1),
                new Dependency(ac, t, null, "ac->t", 1),
                new Dependency(a, s, null, "a->s", 1),

                // Counts: 
                // !a  5
                // >ab 0
                // !ac 1
                // !b  0
                // !ca 1
                // !cb 3
                // !s  4
                // !t  3
            }, "", "test", result);

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
            // !a+>ab+!ac   6 -> 2nd
            // !b           0 -> 4th
            // !ca+!cb      4 -> 3rd
            // !s+!t        7 -> 1st
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
            ProjectItems.IProjector usedProjector = null;
            var pi = new ProjectItems((p, i) => usedProjector = new ProjectItems.SelfOptimizingPrefixTrieProjector(p, i, 2, "prefixTrie"));
            var gc = new GlobalContext();
            pi.Configure(gc, @"{ -pl
    $ IgnoreName(Ignore:Name) ---% SIMPLE

    ! :abc ---% ADetail
    ! :a*  ---% A
    ! :t   ---% T
    ! :**  ---% 
}", forceReload: false);

            Item a = Item.New(ItemType.Generic(2), "x:a");
            Item ab = Item.New(ItemType.Generic(2), "x:ab");
            Item abc = Item.New(ItemType.Generic(2), "x:abc");
            Item abcd = Item.New(ItemType.Generic(2), "x:abcd");
            Item t = Item.New(ItemType.Generic(2), "m:t");

            var result = new List<Dependency>();
            pi.Transform(gc, "test", new[] {
                new Dependency(a, t, null, "a->t", 1), // A->T
                new Dependency(ab, t, null, "ab->t", 1), // A-> T
                new Dependency(abc, t, null, "abc->t", 1), // ADetail ->T
                new Dependency(abcd, t, null, "abcd->t", 1), // A -> T
            }, "", "test", result);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("A", result[0].UsingItem.Values[0]);
            Assert.AreEqual(3, result[0].Ct);
            Assert.AreEqual("ADetail", result[1].UsingItem.Values[0]);
            Assert.AreEqual(1, result[1].Ct);
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
