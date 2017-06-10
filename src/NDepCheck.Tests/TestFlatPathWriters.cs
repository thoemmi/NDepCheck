using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering.TextWriting;
using NDepCheck.Transforming.PathFinding;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class TestFlatPathWriters : AbstractWriterTest {
        [TestInitialize]
        public void TestInitialize() {
            new GlobalContext().ResetAll();
        }

        private Dependency CreateDependency(WorkingGraph graph, Item from, Item to, string pathMarker, bool isStart, bool isEnd, bool isMatchedByCountMatch, bool isLoopBack) {
            Dependency d = FromTo(graph, from, to);
            d.MarkPathElement(pathMarker, 0, isStart: isStart, isEnd: isEnd, isMatchedByCountMatch: isMatchedByCountMatch,
                isLoopBack: isLoopBack);
            return d;
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForOnePath() {
            var gc = new GlobalContext { IgnoreCase = true };
            WorkingGraph graph = gc.CurrentGraph;

            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");
            var pathMarker = "P0";

            Item a = graph.CreateItem(t3, "a:aa:aaa".Split(':'));
            a.MarkPathElement(pathMarker, 0, isStart: false, isEnd: false, isMatchedByCountMatch: false, isLoopBack: false);
            Item b = graph.CreateItem(t3, "b:bb:bbb".Split(':'));
            b.SetMarker(pathMarker, 1);

            var d = CreateDependency(graph, a, b, pathMarker, true, true, false, false);
            Dependency[] dependencies = { d };

            using (var s = new MemoryStream()) {
                var w = new FlatPathWriter();
                w.RenderToStreamForUnitTests(gc, dependencies, s, "P*");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"-- P0
a:aa:aaa
b:bb:bbb $", result.Trim());
            }
        }

        [TestMethod]
        public void TestSimplePathSearchAndFlatPathWriterForOnePath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            var a = graph.CreateItem(t3, "a:aa:aaa".Split(':'));
            var b = graph.CreateItem(t3, "b:bb:bbb".Split(':'));

            var dependencies = new[] {
                FromTo(graph, a, b),
            };

            string result = FindPathsAndWriteFlat(gc, dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual(@"-- A0
T3:a:aa:aaa'A0
T3:b:bb:bbb'A $", result.Trim());
        }

        private static string FindPathsAndWriteFlat(GlobalContext gc, IEnumerable<Dependency> dependencies, string markerPrefix, string transformOptions) {
            var pm = new PathMarker();
            pm.Configure(gc, "", false);
            var transformedDependencies = new List<Dependency>();
            pm.Transform(gc, dependencies, transformOptions, transformedDependencies, s => null);

            string result;
            using (var s = new MemoryStream()) {
                var w = new FlatPathWriter();
                w.RenderToStreamForUnitTests(gc, transformedDependencies, s, $"{markerPrefix}* -sm");
                result = Encoding.ASCII.GetString(s.ToArray());
            }
            return result;
        }

        private static string CreateDefaultOptions(string markerPrefix, int maxPathLength = 5) {
            return ($"{{ {PathMarker.AddIndexedMarkerOption} {markerPrefix} " +
                    $"{PathMarker.PathOptions.MaxPathLengthOption} {maxPathLength} " +
                    $"{PathMarker.PathOptions.CountItemAnchorOption} a: " +
                    $"{PathMarker.PathOptions.MultipleItemAnchorOption} ~c: }}").Replace(" ", Environment.NewLine);
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForNoPath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            var a = graph.CreateItem(t3, "a:aa:aaa".Split(':'));
            var c = graph.CreateItem(t3, "c:cc:ccc".Split(':'));

            var dependencies = new[] { FromTo(graph, a, c) };

            string result = FindPathsAndWriteFlat(gc, dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual("", result.Trim());
        }

        [TestMethod]
        public void TestSimpleFlatPathWriter() {
            var gc = new GlobalContext();

            IEnumerable<Dependency> dependencies = new PathMarker().CreateSomeTestDependencies(gc.CurrentGraph);

            string result = FindPathsAndWriteFlat(gc, dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual(@"-- A0
T3:a:aa:aaa'A0+A1+A2+A3
T3:b:bb:bbb'A $
T3:c:cc:ccc'A
T3:d:dd:ddd'A $
T3:e:ee:eee'A $
T3:f:ff:fff'A $

-- A1
T3:a:aa:aaa'A0+A1+A2+A3
T3:b:bb:bbb'A $
T3:c:cc:ccc'A
T3:d:dd:ddd'A $
<= T3:b:bb:bbb'A $

-- A2
T3:a:aa:aaa'A0+A1+A2+A3
T3:b:bb:bbb'A $
T3:g:gg:ggg'A $

-- A3
T3:a:aa:aaa'A0+A1+A2+A3
T3:h:hh:hhh'A $
T3:g:gg:ggg'A $", result.Trim());
        }


        [TestMethod]
        public void TestLimitedFlatPathWriter() {
            var gc = new GlobalContext();

            IEnumerable<Dependency> dependencies = new PathMarker().CreateSomeTestDependencies(gc.CurrentGraph);

            string result = FindPathsAndWriteFlat(gc, dependencies, "A", CreateDefaultOptions("A", 4));

            Assert.AreEqual(@"-- A0
T3:a:aa:aaa'A0+A1+A2+A3
T3:b:bb:bbb'A $
T3:c:cc:ccc'A
T3:d:dd:ddd'A $
T3:e:ee:eee'A $

-- A1
T3:a:aa:aaa'A0+A1+A2+A3
T3:b:bb:bbb'A $
T3:c:cc:ccc'A
T3:d:dd:ddd'A $
<= T3:b:bb:bbb'A $

-- A2
T3:a:aa:aaa'A0+A1+A2+A3
T3:b:bb:bbb'A $
T3:g:gg:ggg'A $

-- A3
T3:a:aa:aaa'A0+A1+A2+A3
T3:h:hh:hhh'A $
T3:g:gg:ggg'A $", result.Trim());
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForYPath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            var a = graph.CreateItem(t3, "a:aa:aaa".Split(':'));
            var b = graph.CreateItem(t3, "b:bb:bbb".Split(':'));
            var c = graph.CreateItem(t3, "c:cc:ccc".Split(':'));
            var d = graph.CreateItem(t3, "d:dd:ddd".Split(':'));

            var dependencies = new[] {
                        FromTo(graph, a, b),
                        FromTo(graph, b, c),
                        FromTo(graph, b ,d),
                    };

            string result = FindPathsAndWriteFlat(gc, dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual(@"-- A0
T3:a:aa:aaa'A0
T3:b:bb:bbb'A $
T3:d:dd:ddd'A $", result.Trim());
        }

        [TestMethod]
        public void TestOldCountPaths() {
            ItemType xy = ItemType.New("NL(Name:Layer)");
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            Item a = graph.CreateItem(xy, "a", "1");
            Item b = graph.CreateItem(xy, "b", "2");
            Item c = graph.CreateItem(xy, "c", "2");
            Item d = graph.CreateItem(xy, "d", "3");
            Item e = graph.CreateItem(xy, "e", "4");
            Item f = graph.CreateItem(xy, "f", "4");
            Item g = graph.CreateItem(xy, "g", "5");
            Dependency[] dependencies = {
                FromTo(graph, a, b),
                FromTo(graph, a, c),
                FromTo(graph, b, d),
                FromTo(graph, c, d),
                FromTo(graph, d, e),
                FromTo(graph, d, f),
                FromTo(graph, e, g),
                FromTo(graph, f, g)
            };

            string o = FindPathsAndWriteFlat(gc, dependencies, "A", "{ -im A -pi a -ci 2 -pi g }".Replace(" ", Environment.NewLine));

            Console.WriteLine(o);
            Assert.IsTrue(o.Contains("b:2 (*)"));
            Assert.IsTrue(o.Contains("c:2 (*)"));
            Assert.IsTrue(o.Contains("d:3'A=2"));
            Assert.IsTrue(o.Contains("e:4'A=2"));
            Assert.IsTrue(o.Contains("g:5'A=2 $"));
        }
    }
}