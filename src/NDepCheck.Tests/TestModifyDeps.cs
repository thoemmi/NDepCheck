using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Modifying;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class TestModifyDeps {
        [TestMethod]
        public void TestResetBadCt() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{", ModifyDeps.ModificationsOption.Opt, "--->=>reset-bad", "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
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
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{", ModifyDeps.ModificationsOption.Opt, "--->=>reset-questionable", "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
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
        public void TestSimpleMatchReset() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{", ModifyDeps.ModificationsOption.Opt,
                    "--'define->=>reset-questionable",
                    "--->=>",
                "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
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
                Program.ConfigureOption.Opt, typeof(ModifyDeps).Name, "{",
                    ModifyDeps.ModificationsOption.Opt, "--->=>reset-bad,reset-questionable",
                "}",
                Program.TransformTestDataOption.Opt, ".", typeof(ModifyDeps).Name,
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
