using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Modifying;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestMarkDeps {
        [TestMethod]
        public void TestMarkFromTo() {
            using (var f = DisposingFile.CreateTempFileWithTail("Mark.dip")) {
                Assert.AreEqual(0,
                    Program.Main(new[] {
                        Program.DoResetOption.Opt,
                        Program.TransformTestDataOption.Opt, ".", typeof(MarkDeps).Name, "{",
                            MarkDeps.DependencyMatchOptions.DependencyMatchOption.Opt, "'M--->'N",
                            MarkDeps.DependencyMatchOptions.DependencyMatchOption.Opt, "A--->A",

                            MarkDeps.MarkLeftItemOption.Opt, "LeftMatch",
                            MarkDeps.MarkRightItemOption.Opt, "RightMatch",
                            MarkDeps.MarkDependencyItemOption.Opt, "DepMatch",

                            MarkDeps.UnmarkLeftItemOption.Opt, "N", // does nothing                    
                            MarkDeps.UnmarkRightItemOption.Opt, "M", // removes M from A because of match of 2nd pattern
                        "}",

                        Program.WriteDipOption.Opt, f.Filename
                    }));

                using (var sw = new StreamReader(f.Filename)) {
                    string o = sw.ReadToEnd();

                    Assert.IsTrue(o.Contains("SIMPLE:A'LeftMatch+RightMatch "));
                    Assert.IsTrue(o.Contains("SIMPLE:B'M "));
                    Assert.IsTrue(o.Contains("SIMPLE:C'N+RightMatch"));
                    Assert.IsTrue(o.Contains("=> 'DepMatch"));
                }
            }
        }

        [TestMethod]
        public void TestIncludeExclude() {
            using (var f = DisposingFile.CreateTempFileWithTail("Mark.dip")) {
                Assert.AreEqual(0,
                    Program.Main(new[] {
                        Program.DoResetOption.Opt,
                        Program.TransformTestDataOption.Opt, ".", typeof(MarkDeps).Name, "{",
                            MarkDeps.DependencyMatchOptions.DependencyMatchOption.Opt, "'M--",
                            MarkDeps.DependencyMatchOptions.NoMatchOption.Opt, "->B",

                            MarkDeps.MarkLeftItemOption.Opt, "LeftMatch",
                            MarkDeps.MarkRightItemOption.Opt, "RightMatch",
                            MarkDeps.MarkDependencyItemOption.Opt, "DepMatch",
                        "}",

                        Program.WriteDipOption.Opt, f.Filename
                    }));

                using (var sw = new StreamReader(f.Filename)) {
                    string o = sw.ReadToEnd();

                    Assert.IsTrue(Regex.IsMatch(o, "SIMPLE:A[^:=]*RightMatch"));
                    Assert.IsFalse(Regex.IsMatch(o, "SIMPLE:B[^:=]*RightMatch"));
                    Assert.IsTrue(Regex.IsMatch(o, "SIMPLE:C[^:=]*RightMatch"));

                    Assert.IsTrue(Regex.IsMatch(o, "SIMPLE:A[^:=]*LeftMatch"));
                    Assert.IsTrue(Regex.IsMatch(o, "SIMPLE:B[^:=]*LeftMatch"));
                    Assert.IsFalse(Regex.IsMatch(o, "SIMPLE:C[^:=]*LeftMatch"));
                }
            }
        }
    }
}