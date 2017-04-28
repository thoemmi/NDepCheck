using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using NDepCheck.Rendering;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestSomeWriters {
        [TestMethod]
        public void TestItemWriter() {
            string inFile = Path.GetTempFileName() + ".txt";
            using (new TempFileProvider(inFile)) {

                using (var s = new MemoryStream()) {
                    var w = new ItemWriter();
                    var dependencies = w.CreateSomeTestDependencies();
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
        }


        [TestMethod]
        public void WriteAndReadDotNetDependencies() {
            string dipFileName = Path.GetTempFileName() + ".dip";
            using (var dipFile = new TempFileProvider(dipFileName)) {
                int result =
                    Program.Main(new[] {
                        MainTests.TestAssemblyPath,
                        Program.WriteDipOption.Opt, dipFile.Filename,
                        Program.DoResetOption.Opt, dipFile.Filename,
                        Program.CountDependenciesOption.Opt
                    });
                Assert.AreEqual(Program.OK_RESULT, result);

                Console.WriteLine(dipFile.Filename);
            }
        }
    }
}
