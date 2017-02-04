// (c) HMMüller 2006...2015

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NDepCheck.Tests {
    [TestClass]
    public class AttributeTests {
        // ReSharper disable once AssignNullToNotNullAttribute - ok in this test
        private static readonly string TestAssemblyPath = Path.Combine(Path.GetDirectoryName(typeof(MainTests).Assembly.Location), "NDepCheck.TestAssemblyForAttributes.dll");

        [TestMethod]
        public void Exit0() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    NDepCheck.TestAssembly.For.Attributes.** ---> NDepCheck.TestAssembly.For.Attributes.**
                    NDepCheck.TestAssembly.For.Attributes ---> System.**

                    $ MY_ITEM_TYPE(NAMESPACE:CLASS:ASSEMBLY.NAME:ASSEMBLY.VERSION:ASSEMBLY.CULTURE:METHOD:CUSTOM.SectionA:CUSTOM.SectionB:CUSTOM.SectionC) ---> DOTNETCALL                     
                    // VORERST ------------------------------                    
                    : ---> :


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
    }
}
