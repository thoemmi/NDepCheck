using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;
using NDepCheck.Transforming.Setting;
using NDepCheck.Transforming.Reversing;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestAddReverseDeps {
        [TestMethod]
        public void TestAddReverseWithoutRemove() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                // transform testdata
                "-s", ".", typeof(AddReverseDeps).Name, "{", "-minherit", "-uderived", "}",
                // write them as dip file
                "-r", typeof(DipWriter).Name, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.AreEqual(2, Regex.Matches(o, "inherit").Count);
                Assert.AreEqual(2, Regex.Matches(o, "derived").Count);
            }
        }

        [TestMethod]
        public void TestAddReverseWithRemove() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                // transform testdata
                "-s", ".", typeof(AddReverseDeps).Name, "{", "-r", "-minherit", "-uderived", "}",
                // write them as dip file
                "-r", typeof(DipWriter).Name, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.AreEqual(0, Regex.Matches(o, "inherit").Count);
                Assert.AreEqual(2, Regex.Matches(o, "derived").Count);
            }
        }
    }
}
