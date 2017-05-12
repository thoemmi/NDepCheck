using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering.TextWriting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestTreeWriters : AbstractWriterTest {
        [TestMethod]
        public void TestTrivialTreeTwoPathWriter() {
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");
            Item d = Item.New(ItemType.SIMPLE, "d");

            Dependency[] dependencies = {
                new Dependency(a, b, null, "", 1),
                new Dependency(b, c, null, "", 1),
                new Dependency(a, d, null, "", 1),
            };

            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                w.RenderToStreamForUnitTests(dependencies, s, "SI");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a
.b $
.d $", result.Trim());
            }
        }

        [TestMethod]
        public void TestTrivialLoopPathWriter() {
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");

            Dependency[] dependencies = {
                new Dependency(a, b, null, "", 1),
                new Dependency(b, c, null, "", 1),
                new Dependency(c, b, null, "", 1),
            };

            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                w.RenderToStreamForUnitTests(dependencies, s, "SI");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a
.b $
..c
...<= b $", result.Trim());
            }
        }

        [TestMethod]
        public void TestTrivialTreePathWriter() {
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");

            Dependency[] dependencies = {
                new Dependency(a, b, null, "", 1),
                new Dependency(b, c, null, "", 1),
            };

            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                w.RenderToStreamForUnitTests(dependencies, s, "SI");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a
.b $", result.Trim());
            }
        }

        [TestMethod]
        public void TestSimpleLinePathWriter() {
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");
            Item d = Item.New(ItemType.SIMPLE, "d");

            Dependency[] dependencies = {
                new Dependency(a, b, null, "", 1),
                new Dependency(b, c, null, "", 1),
                new Dependency(a, d, null, "", 1),
            };

            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                w.RenderToStreamForUnitTests(dependencies, s, "LI");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a
+-b $
\-d $", result.Trim());
            }
        }

        [TestMethod]
        public void TestTreeLineWriter() {
            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
                w.RenderToStreamForUnitTests(dependencies, s, "LI");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
+-b:bb:bbb $
|.+-c:cc:ccc
|.|.\-d:dd:ddd $
|.|...+-e:ee:eee $
|.|...|.\-f:ff:fff $
|.|...\-<= b:bb:bbb $
|.|.
|.\-g:gg:ggg $
\-h:hh:hhh $
..\-g:gg:ggg $", result.Trim());
            }
        }

        //[TestMethod]
        //public void TestTreeLineWriterX() {
        //    ItemType xy = ItemType.New("XY(X:Y)");
        //    Item a = Item.New(xy, "a:1");
        //    Item b = Item.New(xy, "b:2");
        //    Item c = Item.New(xy, "c:2");
        //    Item d = Item.New(xy, "d:3");
        //    Item e = Item.New(xy, "e:4");
        //    Dependency[] dependencies = {
        //        FromTo(a, b), FromTo(a, c), FromTo(b, d), FromTo(c, d), FromTo(d, e)
        //    };

        //    using (var f = DisposingFile.CreateTempFileWithTail(".txt")) {
        //        var w = new PathWriter();
        //        w.Render(new GlobalContext(), dependencies, null,
        //            "{ -pi a -ci 2 -pi e }", f.Filename, false);

        //        using (var sw = new StreamReader(f.Filename)) {
        //            string o = sw.ReadToEnd();

        //            Assert.IsTrue(o.Contains("SIMPLE:A'LeftMatch+RightMatch "));
        //        }
        //    }
        //}

        [TestMethod]
        public void TestSimpleTreePathWriter() {
            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
                w.RenderToStreamForUnitTests(dependencies, s, "SI");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
.b:bb:bbb $
..c:cc:ccc
...d:dd:ddd $
....e:ee:eee $
.....f:ff:fff $
....<= b:bb:bbb $
..
..g:gg:ggg $
.h:hh:hhh $
..g:gg:ggg $", result.Trim());
            }
        }
    }
}