using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;
using NDepCheck.Transforming.Setting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestSetDeps {
        [TestMethod]
        public void TestResetBadCt() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(SetDeps).Name, "{", "-b", "}",                
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains(";10;5;0;"));
                Assert.IsTrue(o.Contains(";1;0;0;"));
                Assert.IsTrue(o.Contains(";5;0;0;"));
            }
        }

        [TestMethod]
        public void TestResetQuestionableCt() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(SetDeps).Name, "{", "-q", "}",                
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains(";10;0;3;"));
                Assert.IsTrue(o.Contains(";1;0;0;"));
                Assert.IsTrue(o.Contains(";5;0;2;"));
            }
        }

        [TestMethod]
        public void TestResetMatch() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(SetDeps).Name, "{", "-q", "-m", "define", "}",                
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains(";10;5;3;"));
                Assert.IsTrue(o.Contains(";1;0;0;"));
                Assert.IsTrue(o.Contains(";5;0;2;"));
            }
        }

        [TestMethod]
        public void TestResetBadAndQuestionableCt() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(SetDeps).Name, "{", "-b", "-q", "}",                
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains(";10;0;0;"));
                Assert.IsTrue(o.Contains(";1;0;0;"));
                Assert.IsTrue(o.Contains(";5;0;0;"));
            }
        }
    }
}
