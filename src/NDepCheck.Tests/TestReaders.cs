using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using NDepCheck.Reading.DipReading;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class TestReaders {
        [TestMethod]
        public void TestDipWithProxies() {
            var gc = new GlobalContext();
            WorkingGraph graph = gc.CurrentGraph;

            using (var f = DisposingFile.CreateTempFileWithTail(".dip")) {
                using (TextWriter tw = new StreamWriter(f.FileName)) {
                    tw.Write(@"$ NKK(Name:Key1:Key2)
                        NKK:a:keyA1:?     => ;1;0;0;src.abc|1            => NKK:?:keyA1:?
                        NKK:?:keyA1:?     => ;2;1;0;src.abc|3;example123 => NKK:a:keyA2:?
                        NKK:a:keyA2:?     => ;3;0;0;src.abc|5            => NKK:a:keyA1:KEYa1
                        NKK:?:?:KEYa2     => ;4;0;0;src.abc|7            => NKK:a:keyA2:KEYa2
                        NKK:?:keyA2:KEYa2 => ;5;1;0;src.abc|9            => NKK:b::KEYb
                        NKK:?:?:KEYb      => ;6;0;0;src.abc|11           => NKK:?:?:KEYa1
                        NKK:?::KEYb       => ;7;0;0;src.abc|13           => NKK:?:keyA2:?");
                }

                IEnumerable<Dependency> dependencies =
                    new DipReaderFactory().CreateReader(f.FileName, false).ReadDependencies(gc.CurrentGraph, 0, ignoreCase: false);
                Assert.IsNotNull(dependencies);
                Item[] items = dependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }).Distinct().ToArray();
                Assert.AreEqual(3, items.Length);
                Assert.IsTrue(items.Contains(graph.CreateItem(ItemType.Find("NKK"), "a", "keyA1", "KEYa1")));
                Assert.IsTrue(items.Contains(graph.CreateItem(ItemType.Find("NKK"), "a", "keyA2", "KEYa2")));
                Assert.IsTrue(items.Contains(graph.CreateItem(ItemType.Find("NKK"), "b", "", "KEYb")));
            }
        }


        [TestMethod]
        public void WriteAndReadDotNetDependencies() {
            using (var dipFile = DisposingFile.CreateTempFileWithTail(".dip")) {
                int result =
                    Program.Main(new[] {
                        MainTests.TestAssemblyPath,
                        Program.WriteDipOption.Opt, dipFile.FileName,
                        Program.DoResetOption.Opt, dipFile.FileName,
                        Program.CountDependenciesOption.Opt
                    });
                Assert.AreEqual(Program.OK_RESULT, result);
            }
        }
    }
}
