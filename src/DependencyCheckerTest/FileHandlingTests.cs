using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DependencyChecker;
using DependencyCheckerTest.dir1.dir3;
using DependencyCheckerTest2.dir1.dir3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DependencyCheckerTest {
    [TestClass]
    public class FileHandlingTests {
        private readonly Random _rnd = new Random();
        private string _basePath;

        public static Struct13 DummyForAssemblyCopying;
        public static Test2 DummyForAssembly2Copying;

        // TODO: To be tested - jeweils an jeder Stelle gefunden; und nicht gefunden [außer -x]
        // -d ... einfache Directorysuche
        // -d -d ... zwei einfache Directorysuchen
        // -s ... rek. Directorysuche (mit 2 Unterdirs)
        // -s -s ... zwei rek. DS
        // -s -d -s ... Mischung
        // -x ... Defaultfile
        // -s -x ... rek. Suche, dann Defaultfile
        // -d -x ... einfache Suche, dann Defaultfile
        // -x -x ... nicht erlaubt.

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

        private int Run(params string[] args) {
            return DependencyCheckerMain.Main(new List<string>(args.Select(s => s.Replace("%%", _basePath))) {
                    "DependencyCheckerTestAssembly.dll", 
                    "DependencyCheckerTestAssembly2.dll"
                }.ToArray());
        }

        private void WriteDep1To(string directory) {
            Write(directory, "DependencyCheckerTestAssembly.dll.dep", 
                @"DependencyCheckerTest.** ---> **
                  * ---? **
                ");
        }

        private void WriteDep2To(string directory) {
            Write(directory, "DependencyCheckerTestAssembly2.dll.dep", "DependencyCheckerTest2.** ---> **");
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
