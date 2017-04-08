using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using NDepCheck.Transforming.SpecialDependencyMarking;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestMarkMinimalCutDeps {
        private static IEnumerable<Dependency> Run(string options, IEnumerable<Dependency> dependencies) {
            var globalContext = new GlobalContext();
            try {
                var mmc = new MarkMinimalCutDeps();
                var result = new List<Dependency>();
                mmc.Transform(globalContext, "test", dependencies ?? mmc.GetTestDependencies(), options, "test", result);
                return result;
            } finally {
                // Also static caches must be reset, as "Mark" modifies Items
                globalContext.ResetAll();
            }
        }

        //private static void AssertResult(IEnumerable<Item> result, params char[] nameStarts) {
        //    CollectionAssert.AreEquivalent(nameStarts, result.Select(i => i.Name[0]).ToArray());
        //}

        [TestMethod]
        public void TestMarkTrivialCut() {
            const string mark = "CUT";
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var c = Item.New(ItemType.SIMPLE, "c");
            var d = Item.New(ItemType.SIMPLE, "d");
            var dependencies = new[] {
                new Dependency(a, b, null, "D10", 10, 0, 4),
                new Dependency(b, c, null, "D20", 20, 0, 2), // critical edge
                new Dependency(c, d, null, "D30", 30, 0, 1),
                new Dependency(c, d, null, "D40", 40, 0, 3),
            };

            IEnumerable<Dependency> result = Run($"{{ {MarkMinimalCutDeps.MatchSourceOption} a " +
                                                 $"{MarkMinimalCutDeps.MatchTargetOption} d " +
                                                 $"{MarkMinimalCutDeps.MarkerToAddOption} {mark} }}", dependencies);
            Assert.IsTrue(result.All(z => z.Markers.Contains(mark) == (z.Ct == 20)), string.Join("\r\n", result.Select(z => z.AsDipStringWithTypes(false))));
        }
    }
}
