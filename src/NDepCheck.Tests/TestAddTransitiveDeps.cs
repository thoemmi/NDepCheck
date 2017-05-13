using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Reading.DipReading;
using NDepCheck.Transforming.DependencyCreating;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestAddTransitiveDeps {
        [TestMethod]
        public void TestAddTransitiveBasic() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(AddTransitiveDeps).Name, "{",
                    AddTransitiveDeps.FromItemsOption.Opt, "S*",
                    AddTransitiveDeps.ToItemsOption.Opt, "T*",
                "}",
                Program.WriteDipOption.Opt, outFile
            }));

            IEnumerable<Dependency> result = new DipReaderFactory().CreateReader(outFile, false).ReadDependencies(0, ignoreCase: false);
            Assert.IsNotNull(result);

            IEnumerable<Dependency> s2t = result.Where(d => d.UsingItem.Name.StartsWith("S") && d.UsedItem.Name.StartsWith("T"));
            Assert.AreEqual(8, s2t.Count());
        }

        [TestMethod]
        public void TestAddTransitiveIdempotent() {
            string outFile = Path.GetTempFileName() + "OUT.dip";

            Assert.AreEqual(0, Program.Main(new[] {
                Program.TransformTestDataOption.Opt, ".", typeof(AddTransitiveDeps).Name, "{",
                    AddTransitiveDeps.FromItemsOption.Opt, "S*",
                    AddTransitiveDeps.ToItemsOption.Opt, "T*",
                    AddTransitiveDeps.AddMarkerOption.Opt, "D",
                    AddTransitiveDeps.IdempotentOption.Opt,
                "}",
                Program.WriteDipOption.Opt, outFile
            }));

            IEnumerable<Dependency> result = new DipReaderFactory().CreateReader(outFile, false).ReadDependencies(0, ignoreCase: false);
            Assert.IsNotNull(result);

            IEnumerable<Dependency> s2t = result.Where(d => d.UsingItem.Name.StartsWith("S") && d.UsedItem.Name.StartsWith("T"));
            Assert.AreEqual(7, s2t.Count());
        }
    }
}
