using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Matching;
using NDepCheck.PathMatching;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class TestPathRegexTraverser {
        private class PathRegex : PathRegex<Item, Dependency> {
            public PathRegex([NotNull] string definition)
                : base(definition, new Dictionary<string, ItemMatch>(), new Dictionary<string, DependencyMatch>(), ignoreCase: false) {
            }
        }

        private class TestTraverser : AbstractRegexDepthFirstPathTraverser<Dependency, Item, Ignore, Ignore, Ignore, PathStateElement<Dependency>> {
            private readonly List<string> _recordedPaths = new List<string>();
            private readonly List<object> _countedObjects = new List<object>();
            private readonly Dictionary<Item, Dependency[]> _outgoing;

            public IEnumerable<string> RecordedPaths => _recordedPaths;
            public IEnumerable<object> CountedObjects => _countedObjects;

            public TestTraverser(IEnumerable<Dependency> deps, PathRegex regex) : base(regex, checkAbort: () => { }) {
                _outgoing = Item.CollectOutgoingDependenciesMap(deps);
            }

            public void Traverse(Item root) {
                Traverse(root, _outgoing, (r, isCounted) => Ignore.Om);
            }

            protected override bool ShouldVisitSuccessors(Item tail, Stack<Dependency> currentPath, out Ignore initUpSum) {
                initUpSum = Ignore.Om;
                return true;
            }

            protected override DownAndHere AfterPushDependency(Stack<Dependency> currentPath, bool isEnd,
                bool isLoopBack, CountedEnum counted, Ignore down) {
                if (isEnd) {
                    _recordedPaths.Add(string.Join("/", currentPath.Reverse()));
                    switch (counted) {
                        case CountedEnum.DependencyCounted:
                            _countedObjects.Add(currentPath.Peek());
                            break;
                        case CountedEnum.UsedItemCounted:
                            _countedObjects.Add(currentPath.Peek().UsedItem);
                            break;
                    }
                }
                return new DownAndHere(Ignore.Om, Ignore.Om);
            }

            protected override Ignore BeforePopDependency(Stack<Dependency> currentPath, bool isEnd, bool isLoopBack, CountedEnum counted, Ignore down, Ignore here,
                Ignore upSum, Ignore childUp) {
                return Ignore.Om;
            }

            protected override Ignore AfterVisitingSuccessors(bool visitSuccessors, Item tail, Stack<Dependency> currentPath,
                Ignore upSum) {
                return Ignore.Om;
            }

            protected override PathStateElement<Dependency> CreateFirstStateElement(IBeforeDependencyGraphkenState<Item, Dependency, ItemMatch, DependencyMatch> beforeNextDependencyState, Item nextDep) {
                return null;
            }

            protected override PathStateElement<Dependency> CreateStateElement(IBeforeDependencyGraphkenState<Item, Dependency, ItemMatch, DependencyMatch> beforeNextDependencyState, Dependency nextDep) {
                return new PathStateElement<Dependency>(beforeNextDependencyState, nextDep);
            }
        }

        [TestMethod]
        public void TestCreateSimpleRegex() {
            WorkingGraph graph = CreateSmallTestgraph();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.b(.d)?"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(2, traverser.RecordedPaths.Count());

            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a", "b");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a", "b", "d");
        }

        // ReSharper disable once UnusedParameter.Local -- this is an Assert, so it's ok that recordedDependencies is only used in Assert
        private void AssertRecordedDependenciesContainPath(IEnumerable<string> recordedDependencies, params string[] items) {
            var regex = "^[^/]*";
            var sep = "";
            for (int i = 0; i < items.Length - 1; i++) {
                regex += sep + items[i] + ".*--.*->.*" + items[i + 1];
                sep = ".*/.*";
            }
            regex += "[^/]*$";
            Assert.IsTrue(recordedDependencies.Any(s => Regex.IsMatch(s, regex)), "Missing path " + string.Join("--->", items) + " [regex=" + regex + "]");
        }

        private static WorkingGraph CreateSmallTestgraph() {
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            Item a = graph.CreateItem(ItemType.SIMPLE, "a");
            Item b = graph.CreateItem(ItemType.SIMPLE, "b");
            Item c = graph.CreateItem(ItemType.SIMPLE, "c");
            Item d = graph.CreateItem(ItemType.SIMPLE, "d");

            graph.AddDependencies(new[] {
                graph.CreateDependency(a, b, null, "", 1),
                graph.CreateDependency(b, c, null, "", 1),
                graph.CreateDependency(b, d, null, "", 1)
            });

            return graph;
        }

        [TestMethod]
        public void TestCreateSimpleItemCountingRegex() {
            WorkingGraph graph = CreateSmallTestgraph();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.b#(.d)?"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(1, traverser.CountedObjects.Distinct().Count());
        }

        [TestMethod]
        public void TestCreateSimpleDependencyCountingRegex() {
            WorkingGraph graph = CreateSmallTestgraph();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.b(.#d)?"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(1, traverser.CountedObjects.Distinct().Count());
        }

        [TestMethod]
        public void TestLoopIsNotEndless() {
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            Item a = graph.CreateItem(ItemType.SIMPLE, "a");
            Item b = graph.CreateItem(ItemType.SIMPLE, "b");
            Item c = graph.CreateItem(ItemType.SIMPLE, "c");
            Item d = graph.CreateItem(ItemType.SIMPLE, "d");

            graph.AddDependencies(new[] {
                graph.CreateDependency(a, b, null, "", 1),
                // Loop on b & c; but we never reach d!
                graph.CreateDependency(b, c, null, "", 1),
                graph.CreateDependency(c, b, null, "", 1),

                // Loop on d so that it remains in graph
                graph.CreateDependency(d, d, null, "", 1)
            });

            // We want to go from a to d - this will never succeed; but we must make sure that
            // there is no endless loop around b and c.
            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.(b.c.)*d"));

            traverser.Traverse(a);

            Assert.AreEqual(0, traverser.RecordedPaths.Count());
        }


        private static WorkingGraph CreateGraphWithLongTail() {
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;
            ItemType g2 = ItemType.Generic(2, ignoreCase: true);
            Item a = graph.CreateItem(g2, "a:");
            Item b = graph.CreateItem(g2, "b:0");
            Item c = graph.CreateItem(g2, "c:");
            Item b1 = graph.CreateItem(g2, "b:1");
            Item b2 = graph.CreateItem(g2, "b:2");
            Item b3 = graph.CreateItem(g2, "b:3");
            Item b4 = graph.CreateItem(g2, "b:4");
            Item b5 = graph.CreateItem(g2, "b:5");

            graph.AddDependencies(new[] {
                graph.CreateDependency(a, b, null, "", 1), graph.CreateDependency(b, b, null, "", 1),
                graph.CreateDependency(b, c, null, "", 1), graph.CreateDependency(b, b1, null, "", 1),
                graph.CreateDependency(b1, b2, null, "", 1), graph.CreateDependency(b2, b3, null, "", 1),
                graph.CreateDependency(b3, b4, null, "", 1), graph.CreateDependency(b4, b5, null, "", 1),
            });
            return graph;
        }

        [TestMethod]
        public void TestComplexLoopIsFound() {
            // b-loop in Item-Graph matched by a.b.b.b.c is actually looped 3 times
            // Better test on a->b-->b->b->b->b...
            //                  / \\_
            //                  +-+  \->c           ... must loop, but also run along b list and not find an end there!
            WorkingGraph graph = CreateGraphWithLongTail();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.b.b.b.c"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(1, traverser.RecordedPaths.Count());
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a", "b:0", "b:0", "b:0", "c");
        }

        [TestMethod]
        public void TestVariantsOverLoopAreFound() {
            // b-loop in Item-Graph matched by a.b.b.b.c is actually looped 3 times
            // Better test on a->b-->b->b->b->b...
            //                  / \\_
            //                  +-+  \->c           ... must loop, but also run along b list and not find an end there!
            WorkingGraph graph = CreateGraphWithLongTail();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.b.b.b"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(3, traverser.RecordedPaths.Count());
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a", "b:0", "b:0", "b:0");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a", "b:0", "b:0", "b:1");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a", "b:0", "b:1", "b:2");
        }

        [TestMethod]
        public void TestFindAllGraphs() {
            // All graphs in a->b-->b1->b2->b3->b4->b5
            //                 / \\_
            //                 +-+  \->c
            // are:

            WorkingGraph graph = CreateGraphWithLongTail();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex(":(.:)*"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(15, traverser.RecordedPaths.Count());
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0", "b:0");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0", "c:");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0", "b:1");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0", "b:1", "b:2");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0", "b:1", "b:2", "b:3");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0", "b:1", "b:2", "b:3", "b:4");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:0", "b:1", "b:2", "b:3", "b:4", "b:5");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "c:");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:1");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:1", "b:2");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:1", "b:2", "b:3");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:1", "b:2", "b:3", "b:4");
            AssertRecordedDependenciesContainPath(traverser.RecordedPaths, "a:", "b:0", "b:1", "b:2", "b:3", "b:4", "b:5");
        }
    }
}
