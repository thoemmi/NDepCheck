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
g:gg:ggg $

a:aa:aaa
h:hh:hhh $
g:gg:ggg $", result.Trim());
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
    }
}