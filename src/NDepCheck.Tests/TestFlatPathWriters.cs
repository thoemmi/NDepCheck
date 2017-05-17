using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering.TextWriting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestFlatPathWriters : AbstractWriterTest {
        [TestMethod]
        public void TestSimpleFlatPathWriterForOnePath() {
            ItemType t3 = ItemType.New("T3(ShortName:MiddleName:LongName)");

            var a = Item.New(t3, "a:aa:aaa".Split(':'));
            var b = Item.New(t3, "b:bb:bbb".Split(':'));

            var dependencies = new[] {
                FromTo(a, b),
            };

            using (var s = new MemoryStream()) {
                var w = new OldPathWriter();
                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
b:bb:bbb $ (1)", result.Trim());
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
                var w = new OldPathWriter();
                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual("", result.Trim());
            }
        }

        [TestMethod]
        public void TestSimpleFlatPathWriter() {
            using (var s = new MemoryStream()) {
                var w = new OldPathWriter();
                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
b:bb:bbb $ (1)
c:cc:ccc (1)
d:dd:ddd $ (1)
e:ee:eee $ (1)
f:ff:fff $ (1)

a:aa:aaa
b:bb:bbb $ (1)
c:cc:ccc (1)
d:dd:ddd $ (1)
<= b:bb:bbb $ (1)

a:aa:aaa
b:bb:bbb $ (1)
g:gg:ggg $ (1)

a:aa:aaa
h:hh:hhh $ (1)
g:gg:ggg $ (1)", result.Trim());
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
                var w = new OldPathWriter();
                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "F");
                string result = Encoding.ASCII.GetString(s.ToArray());
                Assert.AreEqual(@"a:aa:aaa
b:bb:bbb $ (1)
d:dd:ddd $ (1)", result.Trim());
            }
        }

        [TestMethod]
        public void TestCountPaths() {
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

            using (var temp = DisposingFile.CreateTempFileWithTail(".txt")) {
                var w = new OldPathWriter();
                w.Render(new GlobalContext(), dependencies, null,
                    "{ -ps F -pi a -ci 2 -pi g }".Replace(" ", "\r\n"), temp.Filename, false);

                using (var sw = new StreamReader(temp.Filename)) {
                    string o = sw.ReadToEnd();
                    Console.WriteLine(o);
                    Assert.IsTrue(o.Contains("b:2 (*)"));
                    Assert.IsTrue(o.Contains("c:2 (*)"));
                    Assert.IsTrue(o.Contains("d:3 (2)"));
                    Assert.IsTrue(o.Contains("e:4 (2)"));
                    Assert.IsTrue(o.Contains("g:5 $ (2)"));
                }
            }
        }
    }
}