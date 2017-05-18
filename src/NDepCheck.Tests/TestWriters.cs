using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using NDepCheck.Rendering.TextWriting;

namespace NDepCheck.Tests {
    public class AbstractWriterTest {
        protected Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
            return new Dependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }
    }

    [TestClass]
    public class TestItemWriters : AbstractWriterTest {
        [TestMethod]
        public void TestItemWriter() {
            using (var s = new MemoryStream()) {
                var w = new ItemWriter();
                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies();
                w.RenderToStreamForUnitTests(new GlobalContext(), dependencies, s, "");

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
    }

    [TestClass]
    public class TestDipWriters : AbstractWriterTest {
        [TestMethod]
        public void WriteAndReadDotNetDependencies() {
            using (DisposingFile dipFile = DisposingFile.CreateTempFileWithTail(".dip")) {
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
    }
}