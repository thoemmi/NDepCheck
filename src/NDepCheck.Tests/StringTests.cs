using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class StringTests {
        [TestMethod]
        public void TestExpandHexChars() {
            Assert.AreEqual(" ", GlobalContext.ExpandHexChars("%20"));
            Assert.AreEqual("1 2", GlobalContext.ExpandHexChars("1%202"));
            Assert.AreEqual("637839:3b<3d>3f", GlobalContext.ExpandHexChars("%3637%3839%3a3b%3C3d%3e3f"));
        }
    }
}
