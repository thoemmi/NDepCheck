using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using NDepCheck.Rendering.TextWriting;

namespace NDepCheck.Tests {
    [ExcludeFromCodeCoverage]
    public class AbstractWriterTest {
        protected Dependency FromTo(WorkingGraph graph, Item from, Item to, int ct = 1, int questionable = 0) {
            return graph.CreateDependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionable, notOkReason: "test data");
        }
    }

    [TestClass, ExcludeFromCodeCoverage]
    public class TestItemWriters : AbstractWriterTest {
        [TestMethod]
        public void TestItemWriter() {
            var gc = new GlobalContext();
            using (var s = new MemoryStream()) {
                var w = new ItemWriter();
                IEnumerable<Dependency> dependencies = w.CreateSomeTestDependencies(gc.CurrentGraph);
                w.RenderToStreamForUnitTests(gc, dependencies, s, "");

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

    [TestClass, ExcludeFromCodeCoverage]
    public class TestDipWriters : AbstractWriterTest {
        [TestMethod]
        public void WriteAndReadDotNetDependencies() {
            using (DisposingFile dipFile = DisposingFile.CreateTempFileWithTail(".dip")) {
                int result =
                    Program.Main(new[] {
                        MainTests.TestAssemblyPath, Program.WriteDipOption.Opt, dipFile.FileName,
                        Program.DoResetOption.Opt, dipFile.FileName, Program.CountDependenciesOption.Opt
                    });
                Assert.AreEqual(Program.OK_RESULT, result);

                Console.WriteLine(dipFile.FileName);
            }
        }
    }
}