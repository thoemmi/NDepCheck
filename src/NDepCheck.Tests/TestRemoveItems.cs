using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Marking;
using System.Linq;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestRemoveItems {
        private void AssertEdgeCount(int expected, string o) {
            string[] lines = o.Split('\n');
            int actual = lines.Count(line => line.Contains(EdgeConstants.DIP_ARROW));
            Assert.AreEqual(expected, actual, $"Number of deps is different: '{o}'");
        }

        [TestMethod]
        public void TestRemoveSources() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveItems).Name, "{", "-s", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                // Only A is removed
                AssertEdgeCount(10, o);
            }
        }

        [TestMethod]
        public void TestRemoveSourcesRecursively() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveItems).Name, "{", "-s", "-r", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();
                // A and B are removed
                AssertEdgeCount(9, o);
            }
        }

        [TestMethod]
        public void TestRemoveSinks() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveItems).Name, "{", "-d", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                // I and J are removed
                AssertEdgeCount(9, o);
            }
        }

        [TestMethod]
        public void TestRemoveSinksRecursively() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveItems).Name, "{", "-d", "-r", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                // F, G, H, I, and J are removed
                AssertEdgeCount(6, o);
            }
        }

        [TestMethod]
        public void TestRemoveSinksRecursivelyIgnoringSelfCycles() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(RemoveItems).Name, "{", "-d", "-r", "-i", "}",
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                // E, F, G, H, I, and J are removed
                AssertEdgeCount(4, o);
            }
        }
    }
}
