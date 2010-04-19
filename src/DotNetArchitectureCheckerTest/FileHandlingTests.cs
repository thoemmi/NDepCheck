using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetArchitectureChecker;
using DotNetArchitectureCheckerTest.dir1.dir3;
using DotNetArchitectureCheckerTest2.dir1.dir3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetArchitectureCheckerTest {
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
        public void TestSOk() {
            WriteDep1To(@"a\b");
            WriteDep2To(@"a\c");
            Assert.AreEqual(0, Run(@"-s=%%\a"));
        }

        [TestMethod]
        public void TestDNotOk() {
            WriteDep1To(@"a\b");
            WriteDep2To(@"a\c");
            Assert.AreEqual(6, Run(@"-d=%%\a"));
        }

        [TestMethod]
        public void TestDDOk() {
            WriteDep1To(@"a\b");
            WriteDep2To(@"a\c");
            Assert.AreEqual(0, Run(@"-d=%%\a\b", @"-d=%%\a\xx", @"-d=%%\a\c"));
        }

        [TestMethod]
        public void TestSSOk() {
            WriteDep1To(@"a\b\x\y");
            WriteDep2To(@"a\c\x\y\z");
            Assert.AreEqual(0, Run(@"-s=%%\a\b", @"-s=%%\a\x", @"-s=%%\a\c"));
        }

        [TestMethod]
        public void TestDDNotOk() {
            WriteDep1To(@"a\b");
            WriteDep2To(@"a\c");
            Assert.AreEqual(6, Run(@"-d=%%\a\yy", @"-d=%%\a\xx", @"-d=%%\a\b"));
        }

        [TestMethod]
        public void TestDDXOk() {
            WriteDep1To(@"a\b");
            WriteDep2To(@"a\c");
            WriteXTo(@"a\x");
            Assert.AreEqual(0, Run(@"-x=%%\a\x\Defaults.dep", @"-d=%%\a\yy", @"-d=%%\a\xx", @"-d=%%\a\b"));
        }

        [TestMethod]
        public void TestDPlusOk() {
            WriteDep1PlusTo(@"a\b");
            WriteDep2PlusTo(@"a\b");
            Assert.AreEqual(0, Run(@"-d=%%\a\xx", @"-d=%%\a\b"));
        }

        [TestMethod]
        public void TestSPlusOk() {
            WriteDep1PlusTo(@"a\b");
            WriteDep2PlusTo(@"a\b\c");
            Assert.AreEqual(0, Run(@"-s=%%\a\b"));
        }

        [TestMethod]
        public void TestDoubleMacro() {
            Write(@"a\b", "A.dep",
                @"_A := DotNetArchitectureCheckerTest
                ");
            Write(@"a\b", "B.dep",
                @"+ A.dep
                _B := _A
                ");
            Write(@"a\b\c", "DotNetArchitectureCheckerTestAssembly.dll.dep",
                @"+ ..\B.dep
                  _B.** ---> **
                  * ---? **
                ");
            WriteDep2To(@"a\b\c");
            Assert.AreEqual(0, Run(@"-s=%%\a\b"));
        }

        private int Run(params string[] args) {
            return DotNetArchitectureCheckerMain.Main(new List<string>(args.Select(s => s.Replace("%%", _basePath))) {
                    "DotNetArchitectureCheckerTestAssembly.dll",
                    "DotNetArchitectureCheckerTestAssembly2.dll"
                }.ToArray());
        }

        private void WriteDep1To(string directory) {
            Write(directory, "DotNetArchitectureCheckerTestAssembly.dll.dep", 
                @"DotNetArchitectureCheckerTest.** ---> **
                  * ---? **
                ");
        }

        private void WriteDep1PlusTo(string directory) {
            Write(directory, "DotNetArchitectureCheckerTestAssembly.dll.dep",
                @"+ Dep1Include\Dep1.dep");
            Write(directory + @"\Dep1Include", "Dep1.dep",
                @"DotNetArchitectureCheckerTest.** ---> **
                  * ---? **
                ");
        }

        private void WriteDep2To(string directory) {
            Write(directory, "DotNetArchitectureCheckerTestAssembly2.dll.dep", "DotNetArchitectureCheckerTest2.** ---> **");
        }

        private void WriteDep2PlusTo(string directory) {
            Write(directory, "DotNetArchitectureCheckerTestAssembly2.dll.dep",
                @"+ Dep2Include\Dep2A.dep");
            Write(directory + @"\Dep2Include", "Dep2A.dep",
                @"+ ..\Dep2B.dep");
            Write(directory, "Dep2B.dep", "DotNetArchitectureCheckerTest2.** ---> **");
        }

        private void WriteXTo(string directory) {
            Write(directory, "Defaults.dep", "** ---? **");
        }

        private void Write(string directory, string depFileName, string data) {
            DirectoryInfo di = Directory.CreateDirectory(Path.Combine(_basePath, directory));
            using (TextWriter tw = new StreamWriter(Path.Combine(di.FullName, depFileName))) {
                tw.WriteLine(data);
            }
        }
    }
}
