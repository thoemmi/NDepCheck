using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Modifying;
using System.Linq;
using NDepCheck.Rendering.TextWriting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestRemoveDeps {
        private void AssertEdgeCount(int expected, string o) {
            string[] lines = o.Split('\n');
            int actual = lines.Count(line => line.Contains(Dependency.DIP_ARROW));
            Assert.AreEqual(expected, actual, $"Number of deps is different: '{o}'");
        }

        [TestMethod]
        public void TestRemoveSingleCycles() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--O->=>delete", "--->=>",
                "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
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
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--~?&~!'->=>delete", "--->=>",
                "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(2, o);
            }
        }

        [TestMethod]
        public void TestRemoveBadEdges() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--!'->=>delete", "--->=>",
                "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
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
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--?'->=>delete", "--->=>",
                "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(2, o);
            }
        }

        [TestMethod]
        public void TestRemoveMatchingEdges() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--'use->=>delete", "--->=>",
                "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                AssertEdgeCount(2, o);
            }
        }
    }
}
