using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NDepCheck.TestAssembly.dir1.dir3;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.TestAssemblyÄÖÜß.dir1.dir3;

namespace NDepCheck.Tests {
    [TestClass]
    public class FileHandlingTests {
        private readonly Random _rnd = new Random();
        private string _basePath;

        public static Struct13 DummyForAssemblyCopying;
        public static Test2 DummyForAssembly2Copying;

        // + relativer Pfad zu File, wo's gefunden wurde. DLL/EXE muss woanders stehen!

        [TestInitialize]
        public void TestSetup() {
            _basePath = Path.Combine(Path.GetTempPath(), _rnd.Next(0, 100000).ToString());
        }

        [TestCleanup]
        public void TestCleanup() {
            Directory.Delete(_basePath, true);
        }

        [TestMethod]
        public void TestDOk() {
            WriteDep1To(@"a\b");
            WriteDep2To(@"a\c");
            Assert.AreEqual(Program.OK_RESULT, Run(@"-f Check { -s %%\a }".Split(' ')));
        }

        [TestMethod]
        public void TestDDOk() {
            WriteDep1To(@"a\b\x\y");
            WriteDep2To(@"a\c\x\y\z");
            Assert.AreEqual(Program.OK_RESULT, Run(
                @"-f Check { -s %%\a\b -s %%\a\x -s %%\a\c }".Split(' ')));
        }

        [TestMethod]
        public void TestDPlusOk() {
            WriteDep1PlusTo(@"a\b");
            WriteDep2PlusTo(@"a\b\c");
            Assert.AreEqual(Program.OK_RESULT, Run(@"-f Check { -s %%\a\b }".Split(' ')));
        }

        [TestMethod]
        public void TestDoublyNestedDefine() {
            Write(@"a\b", "A.dep",
                @"_A := NDepCheck.TestAssembly
                ");
            Write(@"a\b", "B.dep",
                @"+ A.dep
                _B := _A
                ");
            Write(@"a\b\c", "NDepCheck.TestAssembly.dll.dep",
                @"+ ..\B.dep
                  $ DOTNETCALL ---> DOTNETCALL
                  _B.** ---> **
                  :* ---? **

                  $ DOTNETREF ---> DOTNETREF 
                  * ---> *
                ");
            WriteDep2To(@"a\b\c");
            Assert.AreEqual(Program.OK_RESULT, Run(@"-f Check { -s %%\a\b }".Split(' ')));
        }

        [TestMethod]
        public void TestPushDownDefine() {
            Write(@"a\b", "A.dep",
                @"_A := NDepCheck.TestAssembly
                  + B.dep
                ");
            Write(@"a\b", "B.dep",
                @"$ DOTNETCALL ---> DOTNETCALL
                  _A.** ---> **
                  :* ---? **
                ");
            Write(@"a\b\c", "NDepCheck.TestAssembly.dll.dep",
                @"+ ..\A.dep
                ");
            WriteDep2To(@"a\b\c");
            Assert.AreEqual(Program.OK_RESULT, Run(@"-f Check { -s %%\a\b }".Split(' ')));
        }

        [TestMethod]
        public void TestExcept() {
            WriteDep1To(@"a\b");
            WriteDefaultSetTo(@"a\c");

            int result = Program.Main(new List<string> {
                    "-j=" + GetPath("NDepCheck.TestAssembly.dll"),
                    "-j=" + "NDepCheck.TestAssemblyÄÖÜß.*.dll", "-", "NDepCheck.TestAssemblyÄÖÜß.dll",
                    "-f", "Check", "{", "-s=" + _basePath + @"\a", "}"
            }.ToArray());
            Assert.AreEqual(Program.OK_RESULT, result);
        }

        private int Run(params string[] args) {
            return Program.Main(new List<string>(args.Select(s => s.Replace("%%", _basePath))) {
                    GetPath("NDepCheck.TestAssembly.dll"),
                    "NDepCheck.TestAssemblyÄÖÜß.*.dll"
                }.ToArray());
        }

        private void WriteDep1To(string directory) {
            Write(directory, "NDepCheck.TestAssembly.dll.dep",
                @"
                  $ DOTNETCALL ---> DOTNETCALL
                  NDepCheck.TestAssembly.** ---> **
                  :* ---? **

                  $ DOTNETREF ---> DOTNETREF
                  ** ---? **
                ");
        }

        private void WriteDep1PlusTo(string directory) {
            Write(directory, "NDepCheck.TestAssembly.dll.dep", @"+ Dep1Include\Dep1.dep");
            Write(directory + @"\Dep1Include", "Dep1.dep", @"
                  $ DOTNETCALL ---> DOTNETCALL
                  NDepCheck.TestAssembly.** ---> **
                  :* ---? **

                  $ DOTNETREF ---> DOTNETREF
                  ** ---? **
                ");
        }

        private void WriteDep2To(string directory) {
            Write(directory, "NDepCheck.TestAssemblyÄÖÜß.dll.dep",
                @"$ DOTNETCALL ---> DOTNETCALL
                  NDepCheck.TestAssemblyÄÖÜß.** ---> **

                  $ DOTNETREF ---> DOTNETREF
                  ** ---? **
                ");
        }

        private void WriteDep2PlusTo(string directory) {
            Write(directory, "NDepCheck.TestAssemblyÄÖÜß.dll.dep", @"+ Dep2Include\Dep2A.dep");
            Write(directory + @"\Dep2Include", "Dep2A.dep", @"+ ..\Dep2B.dep");
            Write(directory, "Dep2B.dep",
                @"$ DOTNETCALL ---> DOTNETCALL

                NDepCheck.TestAssemblyÄÖÜß.** ---> **
                ASSEMBLY.NAME=** ---> ASSEMBLY.NAME=**");
        }

        private void WriteDefaultSetTo(string directory) {
            Write(directory, "Defaults.dep", @"
                $ DOTNETCALL ---> DOTNETCALL
                ** ---? **

                $ DOTNETREF ---> DOTNETREF
                * ---> *");
        }

        private void Write(string directory, string ruleFileName, string data) {
            DirectoryInfo di = Directory.CreateDirectory(Path.Combine(_basePath, directory));
            using (TextWriter tw = new StreamWriter(Path.Combine(di.FullName, ruleFileName), false, Encoding.UTF8)) {
                tw.WriteLine(data);
            }
        }

        private string GetPath(string assembly) {
            // ReSharper disable AssignNullToNotNullAttribute - always works in Test
            return Path.Combine(Path.GetDirectoryName(typeof(MainTests).Assembly.Location), assembly);
            // ReSharper restore AssignNullToNotNullAttribute
        }
    }
}
