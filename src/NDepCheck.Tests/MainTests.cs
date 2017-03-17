// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.GraphTransformations;
using NDepCheck.Rendering;
using NDepCheck.TestRenderer;

namespace NDepCheck.Tests {
    /// <remarks>
    /// Tests of NDepCheck
    /// </remarks>
    [TestClass]
    public class MainTests {
        // ReSharper disable once AssignNullToNotNullAttribute - certainly ok in this test
        private static readonly string TestAssemblyPath =
            Path.Combine(Path.GetDirectoryName(typeof(MainTests).Assembly.Location), "NDepCheck.TestAssembly.dll");

        // ReSharper disable UnusedParameter.Local - Das ist ein Assert, daher die "Nur-Verwendung" in Assert ok :-)
        private static void AssertNotContains(string path, string s) {
            // ReSharper restore UnusedParameter.Local
            using (TextReader tr = new StreamReader(path)) {
                string all = tr.ReadToEnd();
                Assert.IsFalse(all.Contains(s), all);
            }
        }

        [TestMethod]
        public void GeneralSucceedingTest() {
            string depFile = CreateTempDotNetDepFileName();
            using (TextWriter tw = new StreamWriter(depFile, false, Encoding.Default)) {
                tw.Write(@"
// Test dependencies for NDepCheck

$ DOTNETCALL ---> DOTNETCALL
                  
    // Every class may use all classes from its own namespace.
(**): ---> \1:

    // Special dependency for classes from global namespace
    // A class from the global namespace may use
    // all classes from that namespace.
-:** ---> -:**

    // Every class may use all classes from child namespaces
    // of its own namespace.
(**): ---> \1.**:

    // Every class may use all of System.
** ---> System.**:

=---> :=
    \L.** ---> \R.**
    \L.** ---> \L.**
=:


    // NDepCheck may use antlr and itself.
NDepCheck =---> antlr

    // NDepCheck must not use Windows Forms.
NDepCheck.** ---! System.Windows.Forms.**

_TES  := asdasdasdasdasdasd
_TESTS := NDepCheck.Tests
_TEST_OTHERS := xxxxxxxxxxxxx
_TEST := asdasdasdasdasdasd

    // Test declarations from dir1.dir2 may use declarations from dir1.dir3.
_TESTS.dir1.dir2:* ---> _TESTS.dir1.dir3:*


_TESTS.dir1:* ---> _TESTS.dir1.dir3:*
    
    // ...SomeClass.AnotherMethod may use -:NamespacelessTestClassForNDepCheck -
    // but this is questionable.
_TESTS.dir1.dir2:SomeClass::AnotherMethod ---? -:NamespacelessTestClassForNDepCheck

    // A questionable rule that never fires - it should be output.
asdlkfj.* ---? askdjf.*;

    // Umlautmatching rules
_TESTS.dirümläut.** ---> _TESTS.dirümläutö.** 
_TESTS.dirümläut.** ---> _TESTS.dirümläutß.** 
_TESTS.dirumlaut.** ---> _TESTS.dirumlauts.** 

    // Test case for ""open item 5""
    // Methods called InnerClassMethod may call each other
:::InnerClassMethod ---> :::InnerClassMethod 

    // Tests must be able to see tested classes
_TESTS.** ---> NDepCheck.**

    // Tests may use Microsoft.VisualStudio.TestTools.
_TESTS.** ---> Microsoft.VisualStudio.TestTools.**

// ------------------

    // In these tests, we ignore everything in the
    // current test class.
NDepCheck:Tests ---> **

// ------------------

    // All of system is ignored
% ()System.**

    // Classes in NDepCheck.Tests are shown separately, without the namespace
% NDepCheck.Tests.(**)

    // Classes in NDepCheck are also shown separately, but with the namespace
% (NDepCheck)

    // antlr classes are shown by namespace
% (antlr)
% (antlr.**)

    // Top level classes are shown as their class name
% -:(*)
                ");
            }
            string outFile = Path.GetTempFileName();
            //string workingDir = Path.GetTempPath();
            using (TextWriter tw = new StreamWriter(outFile)) {
                TextWriter oldOut = Console.Out;
                Console.SetOut(tw);
                string[] args = { "/v", "/x", depFile, "/f", TestAssemblyPath };
                Program.Main(args);
                Console.SetOut(oldOut);
            }
            AssertNotContains(outFile, "****");
            File.Delete(outFile);
            File.Delete(depFile);
        }

        [TestMethod]
        public void SmallGrapherTest() {
            var rs = new DependencyRuleSet(_ignoreCase, "in test");
            rs.AddProjections(ITEMTYPE, ITEMTYPE, false, "<test>", 0, "% (**)", _ignoreCase);

            var deps = new List<Dependency> {
                NewDependency("A", "a1", "a1", "A", "a2", "a2"),
                NewDependency("A", "a1", "a1", "A", "a4", "a4"),
                NewDependency("A", "a2", "a2", "A", "a3", "a3"),
                NewDependency("A", "a2", "a2", "A", "a4", "a4"),
                NewDependency("A", "a3", "a3", "A", "a4", "a4"),
                NewDependency("B", "b1", "b1", "B", "b2", "b2"),
                NewDependency("B", "b1", "b1", "B", "b4", "b4"),
                NewDependency("B", "b2", "b2", "B", "b3", "b3"),
                NewDependency("B", "b2", "b2", "B", "b4", "b4"),
                NewDependency("B", "b3", "b3", "B", "b2", "b2"),
                NewDependency("B", "b3", "b3", "B", "b4", "b4")
            };

            var nodes = new Dictionary<Item, Item>();
            var edges = new Dictionary<DependencyGrapher.FromTo, Dependency>();

            DependencyGrapher.ReduceGraph(new Options { IgnoreCase = _ignoreCase }, rs, deps, nodes, edges);

            new HideTransitiveEdges<Dependency>(new[] { "a" }).Run(edges.Values);

            using (var sw = new MemoryStream()) {
                new GenericDotRenderer().RenderToStreamForUnitTests(nodes.Values, edges.Values, sw);

                // what to assert??
            }
        }

        private static readonly ItemType ITEMTYPE = ItemType.New("TEST", new[] { "AS", "NS", "CL" },
            new string[] { null, null, null });

        private const bool _ignoreCase = false;

        private Dependency NewDependency(string usingA, string usingN, string usingC, string usedA, string usedN,
            string usedC) {
            return new Dependency(Item.New(ITEMTYPE, usingA, usingN, usingC), Item.New(ITEMTYPE, usedA, usedN, usedC),
                null, 0, 0, 0, 0);
        }

        [TestMethod]
        public void TransitiveReductionGrapherTest() {
            var rs = new DependencyRuleSet(_ignoreCase, "in test");
            rs.AddProjections(ITEMTYPE, ITEMTYPE, true, "<test>", 0, "% (**)", _ignoreCase);

            var deps = new List<Dependency> {
                NewDependency("A", "a1", "a1", "A", "a2", "a2"),
                NewDependency("A", "a1", "a1", "A", "a4", "a4"),
                NewDependency("A", "a2", "a2", "A", "a3", "a3"),
                NewDependency("A", "a2", "a2", "A", "a4", "a4"),
                NewDependency("A", "a3", "a3", "A", "a4", "a4"),
                NewDependency("B", "b1", "b1", "B", "b2", "b2"),
                NewDependency("B", "b1", "b1", "B", "b4", "b4"),
                NewDependency("B", "b2", "b2", "B", "b3", "b3"),
                NewDependency("B", "b2", "b2", "B", "b4", "b4"),
                NewDependency("B", "b3", "b3", "B", "b2", "b2"),
                NewDependency("B", "b3", "b3", "B", "b4", "b4")
            };

            var nodes = new Dictionary<Item, Item>();
            var edges = new Dictionary<DependencyGrapher.FromTo, Dependency>();

            DependencyGrapher.ReduceGraph(new Options { IgnoreCase = _ignoreCase }, rs, deps, nodes, edges);

            new HideTransitiveEdges<Dependency>(new[] { "a" }).Run(edges.Values);

            using (var s = new MemoryStream()) {
                new DotRenderer().RenderToStreamForUnitTests(nodes.Values, edges.Values, s);

                // what to assert??
            }
        }

        [TestMethod]
        public void Exit0() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    $ DOTNETCALL ---> DOTNETCALL
                  
                    NDepCheck.TestAssembly.** ---> NDepCheck.TestAssembly.**
                    NDepCheck.TestAssembly.**::NDepCheck.TestAssembly ---> System.**::mscorlib
                    NDepCheck.TestAssembly.dir1.dir2:SomeClass ---? -:NamespacelessTestClassForNDepCheck::I
                    -:* ---? System:*
                    NDepCheck.TestAssembly.** ---> NDepCheck.TestAssembly.**

                    $ DOTNETREF ---> DOTNETREF
                    * ---> *
                ");
                }
                Assert.AreEqual(0, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
                File.Delete(depFile);
            }
        }

        private static string CreateTempDotNetDepFileName() {
            return Path.GetTempFileName() + ".dll.dep";
        }

        [TestMethod]
        public void Exit0Aspects() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    $ DOTNETCALL ---> DOTNETCALL
                  
                    ::* ---> ::mscorlib

                    ** ---> **

                    // Schlägt fehlt, weil eine SpecialMethod auch auf YetAnotherMethod zugreift!
                    **::*SpecialMethod* {
                        ---> System:*
                        ---> **::*SpecialMethod*
                        ---> **::*ExtraordinaryMethod*
                    }

                    $ DOTNETREF ---> DOTNETREF
                    * ---> *
                    ");
                }
                Assert.AreEqual(0, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
                File.Delete(depFile);
            }
        }

        [TestMethod]
        public void NestedMacroTest1() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    $ DOTNETCALL ---> DOTNETCALL
                  
                    _A := NDepCheck.TestAssembly
                    _B := _A
                    _B.** ---> _B.**
                    _B.** ---> System.**
                    _B.dir1.dir2:SomeClass ---? -:NamespacelessTestClassForNDepCheck::I
                    -:* ---? System:*

                    $ DOTNETREF ---> DOTNETREF
                    * ---> *");
                }
                Assert.AreEqual(0, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
                File.Delete(depFile);
            }
        }


        [TestMethod]
        public void MacroRedefinitionTest() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    -!-> :=
                        \L ---> \R
                    =:

                    -!-> :=
                        \L ---> \R
                    =:

                    $ DOTNETCALL ---> DOTNETCALL
                    
                    _A := NDepCheck.TestAssembly
                    _B := _A
                    _B.** -!-> _B.**
                    _B.** -!-> System.**

                    _B.dir1.dir2:SomeClass ---? -:NamespacelessTestClassForNDepCheck::I
                    -:* ---? System:*

                    $ DOTNETREF ---> DOTNETREF
                    * ---> *
                ");
                }
                Assert.AreEqual(0, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
                File.Delete(depFile);
            }
        }



        [TestMethod]
        public void Exit1() {
            Assert.AreEqual(1, Program.Main(new string[] { }));
            Assert.AreEqual(1, Program.Main(new[] { "/w" }));
            Assert.AreEqual(1, Program.Main(new[] { "/v", "-v", "-i", "/i=100", "-t", "/t", "-g=someDotFile.dot" }));
        }

        [TestMethod]
        public void Exit2() {
            {
                Assert.AreEqual(2, Program.Main(new[] { "/xnonexistingfile.dep" }));
            }
        }

        [TestMethod]
        public void Exit3() {
            string depFile = CreateTempDotNetDepFileName();
            using (TextWriter tw = new StreamWriter(depFile)) {
                tw.Write(@"
                   $ DOTNETCALL ---> DOTNETCALL
                  
                   ** ---> blabla
                ");
            }
            Assert.AreEqual(3, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
            File.Delete(depFile);
        }

        [TestMethod]
        public void Exit0ForEmptyDepFile() {
            string depFile = CreateTempDotNetDepFileName();
            using (TextWriter tw = new StreamWriter(depFile)) {
                tw.Write("");
            }
            Assert.AreEqual(0, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
            File.Delete(depFile);
        }


        [TestMethod]
        public void Exit3Aspects() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"$ DOTNETCALL ---> DOTNETCALL

                    NDepCheck.TestAssembly.** ---> NDepCheck.TestAssembly.**
                    NDepCheck.TestAssembly.** ---> System.**
                    NDepCheck.TestAssembly.dir1.dir2:SomeClass ---? -:NamespacelessTestClassForNDepCheck::I
                    -:* ---? System:*

                    // Schlägt fehlt, weil eine SpecialMethod auch auf ExtraordinaryMethod zugreift!
                    :::*SpecialMethod* {
                        ---> System:*
                        ---> :::*SpecialMethod*
                    }
                    ");
                }
                Assert.AreEqual(3, Program.Main(new[] { "-w", "-x=" + depFile, TestAssemblyPath }));
                File.Delete(depFile);
            }
        }

        [TestMethod]
        public void Exit4() {
            string depFile = CreateTempDotNetDepFileName();
            using (TextWriter tw = new StreamWriter(depFile)) {
                tw.Write(@"$ DOTNETCALL ---> DOTNETCALL

                    : ---> blabla
                ");
            }
            Assert.AreEqual(4, Program.Main(new[] { "/x=" + depFile, "nonexistingfile.dll" }));
            File.Delete(depFile);
        }

        [TestMethod]
        public void Exit5() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"$ DOTNETCALL ---> DOTNETCALL

                    =---> :=    
                       : ---> blabla
                    =:
                    --->> :=    
                       ** ---> blabla
                    =:
                ");
                }
                Assert.AreEqual(5, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
                File.Delete(depFile);
            }
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"$ DOTNETCALL ---> DOTNETCALL

                    --> :=    
                       ** ---> blabla
                    =:
                ");
                }
                Assert.AreEqual(5, Program.Main(new[] { "-x=" + depFile, TestAssemblyPath }));
                File.Delete(depFile);
            }
        }

        [TestMethod]
        public void TestROption() {
            string depFile = CreateTempDotNetDepFileName();
            using (TextWriter tw = new StreamWriter(depFile)) {
                tw.Write(@"
                    $ DOTNETCALL ---> DOTNETCALL
                  
                    NDepCheck.TestAssembly.** ---> NDepCheck.TestAssembly.**
                    NDepCheck.TestAssembly.**::NDepCheck.TestAssembly ---> System.**::mscorlib
                    NDepCheck.TestAssembly.dir1.dir2:SomeClass        ---? -:NamespacelessTestClassForNDepCheck::I
                    -:*                       ---? System:**
                    NDepCheck.TestAssembly.** ---> NDepCheck.TestAssembly.**

                    $ DOTNETREF ---> DOTNETREF
                    **          ---> **

                    $ DOTNETCALL      ---> SIMPLE(Name)
                    ! System**:**     ---> .Net
                    ! Microsoft**:**  ---> .Net
                    ! (**):(**)       ---> \1#\2
                ");
            }

            string gifFile = Path.GetTempFileName() + ".gif";
            Console.WriteLine("Writing GIF to " + gifFile);
            // typeof(FullName) forces copying to known directory ...
            Assert.AreEqual(0,
                Program.Main(new[] {
                    "-x=" + depFile, TestAssemblyPath, "-r", "NDepCheck.TestRenderer.dll",
                    typeof(TestRendererForLoadFromAssembly).FullName, gifFile
                }));

            File.Delete(depFile);
            File.Delete(gifFile);
        }

        [TestMethod]
        public void TestGOption() {
            string gifFile = Path.GetTempFileName() + ".gif";
            Console.WriteLine("Writing GIF to " + gifFile);
            // The usage typeof(...).FullName forces copying of assembly to bin directory.
            Assert.AreEqual(0,
                Program.Main(new[] {
                    TestAssemblyPath, "-g", "NDepCheck.TestRenderer.dll",
                    typeof(TestRendererForLoadFromAssembly).FullName, gifFile
                }));

            File.Delete(gifFile);
        }

        [TestMethod]
        public void TestGOptionWithIXOSRenderer() {
            string gifFile = Path.GetTempFileName() + ".gif";
            Console.WriteLine(gifFile);
            Assert.AreEqual(0,
                Program.Main(new[] {
                    TestAssemblyPath, "-g", "NDepCheck.Tests.dll",
                    typeof(TestRendering.IXOSApplicationRenderer).FullName, gifFile
                }));

            File.Delete(gifFile);
        }

        [TestMethod]
        public void TestExtendedXOption() {
            string inFile = Path.GetTempFileName() + "IN.dip";
            string ndFile = Path.GetTempFileName() + "ND.nd";
            string outFile = Path.GetTempFileName() + "OUT.dip";
            using (TextWriter tw = new StreamWriter(inFile)) {
                tw.Write($@"AB A B
                AB:a:1 -> 1;0            -> AB:a:1
                AB:a:1 -> 2;1;example123 -> AB:a:2
                AB:a:2 -> 3;0            -> AB:a:1
                AB:a:2 -> 4;0            -> AB:a:2
                AB:a:2 -> 5;1            -> AB:b:
                AB:b:  -> 6;0            -> AB:a:1
                AB:b:  -> 7;0            -> AB:a:2");
            }

            using (TextWriter tw = new StreamWriter(ndFile)) {
                tw.Write($@"
                    {inFile}
                    -x {{
                        $ AB ---> AB
                        ! a:** ----> _a_:
                        ! b:** ----> _b_:
                    }}
                    -c {outFile}");
            }

            Assert.AreEqual(0, Program.Main(new[] { "-@", ndFile }));

            using (var sw = new StreamReader(outFile)) {
                var o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains("AB:_a_: -> 10;1; -> AB:_a_:"));
                Assert.IsTrue(o.Contains("AB:_a_: -> 5;1; -> AB:_b_:"));
                Assert.IsTrue(o.Contains("AB:_b_: -> 13;0; -> AB:_a_:"));
            }
        }

    }
}
