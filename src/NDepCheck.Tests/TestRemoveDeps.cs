using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;
using NDepCheck.Transforming.Removing;
using System.Linq;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestRemoveDeps {
        private void AssertEdgeCount(int expected, string o) {
            string[] lines = o.Split('\n');
            int actual = lines.Count(line => line.Contains(EdgeConstants.DIP_ARROW));
            Assert.AreEqual(expected, actual, $"Number of deps is different: '{o}'");
        }

        [TestMethod]
        public void TestRemoveSingleCycles() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveDeps).Name, "{", "-i", "}",
                Program.WriteFileOption.Opt, typeof(DipWriter).Name, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(2, o);
            }
        }

        [TestMethod]
        public void TestRemoveOkEdges() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveDeps).Name, "{", "-o", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(3, o);
            }
        }

        [TestMethod]
        public void TestRemoveBadEdges() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveDeps).Name, "{", "-b", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(1, o);
            }
        }

        [TestMethod]
        public void TestRemoveQuestionableEdges() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveDeps).Name, "{", "-q", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(3, o);
            }
        }

        [TestMethod]
        public void TestRemoveMatchingEdges() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveDeps).Name, "{", "-m", "inherit", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(2, o);
            }
        }
    }
}
