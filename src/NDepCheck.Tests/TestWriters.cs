using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using NDepCheck.Rendering.TextWriting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestWriters {
        [TestMethod]
        public void TestItemWriter() {
            using (var s = new MemoryStream()) {
                var w = new ItemWriter();
                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
                w.RenderToStreamForUnitTests(dependencies, s);

                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"$ AMO(Assembly:Module:Order)
    --3->*--0->     AMO:BAC:BAC:0100'area
    --2->*--0->     AMO:Kah.MI:KAH:0301'area+mi
    --1->*--1->     AMO:KAH:KAH:0300'area
    --1->*--2->     AMO:KST:KST:0200'area
    --0->*--4->     AMO:VKF:VKF:0400'area
", result);
            }
        }

        [TestMethod]
        public void WriteAndReadDotNetDependencies() {
            using (DisposingFile dipFile = DisposingFile.TempFileWithTail(".dip")) {
                int result =
                    Program.Main(new[] {
                        MainTests.TestAssemblyPath, Program.WriteDipOption.Opt, dipFile.Filename,
                        Program.DoResetOption.Opt, dipFile.Filename, Program.CountDependenciesOption.Opt
                    });
                Assert.AreEqual(Program.OK_RESULT, result);

                Console.WriteLine(dipFile.Filename);
            }
        }

        [TestMethod]
        private IEnumerable<Dependency> CreateSomeTestDependencies() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var b = Item.New(t3, "b:bb:bbb".Split(':'));
            var c = Item.New(t3, "c:cc:ccc".Split(':'));
            var d = Item.New(t3, "d:dd:ddd".Split(':'));
            var e = Item.New(t3, "e:ee:eee".Split(':'));
            var f = Item.New(t3, "f:ff:fff".Split(':'));
            var g = Item.New(t3, "g:gg:ggg".Split(':'));

            return new[]
                {FromTo(a, b), FromTo(b, c), FromTo(c, d), FromTo(d, e), FromTo(d, b), FromTo(e, f), FromTo(b, g),};
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionable);
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
.b $
..c", result.Trim());
            }
        }

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
..c
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
..g:gg:ggg $", result.Trim());
            }
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

            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                w.RenderToStreamForUnitTests(dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
b:bb:bbb $
d:dd:ddd $", result.Trim());
            }
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForOnePath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var b = Item.New(t3, "b:bb:bbb".Split(':'));

            var dependencies = new[] {
                FromTo(a, b),
            };

            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                w.RenderToStreamForUnitTests(dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
b:bb:bbb $", result.Trim());
            }
        }

        [TestMethod]
        public void TestSimpleFlatPathWriterForNoPath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var c = Item.New(t3, "c:cc:ccc".Split(':'));

            var dependencies = new[] {
                FromTo(a, c),
            };

            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                w.RenderToStreamForUnitTests(dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual("", result.Trim());
            }
        }

        [TestMethod]
        public void TestSimpleFlatPathWriter() {
            using (var s = new MemoryStream()) {
                var w = new PathWriter();
                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
                w.RenderToStreamForUnitTests(dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
b:bb:bbb $
c:cc:ccc
d:dd:ddd $
e:ee:eee $
f:ff:fff $

a:aa:aaa
b:bb:bbb $
c:cc:ccc
d:dd:ddd $
<= b:bb:bbb $

a:aa:aaa
b:bb:bbb $
g:gg:ggg $", result.Trim());
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
|.\-c
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
\-b:bb:bbb $
..+-c:cc:ccc
..|.\-d:dd:ddd $
..|...+-e:ee:eee $
..|...|.\-f:ff:fff $
..|...\-<= b:bb:bbb $
..|.
..\-g:gg:ggg $", result.Trim());
            }
        }
    }
}