// (c) HMMüller 2006...2010

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DotNetArchitectureChecker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetArchitectureCheckerTest {
    /// <remarks>
    /// Tests of DotNetArchitectureChecker
    /// </remarks>
    [TestClass]
    public class MainTests {
        private static void AssertNotContains(string path, string s) {
            using (TextReader tr = new StreamReader(path)) {
                string all = tr.ReadToEnd();
                Assert.IsFalse(all.Contains(s), all);
            }
        }

        [TestMethod]
        public void GeneralSucceedingTest() {
            string depFile = Path.GetTempFileName();
            using (TextWriter tw = new StreamWriter(depFile, false, Encoding.Default)) {
                tw.Write(
                    @"
// Test dependencies for DotNetArchitectureChecker

    // Every class may use all classes from its own namespace.
(**).* ---> \1.*

    // Special dependency for classes from global namespace
    // (the pattern above will not work, because it contains a
    // period): A class from the global namespace may use
    // all classes from that namespace.
* ---> *

    // Every class may use all classes from child namespaces
    // of its own namespace.
(**).* ---> \1.**.*

    // Every class may use all of System.
** ---> System.**

====> :=
    \L.** ---> \R.**
    \L.** ---> \L.**
=:


    // DotNetArchitectureChecker may use antlr and itself.
DotNetArchitectureChecker ====> antlr

    // DotNetArchitectureChecker must not use Windows Forms.
DotNetArchitectureChecker.** ---! System.Windows.Forms.**

_TES  := asdasdasdasdasdasd
_TESTS := DotNetArchitectureCheckerTest
_TEST_OTHERS := xxxxxxxxxxxxx
_TEST := asdasdasdasdasdasd

    // Test declarations from dir1.dir2 may use declarations from dir1.dir3.
_TESTS.dir1.dir2.* ---> _TESTS.dir1.dir3.*


_TESTS.dir1.* ---> _TESTS.dir1.dir3.*
    
    // SomeClass::AnotherMethod may use NamespacelessTestClassForDotNetArchitectureChecker -
    // but this is questionable.
_TESTS.dir1.dir2.SomeClass::AnotherMethod ---? NamespacelessTestClassForDotNetArchitectureChecker

    // A questionable rule that never fires - it should be output.
asdlkfj.* ---? askdjf.*;

    // Umlautmatching rules
_TESTS.dirümläut.** ---> _TESTS.dirümläutö.** 
_TESTS.dirümläut.** ---> _TESTS.dirümläutß.** 
_TESTS.dirumlaut.** ---> _TESTS.dirumlauts.** 

    // Test case for ""open item 5""
    // Methods called InnerClassMethod may call each other
**/**::InnerClassMethod ---> **/**::InnerClassMethod 

    // Tests must be able to see tested classes
_TESTS.** ---> DotNetArchitectureChecker.**

    // Tests may use Microsoft.VisualStudio.TestTools.
_TESTS.** ---> Microsoft.VisualStudio.TestTools.**

// ------------------

    // In these tests, we ignore everything in the
    // current test class.
DotNetArchitectureCheckerTest.UnitTests ---> **

    // All of system is ignored
% ()System.**

    // Classes in DotNetArchitectureCheckerTests are shown separately, without the namespace
% DotNetArchitectureCheckerTests.(**)

    // Classes in DotNetArchitectureChecker are also shown separately, but with the namespace
% (DotNetArchitectureChecker).*

    // antlr classes are shown by namespace
% (antlr).*
% (antlr.**).*

    // Top level classes are shown as their class name
% (*)
                ");
            }
            string outFile = Path.GetTempFileName();
            //string workingDir = Path.GetTempPath();
            using (TextWriter tw = new StreamWriter(outFile)) {
                TextWriter oldOut = Console.Out;
                Console.SetOut(tw);
                string[] args = { "/v", "/x", "/f=" + depFile, "DotNetArchitectureCheckerTest.dll" };
                DotNetArchitectureCheckerMain.Main(args);
                Console.SetOut(oldOut);
            }
            AssertNotContains(outFile, "****");
            File.Delete(outFile);
            File.Delete(depFile);
        }

        [TestMethod]
        public void SmallGrapherTest() {
            var rs = new DependencyRuleSet(true);
            rs.AddGraphAbstractions("<test>", 0, "% (**)");

            var options = new Options {
                DotFilename = Path.Combine(Path.GetTempPath(), "test.dot")
            };
            var dg = new DependencyGrapher(new DependencyChecker(options), options);
            var deps = new List<Dependency> {
                                                new Dependency("a1", "a1", "a2", "a2", null, 0, 0, 0, 0),
                                                new Dependency("a1", "a1", "a4", "a4", null, 0, 0, 0, 0),
                                                new Dependency("a2", "a2", "a3", "a3", null, 0, 0, 0, 0),
                                                new Dependency("a2", "a2", "a4", "a4", null, 0, 0, 0, 0),
                                                new Dependency("a3", "a3", "a4", "a4", null, 0, 0, 0, 0),
                                                new Dependency("b1", "b1", "b2", "b2", null, 0, 0, 0, 0),
                                                new Dependency("b1", "b1", "b4", "b4", null, 0, 0, 0, 0),
                                                new Dependency("b2", "b2", "b3", "b3", null, 0, 0, 0, 0),
                                                new Dependency("b2", "b2", "b4", "b4", null, 0, 0, 0, 0),
                                                new Dependency("b3", "b3", "b2", "b2", null, 0, 0, 0, 0),
                                                new Dependency("b3", "b3", "b4", "b4", null, 0, 0, 0, 0)
                                            };

            dg.Graph(rs, deps);

            // what to assert??
        }

        [TestMethod]
        public void TransitiveReductionGrapherTest() {
            var rs = new DependencyRuleSet(true);
            rs.AddGraphAbstractions("<test>", 0, "% (**)");

            var options = new Options {
                DotFilename = Path.Combine(Path.GetTempPath(), "test.dot"),
                ShowTransitiveEdges = true
            };
            var dg = new DependencyGrapher(new DependencyChecker(options), options);
            var deps = new List<Dependency> {
                                                new Dependency("a1", "a1", "a2", "a2", null, 0, 0, 0, 0),
                                                new Dependency("a1", "a1", "a4", "a4", null, 0, 0, 0, 0),
                                                new Dependency("a2", "a2", "a3", "a3", null, 0, 0, 0, 0),
                                                new Dependency("a2", "a2", "a4", "a4", null, 0, 0, 0, 0),
                                                new Dependency("a3", "a3", "a4", "a4", null, 0, 0, 0, 0),
                                                new Dependency("b1", "b1", "b2", "b2", null, 0, 0, 0, 0),
                                                new Dependency("b1", "b1", "b4", "b4", null, 0, 0, 0, 0),
                                                new Dependency("b2", "b2", "b3", "b3", null, 0, 0, 0, 0),
                                                new Dependency("b2", "b2", "b4", "b4", null, 0, 0, 0, 0),
                                                new Dependency("b3", "b3", "b2", "b2", null, 0, 0, 0, 0),
                                                new Dependency("b3", "b3", "b4", "b4", null, 0, 0, 0, 0)
                                            };

            dg.Graph(rs, deps);

            // what to assert??
        }

        [TestMethod]
        public void Exit0() {
            {
                string depFile = Path.GetTempFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    DotNetArchitectureCheckerTest.** ---> DotNetArchitectureCheckerTest.**
                    DotNetArchitectureCheckerTest.** ---> System.**
                    DotNetArchitectureCheckerTest.dir1.dir2.SomeClass::* ---? NamespacelessTestClassForDotNetArchitectureChecker::I
                    * ---? System.*
                ");
                }
                Assert.AreEqual(0, DotNetArchitectureCheckerMain.Main(new[] { "-x=" + depFile, "DotNetArchitectureCheckerTestAssembly.dll" }));
                File.Delete(depFile);
            }
        }

        [TestMethod]
        public void NestedMacroTest1() {
            {
                string depFile = Path.GetTempFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    _A := DotNetArchitectureCheckerTest
                    _B := _A
                    _B.** ---> _B.**
                    _B.** ---> System.**
                    _B.dir1.dir2.SomeClass::* ---? NamespacelessTestClassForDotNetArchitectureChecker::I
                    * ---? System.*
                ");
                }
                Assert.AreEqual(0, DotNetArchitectureCheckerMain.Main(new[] { "-x=" + depFile, "DotNetArchitectureCheckerTestAssembly.dll" }));
                File.Delete(depFile);
            }
        }

        [TestMethod]
        public void Exit1() {
            Assert.AreEqual(1, DotNetArchitectureCheckerMain.Main(new string[] { }));
            Assert.AreEqual(1, DotNetArchitectureCheckerMain.Main(new[] { "/y" }));
            Assert.AreEqual(1, DotNetArchitectureCheckerMain.Main(new[] { "/v", "-v", "-i", "/i=100", "-t", "/t", "-g=someDotFile.dot" }));
        }

        [TestMethod]
        public void Exit2() {
            {
                Assert.AreEqual(2, DotNetArchitectureCheckerMain.Main(new[] { "/xnonexistingfile.dep" }));
            }
        }

        [TestMethod]
        public void Exit3() {
            {
                string depFile = Path.GetTempFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    ** ---> blabla
                ");
                }
                Assert.AreEqual(3, DotNetArchitectureCheckerMain.Main(new[] { "-x=" + depFile, "DotNetArchitectureCheckerTestAssembly.dll" }));
                File.Delete(depFile);
            }
            {
                string depFile = Path.GetTempFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write("");
                }
                Assert.AreEqual(3, DotNetArchitectureCheckerMain.Main(new[] { "-x=" + depFile, "DotNetArchitectureCheckerTestAssembly.dll" }));
                File.Delete(depFile);
            }
        }

        [TestMethod]
        public void Exit4() {
            string depFile = Path.GetTempFileName();
            using (TextWriter tw = new StreamWriter(depFile)) {
                tw.Write(@"
                    ** ---> blabla
                ");
            }
            Assert.AreEqual(4, DotNetArchitectureCheckerMain.Main(new[] { "/x=" + depFile, "nonexistingfile.dll" }));
            File.Delete(depFile);
        }

        [TestMethod]
        public void Exit5() {
            {
                string depFile = Path.GetTempFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(
                        @"
                    ====> :=    
                       ** ---> blabla
                    =:
                    ===>> :=    
                       ** ---> blabla
                    =:
                ");
                }
                Assert.AreEqual(5, DotNetArchitectureCheckerMain.Main(new[] { "-x=" + depFile, "DotNetArchitectureCheckerTestAssembly.dll" }));
                File.Delete(depFile);
            }
            {
                string depFile = Path.GetTempFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(
                        @"
                    --> :=    
                       ** ---> blabla
                    =:
                ");
                }
                Assert.AreEqual(5, DotNetArchitectureCheckerMain.Main(new[] { "-x=" + depFile, "DotNetArchitectureCheckerTestAssembly.dll" }));
                File.Delete(depFile);
            }
        }
    }
}