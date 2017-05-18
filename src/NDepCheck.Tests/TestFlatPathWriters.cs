using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering.PathFinding;
using NDepCheck.Rendering.TextWriting;
using NDepCheck.Transforming.PathFinding;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestFlatPathWriters : AbstractWriterTest {
        [TestInitialize]
        public void TestInitialize() {
            new GlobalContext().ResetAll();
        }

        private Dependency CreateDependency(Item from, Item to, string pathMarker, bool isStart, bool isEnd, bool isMatchedByCountMatch, bool isLoopBack) {
            Dependency d = FromTo(from, to);
            d.MarkPathElement(pathMarker, 0, isStart: isStart, isEnd: isEnd, isMatchedByCountMatch: isMatchedByCountMatch,
                isLoopBack: isLoopBack);
            return d;
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForOnePath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");
            var pathMarker = "P0";

            Item a = Item.New(t3, "a:aa:aaa".Split(':'));
            a.MarkPathElement(pathMarker, 0, isStart: false, isEnd: false, isMatchedByCountMatch: false, isLoopBack: false);
            Item b = Item.New(t3, "b:bb:bbb".Split(':'));
            b.SetMarker(pathMarker, 1);

            var d = CreateDependency(a, b, pathMarker, true, true, false, false);
            Dependency[] dependencies = { d };

            using (var s = new MemoryStream()) {
                var w = new FlatPathWriter();
                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "P*");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"-- P0
a:aa:aaa
b:bb:bbb $", result.Trim());
            }
        }

        [TestMethod]
        public void TestSimplePathSearchAndFlatPathWriterForOnePath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var b = Item.New(t3, "b:bb:bbb".Split(':'));

            var dependencies = new[] {
                FromTo(a, b),
            };

            string result = FindPathsAndWriteFlat(dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual(@"-- A0
T3:a:aa:aaa'A0
T3:b:bb:bbb'A $", result.Trim());
        }

        private static string FindPathsAndWriteFlat(IEnumerable<Dependency> dependencies, string markerPrefix, string transformOptions) {
            var gc = new GlobalContext { IgnoreCase = true };
            var pm = new PathMarker();
            pm.Configure(gc, "", false);
            var transformedDependencies = new List<Dependency>();
            pm.Transform(gc, dependencies, transformOptions, transformedDependencies);

            string result;
            using (var s = new MemoryStream()) {
                var w = new FlatPathWriter();
                w.RenderToStreamForUnitTests(new GlobalContext(), transformedDependencies, s, $"{markerPrefix}* -sm");
                result = Encoding.ASCII.GetString(s.ToArray());
            }
            return result;
        }

        private static string CreateDefaultOptions(string markerPrefix, int maxPathLength = 5) {
            return ($"{{ {PathMarker.AddIndexedMarkerOption} {markerPrefix} " +
                    $"{PathMarker.MaxPathLengthOption} {maxPathLength} " +
                    $"{PathMarker.CountItemAnchorOption} a: " +
                    $"{PathMarker.MultipleItemAnchorOption} ~c: }}").Replace(" ", Environment.NewLine);
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForNoPath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var c = Item.New(t3, "c:cc:ccc".Split(':'));

            var dependencies = new[] { FromTo(a, c) };

            string result = FindPathsAndWriteFlat(dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual("", result.Trim());
        }

        [TestMethod]
        public void TestSimpleFlatPathWriter() {
            IEnumerable<Dependency> dependencies = new PathMarker().CreateSomeTestDependencies();

            string result = FindPathsAndWriteFlat(dependencies, "A", CreateDefaultOptions("A"));

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
            IEnumerable<Dependency> dependencies = new PathMarker().CreateSomeTestDependencies();

            string result = FindPathsAndWriteFlat(dependencies, "A", CreateDefaultOptions("A", 4));

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

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var b = Item.New(t3, "b:bb:bbb".Split(':'));
            var c = Item.New(t3, "c:cc:ccc".Split(':'));
            var d = Item.New(t3, "d:dd:ddd".Split(':'));

            var dependencies = new[] {
                        FromTo(a, b),
                        FromTo(b, c),
                        FromTo(b ,d),
                    };

            string result = FindPathsAndWriteFlat(dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual(@"-- A0
T3:a:aa:aaa'A0
T3:b:bb:bbb'A $
T3:d:dd:ddd'A $", result.Trim());
        }

        [TestMethod]
        public void TestOldCountPaths() {
            ItemType xy = ItemType.New("NL(Name:Layer)");
            Item a = Item.New(xy, "a", "1");
            Item b = Item.New(xy, "b", "2");
            Item c = Item.New(xy, "c", "2");
            Item d = Item.New(xy, "d", "3");
            Item e = Item.New(xy, "e", "4");
            Item f = Item.New(xy, "f", "4");
            Item g = Item.New(xy, "g", "5");
            Dependency[] dependencies = {
                        FromTo(a, b), FromTo(a, c), FromTo(b, d), FromTo(c, d), FromTo(d, e), FromTo(d, f), FromTo(e, g), FromTo(f, g)
                    };

            string o = FindPathsAndWriteFlat(dependencies, "A", "{ -im A -pi a -ci 2 -pi g }".Replace(" ", Environment.NewLine));

            Console.WriteLine(o);
            Assert.IsTrue(o.Contains("b:2 (*)"));
            Assert.IsTrue(o.Contains("c:2 (*)"));
            Assert.IsTrue(o.Contains("d:3'A=2"));
            Assert.IsTrue(o.Contains("e:4'A=2"));
            Assert.IsTrue(o.Contains("g:5'A=2 $"));
        }
    }
}