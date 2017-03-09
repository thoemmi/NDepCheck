// (c) HMMüller 2006...2015

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NDepCheck.Tests {
    [TestClass]
    public class AttributeTests {
        private static readonly string _testAssemblyPath = Path.Combine(Path.GetDirectoryName(typeof(MainTests).Assembly.Location ?? "IGNORE"), "NDepCheck.TestAssemblyForAttributes.dll");

        [TestMethod]
        public void Exit0() {
            {
                string depFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(depFile)) {
                    tw.Write(@"
                    $ DOTNETCALL ---> DOTNETCALL

                    NDepCheck.TestAssembly.For.Attributes.** ---> NDepCheck.TestAssembly.For.Attributes.**
                    NDepCheck.TestAssembly.For.Attributes ---> System.**

                    $ MY_ITEM_TYPE(NAMESPACE:CLASS:ASSEMBLY.NAME:ASSEMBLY.VERSION:ASSEMBLY.CULTURE:METHOD:CUSTOM.SectionA:CUSTOM.SectionB:CUSTOM.SectionC) ---> DOTNETCALL                     
                    // VORERST ------------------------------                    
                    : ---> :

                    $ MY_ITEM_TYPE(NAMESPACE:CLASS:ASSEMBLY.NAME:ASSEMBLY.VERSION:ASSEMBLY.CULTURE:METHOD:CUSTOM.SectionA:CUSTOM.SectionB:CUSTOM.SectionC) ---> MY_ITEM_TYPE
                    // VORERST ------------------------------                    
                    : ---> :

                    $ DOTNETREF ---> DOTNETREF
                    * ---> *

                ");
                }
                Assert.AreEqual(0, Program.Main(new[] { "-x=" + depFile, _testAssemblyPath }));
                File.Delete(depFile);
            }
        }

        private static string CreateTempDotNetDepFileName() {
            return Path.GetTempFileName() + ".dll.dep";
        }
    }
}
