using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Modifying;
using NDepCheck.Transforming.DependencyCreating;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestAddReverseDeps {
        [TestMethod]
        public void TestAddReverseWithoutRemove() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(AddReverseDeps).Name, "{",
                    // Do not remove original: AddReverseDeps.RemoveOriginalOption.Opt,
                    AddReverseDeps.DependencyMatchOption.Opt, "'inherit",
                    AddReverseDeps.MarkerToAddOption.Opt, "derived",
                "}",
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--'derived->=>-inherit", "--->=>",
                "}",
                Program.TransformOption.Opt, typeof(ModifyDeps).Name,
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
                Program.TransformTestDataOption.Opt, ".", typeof(AddReverseDeps).Name, "{",
                    AddReverseDeps.RemoveOriginalOption.Opt, 
                    AddReverseDeps.DependencyMatchOption.Opt, "'inherit",
                    AddReverseDeps.MarkerToAddOption.Opt, "derived",
                "}",
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--->=>-inherit",
                "}",
                Program.TransformOption.Opt, typeof(ModifyDeps).Name,
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
