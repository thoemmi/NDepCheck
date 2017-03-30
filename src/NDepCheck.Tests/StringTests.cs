using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.Projecting;

namespace NDepCheck.Tests {
    [TestClass]
    public class StringTests {
        [TestMethod]
        public void TestExpandHexChars() {
            Assert.AreEqual(" ", Projection.ExpandHexChars("%20"));
            Assert.AreEqual("1 2", Projection.ExpandHexChars("1%202"));
            Assert.AreEqual("637839:3b<3d>3f", Projection.ExpandHexChars("%3637%3839%3a3b%3C3d%3e3f"));
        }
    }
}
