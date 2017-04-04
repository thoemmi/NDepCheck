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
                Program.TransformTestDataOption.Opt, ".", typeof(AddReverseDeps).Name, "{", "-m=inherit", "-u=derived", "}",                
                Program.WriteDipOption.Opt, outFile
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
                Program.TransformTestDataOption.Opt, ".", typeof(AddReverseDeps).Name, "{", "-r", "-m", "inherit", "-u", "derived", "}",                
                Program.WriteDipOption.Opt, outFile
            }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.AreEqual(0, Regex.Matches(o, "inherit").Count);
                Assert.AreEqual(2, Regex.Matches(o, "derived").Count);
            }
        }
    }
}
