//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using NDepCheck.Rendering.TextWriting;

//namespace NDepCheck.Tests {
//    [TestClass]
//    public class TestTreeWriters : AbstractWriterTest {
//        [TestMethod]
//        public void TestTrivialTreeTwoPathWriter() {
//            Item a = Item.New(ItemType.SIMPLE, "a");
//            Item b = Item.New(ItemType.SIMPLE, "b");
//            Item c = Item.New(ItemType.SIMPLE, "c");
//            Item d = Item.New(ItemType.SIMPLE, "d");

//            Dependency[] dependencies = {
//                new Dependency(a, b, null, "", 1),
//                new Dependency(b, c, null, "", 1),
//                new Dependency(a, d, null, "", 1),
//            };

//            using (var s = new MemoryStream()) {
//                var w = new OldPathWriter();
//                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "SI");
//                string result = Encoding.ASCII.GetString(s.ToArray());
//                Assert.AreEqual(@"a
//.b $ (1)
//.d $ (1)", result.Trim());
//            }
//        }

//        [TestMethod]
//        public void TestTrivialLoopPathWriter() {
//            Item a = Item.New(ItemType.SIMPLE, "a");
//            Item b = Item.New(ItemType.SIMPLE, "b");
//            Item c = Item.New(ItemType.SIMPLE, "c");

//            Dependency[] dependencies = {
//                new Dependency(a, b, null, "", 1),
//                new Dependency(b, c, null, "", 1),
//                new Dependency(c, b, null, "", 1),
//            };

//            using (var s = new MemoryStream()) {
//                var w = new OldPathWriter();
//                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "SI");
//                string result = Encoding.ASCII.GetString(s.ToArray());
//                Assert.AreEqual(@"a
//.b $ (1)
//..c (1)
//...<= b $ (1)", result.Trim());
//            }
//        }

//        [TestMethod]
//        public void TestTrivialTreePathWriter() {
//            Item a = Item.New(ItemType.SIMPLE, "a");
//            Item b = Item.New(ItemType.SIMPLE, "b");
//            Item c = Item.New(ItemType.SIMPLE, "c");

//            Dependency[] dependencies = {
//                new Dependency(a, b, null, "", 1),
//                new Dependency(b, c, null, "", 1),
//            };

//            using (var s = new MemoryStream()) {
//                var w = new OldPathWriter();
//                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "SI");
//                string result = Encoding.ASCII.GetString(s.ToArray());
//                Assert.AreEqual(@"a
//.b $ (1)", result.Trim());
//            }
//        }

//        [TestMethod]
//        public void TestSimpleLinePathWriter() {
//            Item a = Item.New(ItemType.SIMPLE, "a");
//            Item b = Item.New(ItemType.SIMPLE, "b");
//            Item c = Item.New(ItemType.SIMPLE, "c");
//            Item d = Item.New(ItemType.SIMPLE, "d");

//            Dependency[] dependencies = {
//                new Dependency(a, b, null, "", 1),
//                new Dependency(b, c, null, "", 1),
//                new Dependency(a, d, null, "", 1),
//            };

//            using (var s = new MemoryStream()) {
//                var w = new OldPathWriter();
//                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "LI");
//                string result = Encoding.ASCII.GetString(s.ToArray());
//                Assert.AreEqual(@"a
//+-b $ (1)
//\-d $ (1)", result.Trim());
//            }
//        }

//        [TestMethod]
//        public void TestTreeLineWriter() {
//            using (var s = new MemoryStream()) {
//                var w = new OldPathWriter();
//                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
//                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "LI");
//                string result = Encoding.ASCII.GetString(s.ToArray());
//                Assert.AreEqual(@"a:aa:aaa
//+-b:bb:bbb $ (1)
//|.+-c:cc:ccc (1)
//|.|.\-d:dd:ddd $ (1)
//|.|...+-e:ee:eee $ (1)
//|.|...|.\-f:ff:fff $ (1)
//|.|...\-<= b:bb:bbb $ (1)
//|.|.
//|.\-g:gg:ggg $ (1)
//\-h:hh:hhh $ (1)
//..\-g:gg:ggg $ (1)", result.Trim());
//            }
//        }

//        [TestMethod]
//        public void TestSimpleTreePathWriter() {
//            using (var s = new MemoryStream()) {
//                var w = new OldPathWriter();
//                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
//                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "SI");
//                string result = Encoding.ASCII.GetString(s.ToArray());
//                Assert.AreEqual(@"a:aa:aaa
//.b:bb:bbb $ (1)
//..c:cc:ccc (1)
//...d:dd:ddd $ (1)
//....e:ee:eee $ (1)
//.....f:ff:fff $ (1)
//....<= b:bb:bbb $ (1)
//..
//..g:gg:ggg $ (1)
//.h:hh:hhh $ (1)
//..g:gg:ggg $ (1)", result.Trim());
//            }
//        }
//    }
//}