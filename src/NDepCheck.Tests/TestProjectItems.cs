using System;
using System.Collections.Generic;
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
            Item ca = Item.New(ItemType.Generic(2), "x:ca");
            Item cb = Item.New(ItemType.Generic(2), "x:cb");
            Item s = Item.New(ItemType.Generic(2), "m:s");
            Item t = Item.New(ItemType.Generic(2), "m:t");

            var result = new List<Dependency>();
            pi.Transform(gc, "test", new[] {
                new Dependency(a, a, null, "a->a", 1), // the only surviving dependency
                new Dependency(a, s, null, "a->s", 1), // vanishes, because s is not mapped
                new Dependency(ab, s, null, "ab->s", 1), // same
                new Dependency(ca, s, null, "ca->s", 1), // etc.
                new Dependency(cb, s, null, "cb->s", 1), new Dependency(cb, t, null, "cb->t", 1),
                // vanishes, because s is not mapped
                new Dependency(a, t, null, "a->t", 1), new Dependency(a, t, null, "a->t", 1),
                new Dependency(a, s, null, "a->s", 1),
            }, "", "test", result);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("A", result[0].UsingItem.Values[0]);
            Assert.AreEqual("A", result[0].UsedItem.Values[0]);
            // Assert internal operations ...!!!
        }

        [TestMethod]
        public void TestSmallSelfOptimizingFirstLetterProjector() {
            SmallTestForPrefixOptimizedProjector((p, i) => new ProjectItems.SelfOptimizingFirstLetterProjector(p, i, 2));
        }

        [TestMethod]
        public void TestSmallSelfOptimizingPrefixTrieProjector() {
            SmallTestForPrefixOptimizedProjector((p, i) => new ProjectItems.SelfOptimizingPrefixTrieProjector(p, i, 2));
        }

    }
}
