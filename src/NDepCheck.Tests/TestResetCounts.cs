using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;
using NDepCheck.Transforming.Counts;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestResetCounts {
        [TestMethod]
        public void TestResetBadCt() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                // transform testdata
                "-s", ".", typeof(ResetCounts).Name, "{", "-b", "}",
                // write them as dip file
                "-r", typeof(DipWriter).Name, outFile
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
                // transform testdata
                "-s", ".", typeof(ResetCounts).Name, "{", "-q", "}",
                // write them as dip file
                "-r", typeof(DipWriter).Name, outFile
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
                // transform testdata
                "-s", ".", typeof(ResetCounts).Name, "{", "-q", "-mdefine", "}",
                // write them as dip file
                "-r", typeof(DipWriter).Name, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains(";10;5;3;"));
                Assert.IsTrue(o.Contains(";1;0;0;"));
                Assert.IsTrue(o.Contains(";5;0;2;"));
            }
        }
    }
}
