using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Modifying;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestMarkDeps {
        [TestMethod]
        public void MarkFromTo() {
            string outFile = Path.GetTempFileName() + "Mark.dip";
            using (new TempFileProvider(outFile)) {
                Assert.AreEqual(0,
                    Program.Main(new[] {
                        Program.DoResetOption.Opt,
                        Program.TransformTestDataOption.Opt, ".", typeof(MarkDeps).Name, "{",
                            MarkDeps.DependencyMatchOption.Opt, "'M--->'N",
                            MarkDeps.DependencyMatchOption.Opt, "A--->A",

                            MarkDeps.MarkLeftItemOption.Opt, "LeftMatch",
                            MarkDeps.MarkRightItemOption.Opt, "RightMatch",
                            MarkDeps.MarkDependencyItemOption.Opt, "DepMatch",

                            MarkDeps.UnmarkLeftItemOption.Opt, "N", // does nothing                    
                            MarkDeps.UnmarkRightItemOption.Opt, "M", // removes M from A because of match of 2nd pattern
                        "}",

                        Program.WriteDipOption.Opt, outFile
                    }));

                using (var sw = new StreamReader(outFile)) {
                    string o = sw.ReadToEnd();

                    Console.WriteLine(o);

                    Assert.IsTrue(o.Contains("SIMPLE:A'LeftMatch+RightMatch "));
                    Assert.IsTrue(o.Contains("SIMPLE:B'M "));
                    Assert.IsTrue(o.Contains("SIMPLE:C'N+RightMatch"));
                    Assert.IsTrue(o.Contains("=> DepMatch"));
                }
            }
        }
    }
}