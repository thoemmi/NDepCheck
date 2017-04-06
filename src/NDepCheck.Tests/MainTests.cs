// (c) HMMüller 2006...2017

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;
using NDepCheck.TestRenderer;
using NDepCheck.Transforming.Projecting;
using NDepCheck.Transforming.ViolationChecking;

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
            using (var ruleFile = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(ruleFile.Filename, false, Encoding.Default)) {
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


    // NDepCheck may use antlr and itself.
NDepCheck ---> antlr

    // NDepCheck must not use Windows Forms.
NDepCheck.** ---! System.Windows.Forms.**

_TES  := asdasdasdasdasdasd
_TESTS := NDepCheck.TestAssembly
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

//////// ------------------

//////    // All of system is ignored
//////% ()System.**

//////    // Classes in NDepCheck.Tests are shown separately, without the namespace
//////% NDepCheck.Tests.(**)

//////    // Classes in NDepCheck are also shown separately, but with the namespace
//////% (NDepCheck)

//////    // antlr classes are shown by namespace
//////% (antlr)
//////% (antlr.**)

//////    // Top level classes are shown as their class name
//////% -:(*)
                ");
                }
                string outFile = Path.GetTempFileName();
                int result;
                //string workingDir = Path.GetTempPath();
                using (TextWriter tw = new StreamWriter(outFile)) {
                    TextWriter oldOut = Console.Out;
                    Console.SetOut(tw);
                    string[] args = { Program.LogVerboseOption.Opt,
                        Program.ConfigureOption.Opt, typeof(CheckDeps).Name, "{", "-f", ruleFile.Filename, "}",
                        Program.ReadOption.Opt, TestAssemblyPath };
                    result = Program.Main(args);
                    Console.SetOut(oldOut);
                }
                AssertNotContains(outFile, "****");
                File.Delete(outFile);
                Assert.AreEqual(Program.OK_RESULT, result);
            }
        }

        ////[TestMethod]
        ////public void SmallGrapherTest() {
        ////    var rs = new DependencyRuleSet(_ignoreCase, "in test");
        ////    rs.AddProjections(ITEMTYPE, ITEMTYPE, false, "<test>", 0, "% (**)", _ignoreCase);

        ////    var deps = new List<Dependency> {
        ////        NewDependency("A", "a1", "a1", "A", "a2", "a2"),
        ////        NewDependency("A", "a1", "a1", "A", "a4", "a4"),
        ////        NewDependency("A", "a2", "a2", "A", "a3", "a3"),
        ////        NewDependency("A", "a2", "a2", "A", "a4", "a4"),
        ////        NewDependency("A", "a3", "a3", "A", "a4", "a4"),
        ////        NewDependency("B", "b1", "b1", "B", "b2", "b2"),
        ////        NewDependency("B", "b1", "b1", "B", "b4", "b4"),
        ////        NewDependency("B", "b2", "b2", "B", "b3", "b3"),
        ////        NewDependency("B", "b2", "b2", "B", "b4", "b4"),
        ////        NewDependency("B", "b3", "b3", "B", "b2", "b2"),
        ////        NewDependency("B", "b3", "b3", "B", "b4", "b4")
        ////    };

        ////    var nodes = new Dictionary<Item, Item>();
        ////    var edges = new Dictionary<FromTo, Dependency>();

        ////    DependencyGrapher.ReduceGraph(new Option { IgnoreCase = _ignoreCase }, rs, deps, nodes, edges);

        ////    new HideTransitiveEdges<Dependency>(new[] { "a" }).Run(edges.Values);

        ////    using (var sw = new MemoryStream()) {
        ////        new GenericDotRenderer().RenderToStreamForUnitTests(nodes.Values, edges.Values, sw);

        ////        // what to assert??
        ////    }
        ////}

        private static readonly ItemType ITEMTYPE = ItemType.New("TEST", new[] { "AS", "NS", "CL" },
            new string[] { null, null, null });

        private Dependency NewDependency(string usingA, string usingN, string usingC, string usedA, string usedN,
            string usedC) {
            return new Dependency(Item.New(ITEMTYPE, usingA, usingN, usingC), Item.New(ITEMTYPE, usedA, usedN, usedC),
                null, "Test", ct: 1);
        }

        ////[TestMethod]
        ////public void TransitiveReductionGrapherTest() {
        ////    var rs = new DependencyRuleSet(_ignoreCase, "in test");
        ////    rs.AddProjections(ITEMTYPE, ITEMTYPE, true, "<test>", 0, "% (**)", _ignoreCase);

        ////    var deps = new List<Dependency> {
        ////        NewDependency("A", "a1", "a1", "A", "a2", "a2"),
        ////        NewDependency("A", "a1", "a1", "A", "a4", "a4"),
        ////        NewDependency("A", "a2", "a2", "A", "a3", "a3"),
        ////        NewDependency("A", "a2", "a2", "A", "a4", "a4"),
        ////        NewDependency("A", "a3", "a3", "A", "a4", "a4"),
        ////        NewDependency("B", "b1", "b1", "B", "b2", "b2"),
        ////        NewDependency("B", "b1", "b1", "B", "b4", "b4"),
        ////        NewDependency("B", "b2", "b2", "B", "b3", "b3"),
        ////        NewDependency("B", "b2", "b2", "B", "b4", "b4"),
        ////        NewDependency("B", "b3", "b3", "B", "b2", "b2"),
        ////        NewDependency("B", "b3", "b3", "B", "b4", "b4")
        ////    };

        ////    var nodes = new Dictionary<Item, Item>();
        ////    var edges = new Dictionary<DependencyGrapher.FromTo, Dependency>();

        ////    DependencyGrapher.ReduceGraph(new Option { IgnoreCase = _ignoreCase }, rs, deps, nodes, edges);

        ////    new HideTransitiveEdges<Dependency>(new[] { "a" }).Run(edges.Values);

        ////    using (var s = new MemoryStream()) {
        ////        new DotRenderer().RenderToStreamForUnitTests(nodes.Values, edges.Values, s);

        ////        // what to assert??
        ////    }
        ////}

        [TestMethod]
        public void ExitOk() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
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
                Assert.AreEqual(Program.OK_RESULT, Program.Main(CreateCheckDepsArgs(d)));
            }
        }

        private static string[] CreateCheckDepsArgs(FileProvider d) {
            return new[] { Program.ConfigureOption.Opt, typeof(CheckDeps).Name, "{", "-f=" + d.Filename, "}", TestAssemblyPath };
        }

        private static string CreateTempDotNetDepFileName() {
            return Path.GetTempFileName() + ".dll.dep";
        }

        [TestMethod]
        public void ExitOkAspects() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
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
                Assert.AreEqual(Program.OK_RESULT, Program.Main(CreateCheckDepsArgs(d)));
            }
        }

        [TestMethod]
        public void NestedMacroTest1() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
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
                Assert.AreEqual(Program.OK_RESULT, Program.Main(CreateCheckDepsArgs(d)));
            }
        }

        [TestMethod]
        public void AnotherOlderTest() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
                    tw.Write(@"
                    $ DOTNETCALL ---> DOTNETCALL
                    
                    _A := NDepCheck.TestAssembly
                    _B := _A
                    _B.** ---> _B.**
                    _B.** ---> System.**

                    _B.dir1.dir2:SomeClass ---? -:NamespacelessTestClassForNDepCheck::I
                    -:* ---? System:*

                    $ DOTNETREF ---> DOTNETREF
                    * ---> *
                ");
                }
                Assert.AreEqual(Program.OK_RESULT, Program.Main(CreateCheckDepsArgs(d)));
            }
        }

        ////[TestMethod]
        ////public void Exit1() {
        ////    Assert.AreEqual(1, Program.Main(new string[] { }));
        ////    Assert.AreEqual(1, Program.Main(new[] { "/w" }));
        ////    ____
        ////    Assert.AreEqual(1, Program.Main(new[] { "/v", "-v", "-i", "/i=100", "-t", "/t", "-g=someDotFile.dot" }));
        ////}

        [TestMethod]
        public void ExitExceptionOnMissingDefaultSet() {
            Assert.AreEqual(Program.EXCEPTION_RESULT, Program.Main(
                new[] { Program.ConfigureOption.Opt, typeof(CheckDeps).FullName, "{", "-fnonexistingfile.dep", "}", TestAssemblyPath
                }
            ));
        }

        [TestMethod]
        public void ExitDependenciesNotOk() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                // The rules are not enough for the test assembly - we expect return result 3
                using (TextWriter tw = new StreamWriter(d.Filename)) {
                    tw.Write(@"
                   $ DOTNETCALL ---> DOTNETCALL
                  
                   ** ---> blabla
                ");
                }

                Assert.AreEqual(Program.DEPENDENCIES_NOT_OK, Program.Main(CreateCheckDepsArgs(d)));
            }
        }

        [TestMethod]
        public void ExitNoRuleGroupsFoundForEmptyDepFile() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
                    tw.Write("");
                }
                Assert.AreEqual(Program.NO_RULE_GROUPS_FOUND, Program.Main(CreateCheckDepsArgs(d)));
            }
        }


        [TestMethod]
        public void ExitDependenciesNotOkAspects() {

            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
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

                Assert.AreEqual(Program.DEPENDENCIES_NOT_OK, Program.Main(
                    new[] { Program.LogChattyOption.Opt }.Concat(CreateCheckDepsArgs(d)).ToArray()
                ));
            }

        }

        [TestMethod]
        public void ExitFileNotFound() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
                    tw.Write(@"$ DOTNETCALL ---> DOTNETCALL

                    : ---> blabla
                ");
                }
                Assert.AreEqual(Program.FILE_NOT_FOUND_RESULT, Program.Main(
                   CreateCheckDepsArgs(d).Concat(new[] { "nonexistingfile.dll" }).ToArray()
                ));
            }
        }

        [TestMethod]
        public void ExitException() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
                    tw.Write(@"$ DOTNETCALL ---> DOTNETCALL

                    // Bad - contains --->
                    =---> :=    
                       : ---> blabla
                    =:

                    // Bad - contains --->
                    --->> :=    
                       ** ---> blabla
                    =:
                ");
                }
                Assert.AreEqual(Program.EXCEPTION_RESULT, Program.Main(CreateCheckDepsArgs(d)));
            }
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
                    tw.Write(@"$ DOTNETCALL ---> DOTNETCALL

                    --> :=    
                       ** ---> blabla
                    =:
                ");
                }
                Assert.AreEqual(7, Program.Main(CreateCheckDepsArgs(d)));
            }
        }

        public class FileProvider : IDisposable {
            private bool _doDelete = true;
            public string Filename { get; }

            public FileProvider Keep {
                get { _doDelete = false; return this; }
            }

            public FileProvider(string fileName) {
                Filename = fileName;
            }

            public void Dispose() {
                if (_doDelete) {
                    File.Delete(Filename);
                }
            }
        }

        [TestMethod]
        public void TestWritePluginption() {
            using (var d = new FileProvider(CreateTempDotNetDepFileName())) {
                using (TextWriter tw = new StreamWriter(d.Filename)) {
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

                using (var e = new FileProvider(Path.GetTempFileName() + ".gif")) {
                    // typeof(FullName) forces copying to known directory ...
                    Assert.AreEqual(0,
                        Program.Main(
                            CreateCheckDepsArgs(d).Concat(
                            new[] {
                                Program.WritePluginOption.Opt,
                                "NDepCheck.TestRenderer.dll", typeof(TestRendererForLoadFromAssembly).FullName, e.Filename
                            }).ToArray()
                        ));
                }
            }
        }

        [TestMethod]
        public void TestWriteTestDataOption() {
            using (var d = new FileProvider(Path.GetTempFileName() + ".gif")) {
                // The usage typeof(...).FullName forces copying of assembly to bin directory.
                Assert.AreEqual(0,
                    Program.Main(new[] {
                        TestAssemblyPath,
                        Program.WriteTestDataOption.Opt,
                        "NDepCheck.TestRenderer.dll",
                        typeof(TestRendererForLoadFromAssembly).Name, d.Filename
                    }));
            }
        }

        [TestMethod]
        public void TestWriteTestDataOptionWithModulesAndInterfacesRenderer() {
            using (var d = new FileProvider(Path.GetTempFileName() + ".gif")) {
                Assert.AreEqual(0,
                    Program.Main(new[] {
                        TestAssemblyPath, Program.WriteTestDataOption.Opt, ".", typeof(ModulesAndInterfacesRenderer).Name,
                        $"{{ {GraphicsRenderer.WidthOption} 1500 {GraphicsRenderer.HeightOption} 1000 {GraphicsRenderer.TitleOption} TestGOption {ModulesAndInterfacesRenderer.InterfaceSelectorOption} MI }}", d.Filename
                    }));
            }
        }

        [TestMethod]
        public void TestExtendedROptionHelp() {
            Assert.AreEqual(0, Program.Main(new[] { Program.WriteHelpOption.Opt }));
        }

        [TestMethod]
        public void TestConfigureAndTransformAndWriteDip() {
            string inFile = Path.GetTempFileName() + "IN.dip";
            string ndFile = Path.GetTempFileName() + "ND.nd";
            string outFile = Path.GetTempFileName() + "OUT.dip";
            using (TextWriter tw = new StreamWriter(inFile)) {
                tw.Write(@"$ AB(A:B)
                AB:a:1 => ;1;0;0;src.abc|1            => AB:a:1
                AB:a:1 => ;2;1;0;src.abc|3;example123 => AB:a:2
                AB:a:2 => ;3;0;0;src.abc|5            => AB:a:1
                AB:a:2 => ;4;0;0;src.abc|7            => AB:a:2
                AB:a:2 => ;5;1;0;src.abc|9            => AB:b:
                AB:b:  => ;6;0;0;src.abc|11           => AB:a:1
                AB:b:  => ;7;0;0;src.abc|13           => AB:a:2");
            }

            using (TextWriter tw = new StreamWriter(ndFile)) {
                tw.Write($@"
                    {inFile}
                    {Program.ConfigureOption} {typeof(ProjectItems).Name} {{ 
                        -pl
                          $ AB(A:B) ---% AB
                          ! a:** ---% _a_:
                          ! b:** ---% _b_:
                    }}
                    {Program.TransformOption} {typeof(ProjectItems).Name} {{ }}
                    {Program.WriteDipOption} {outFile}");
            }

            Assert.AreEqual(0, Program.Main(new[] { Program.DoScriptOption.Opt, ndFile }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains("AB:_a_: => ;10;1;0;src.abc|1; => AB:_a_:"));
                Assert.IsTrue(o.Contains("AB:_a_: => ;5;1;0;src.abc|9; => AB:_b_:"));
                Assert.IsTrue(o.Contains("AB:_b_: => ;13;0;0;src.abc|11; => AB:_a_:"));
            }
        }

        [TestMethod]
        public void TestExtendedFAndTAndQOption() {
            string inFile = Path.GetTempFileName() + "IN.dip";
            string ndFile = Path.GetTempFileName() + "ND.nd";
            string outFile = Path.GetTempFileName() + "OUT.dip";
            using (TextWriter tw = new StreamWriter(inFile)) {
                tw.Write(@"$ AB(A:B)
                AB:a:1 => ;1;0;0;src.txt|1            => AB:a:1
                AB:a:1 => ;2;1;0;src.txt|2;example123 => AB:a:2
                AB:a:2 => ;3;0;0;src.txt|3            => AB:a:1
                AB:a:2 => ;4;0;0;src.txt|4            => AB:a:2
                AB:a:2 => ;5;1;0;src.txt|5            => AB:b:
                AB:b:  => ;6;0;0;src.txt|6            => AB:a:1
                AB:b:  => ;7;0;0;src.txt|7            => AB:a:2");
            }

            using (TextWriter tw = new StreamWriter(ndFile)) {
                tw.Write($@"
                    {inFile}
                    {Program.ConfigurePluginOption} . {typeof(ProjectItems).Name} {{ 
                        -pl
                          $ AB(A:B) ---% AB
                          ! a:** ---% _a_:
                          ! b:** ---% _b_:
                    }}
                    {Program.TransformPluginOption} . {typeof(ProjectItems).FullName} {{ }}
                    {Program.WritePluginOption} . {typeof(DipWriter).FullName} {{ {DipWriter.NoExampleInfoOption} }} {outFile}");
            }

            Assert.AreEqual(0, Program.Main(new[] { Program.DoScriptOption.Opt, ndFile }));

            using (var sw = new StreamReader(outFile)) {
                string o = sw.ReadToEnd();

                Assert.IsTrue(o.Contains("AB:_a_: => ;10;1;0;src.txt|1; => AB:_a_:"));
                Assert.IsTrue(o.Contains("AB:_a_: => ;5;1;0;src.txt|5; => AB:_b_:"));
                Assert.IsTrue(o.Contains("AB:_b_: => ;13;0;0;src.txt|6; => AB:_a_:"));
            }
        }

        [TestMethod]
        public void TestHelpForAllReaders() {
            Assert.AreEqual(0, Program.Main(new[] { Program.ReadPluginHelpOption.Opt, "." }));
            Assert.AreEqual(0, Program.Main(new[] { Program.ReadHelpOption.Opt }));
        }

        [TestMethod]
        public void TestHelpForAllTransformers() {
            Assert.AreEqual(0, Program.Main(new[] { Program.TransformPluginHelpOption.Opt, "." }));
            Assert.AreEqual(0, Program.Main(new[] { Program.TransformHelpOption.Opt }));
        }

        [TestMethod]
        public void TestHelpForAllRenderers() {
            Assert.AreEqual(0, Program.Main(new[] { Program.WritePluginHelpOption.Opt, "." }));
            Assert.AreEqual(0, Program.Main(new[] { Program.WritePluginHelpOption.Opt }));
            Assert.AreEqual(0, Program.Main(new[] { Program.WriteHelpOption.Opt }));
        }
    }
}
