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

        private class TestTraverser : AbstractRegexDepthFirstPathTraverser<Dependency, Item, Ignore, Ignore, Ignore> {
            private readonly List<string> _recordedDependencies = new List<string>();
            private readonly List<object> _countedObjects = new List<object>();
            private readonly Dictionary<Item, Dependency[]> _outgoing;

            public IEnumerable<string> RecordedDependencies => _recordedDependencies;
            public IEnumerable<object> CountedObjects => _countedObjects;

            public TestTraverser(IEnumerable<Dependency> deps, PathRegex regex) : base(regex, checkAbort: () => { }) {
                _outgoing = Item.CollectOutgoingDependenciesMap(deps);
            }

            public void Traverse(Item root) {
                base.Traverse(root, _outgoing, Ignore.Om);
            }

            protected override bool ShouldVisitSuccessors(Item tail, Stack<Dependency> currentPath, out Ignore initUpSum) {
                initUpSum = Ignore.Om;
                return true;
            }

            protected override DownAndHere AfterPushDependency(Stack<Dependency> currentPath, bool isEnd,
                    Ignore down, object countedObject) {
                if (isEnd) {
                    _recordedDependencies.Add(string.Join("/", currentPath.Reverse()));
                    if (countedObject != null) {
                        _countedObjects.Add(countedObject);
                    }
                }
                return new DownAndHere(Ignore.Om, Ignore.Om);
            }

            protected override Ignore BeforePopDependency(Stack<Dependency> currentPath, bool isEnd,
                Ignore here, Ignore upSum, Ignore childUp, object countedObject) {
                return Ignore.Om;
            }

            protected override Ignore AfterVisitingSuccessors(bool visitSuccessors, Item tail, Stack<Dependency> currentPath,
                Ignore upSum) {
                return Ignore.Om;
            }
        }

        [TestMethod]
        public void TestCreateSimpleRegex() {
            WorkingGraph graph = CreateSmallTestgraph();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.b(.d)?"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(2, traverser.RecordedDependencies.Count());

            AssertRecordedDependenciesContainPath(traverser.RecordedDependencies, "a", "b");
            AssertRecordedDependenciesContainPath(traverser.RecordedDependencies, "a", "b", "d");
        }

        // ReSharper disable once UnusedParameter.Local -- this is an Assert, so it's ok that recordedDependencies is only used in Assert
        private void AssertRecordedDependenciesContainPath(IEnumerable<string> recordedDependencies, params string[] items) {
            var regex = "^[^/]*";
            var sep = "";
            for (int i = 0; i < items.Length - 1; i++) {
                regex +=sep + items[i] + ".*--.*->.*" + items[i + 1];
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
        public void TestCreateSimpleCountingRegex() {
            WorkingGraph graph = CreateSmallTestgraph();

            var traverser = new TestTraverser(graph.VisibleDependencies, new PathRegex("a.b$(.d)?"));

            traverser.Traverse(graph.VisibleDependencies.First().UsingItem);

            Assert.AreEqual(1, traverser.CountedObjects.Distinct().Count());
        }

        // Test: b-loop in Item-Graph matched by a.b.b.b.c is actually looped 3 times
        // Better test on a->b-->b->b->b->b
        //                  / \\_
        //                  +-+  \->c           ... must loop, but also run along b list and not find an end there!

        // Test: That exactly same position in regex AND in graph can be detected and stop search!
    }
}
