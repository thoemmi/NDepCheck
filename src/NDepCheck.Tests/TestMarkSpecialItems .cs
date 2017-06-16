using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using NDepCheck.Transforming.SpecialItemMarking;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class TestMarkSpecialItems {
        private static IEnumerable<Item> Run(MarkSpecialItems.TransformOptions options, string mark) {
            var gc = new GlobalContext();
            try {
                var msi = new MarkSpecialItems();
                var result = new List<Dependency>();
                msi.Transform(gc, Ignore.Om, options, msi.CreateSomeTestDependencies(gc.CurrentGraph), result);
                return result.SelectMany(d => new[] { d.UsingItem, d.UsedItem })
                        .Distinct()
                        .Where(i => i.MarkersContain(mark));
            } finally {
                // Also static caches must be reset, as "Mark" modifies Items
                gc.ResetAll();
            }
        }

        private static void AssertResult(IEnumerable<Item> result, params char[] nameStarts) {
            CollectionAssert.AreEquivalent(nameStarts, result.Select(i => i.Name[0]).ToArray());
        }

        [TestMethod]
        public void TestMarkSources() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run(new MarkSpecialItems.TransformOptions {
                MarkSources = true,
                MarkerToAdd = mark
            }, mark);
            AssertResult(result, 'A');
        }

        [TestMethod]
        public void TestMarkSourcesRecursively() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run(new MarkSpecialItems.TransformOptions {
                MarkSources = true,
                Recursive = true,
                MarkerToAdd = mark
            }, mark);
            AssertResult(result, 'A', 'B');
        }

        [TestMethod]
        public void TestMarkSinks() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run(new MarkSpecialItems.TransformOptions {
                MarkSinks = true,
                MarkerToAdd = mark
            }, mark);
            AssertResult(result, 'I', 'J');
        }

        [TestMethod]
        public void TestMarkSinksRecursivelyIncludingSelfCycles() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run(
                new MarkSpecialItems.TransformOptions {
                    MarkSinks = true,
                    ConsiderSelfCyclesInSourcesAndSinks = true,
                    Recursive = true,
                    MarkerToAdd = mark
                }, mark);
            AssertResult(result, 'F', 'G', 'H', 'I', 'J');
        }

        [TestMethod]
        public void TestMarkSinksRecursively() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run(new MarkSpecialItems.TransformOptions {
                MarkSinks = true,
                Recursive = true,
                MarkerToAdd = mark
            }, mark);
            AssertResult(result, 'E', 'F', 'G', 'H', 'I', 'J');
        }
    }
}
