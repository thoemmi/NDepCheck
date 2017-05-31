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

        private Dependency CreateDependency(Environment env, Item from, Item to, string pathMarker, bool isStart, bool isEnd, bool isMatchedByCountMatch, bool isLoopBack) {
            Dependency d = FromTo(env, from, to);
            d.MarkPathElement(pathMarker, 0, isStart: isStart, isEnd: isEnd, isMatchedByCountMatch: isMatchedByCountMatch,
                isLoopBack: isLoopBack);
            return d;
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForOnePath() {
            var gc = new GlobalContext { IgnoreCase = true };
            Environment env = gc.CurrentEnvironment;

            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");
            var pathMarker = "P0";

            Item a = env.NewItem(t3, "a:aa:aaa".Split(':'));
            a.MarkPathElement(pathMarker, 0, isStart: false, isEnd: false, isMatchedByCountMatch: false, isLoopBack: false);
            Item b = env.NewItem(t3, "b:bb:bbb".Split(':'));
            b.SetMarker(pathMarker, 1);

            var d = CreateDependency(env, a, b, pathMarker, true, true, false, false);
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
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(t3, "a:aa:aaa".Split(':'));
            var b = env.NewItem(t3, "b:bb:bbb".Split(':'));

            var dependencies = new[] {
                FromTo(env, a, b),
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
            pm.Transform(gc, dependencies, transformOptions, transformedDependencies);

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
                    $"{PathMarker.MaxPathLengthOption} {maxPathLength} " +
                    $"{PathMarker.CountItemAnchorOption} a: " +
                    $"{PathMarker.MultipleItemAnchorOption} ~c: }}").Replace(" ", System.Environment.NewLine);
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForNoPath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(t3, "a:aa:aaa".Split(':'));
            var c = env.NewItem(t3, "c:cc:ccc".Split(':'));

            var dependencies = new[] { FromTo(env, a, c) };

            string result = FindPathsAndWriteFlat(gc, dependencies, "A", CreateDefaultOptions("A"));

            Assert.AreEqual("", result.Trim());
        }

        [TestMethod]
        public void TestSimpleFlatPathWriter() {
            var gc = new GlobalContext();

            IEnumerable<Dependency> dependencies = new PathMarker().CreateSomeTestDependencies(gc.CurrentEnvironment);

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

            IEnumerable<Dependency> dependencies = new PathMarker().CreateSomeTestDependencies(gc.CurrentEnvironment);

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
            Environment env = gc.CurrentEnvironment;

            var a = env.NewItem(t3, "a:aa:aaa".Split(':'));
            var b = env.NewItem(t3, "b:bb:bbb".Split(':'));
            var c = env.NewItem(t3, "c:cc:ccc".Split(':'));
            var d = env.NewItem(t3, "d:dd:ddd".Split(':'));

            var dependencies = new[] {
                        FromTo(env, a, b),
                        FromTo(env, b, c),
                        FromTo(env, b ,d),
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
            Environment env = gc.CurrentEnvironment;

            Item a = env.NewItem(xy, "a", "1");
            Item b = env.NewItem(xy, "b", "2");
            Item c = env.NewItem(xy, "c", "2");
            Item d = env.NewItem(xy, "d", "3");
            Item e = env.NewItem(xy, "e", "4");
            Item f = env.NewItem(xy, "f", "4");
            Item g = env.NewItem(xy, "g", "5");
            Dependency[] dependencies = {
                FromTo(env, a, b),
                FromTo(env, a, c),
                FromTo(env, b, d),
                FromTo(env, c, d),
                FromTo(env, d, e),
                FromTo(env, d, f),
                FromTo(env, e, g),
                FromTo(env, f, g)
            };

            string o = FindPathsAndWriteFlat(gc, dependencies, "A", "{ -im A -pi a -ci 2 -pi g }".Replace(" ", System.Environment.NewLine));

            Console.WriteLine(o);
            Assert.IsTrue(o.Contains("b:2 (*)"));
            Assert.IsTrue(o.Contains("c:2 (*)"));
            Assert.IsTrue(o.Contains("d:3'A=2"));
            Assert.IsTrue(o.Contains("e:4'A=2"));
            Assert.IsTrue(o.Contains("g:5'A=2 $"));
        }
    }
}