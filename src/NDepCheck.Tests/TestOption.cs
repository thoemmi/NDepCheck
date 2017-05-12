using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestOption {
        private static string TestOptions(ref int i) {
            //                   0    1    2    3    4    5    6    7    8    9    10   11   12   13
            string[] options = { "-", "a", "{", "b", "{", "c", "d", "}", "e", "{", "f", "}", "}", "g" };
            string result = Option.ExtractNextValue(options, ref i);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count(c => c == '{') == result.Count(c => c == '}'));
            return result;
        }

        [TestMethod]
        public void TestSimpleOption() {
            int i = 0;
            string result = TestOptions(ref i);
            Assert.AreEqual(1, i);
            Assert.AreEqual("a", result);
        }

        [TestMethod]
        public void TestSimpleCollectMutipleArgs() {
            int i = 8;
            string result = TestOptions(ref i);
            Assert.AreEqual(11, i);
            Assert.IsTrue(result.Contains("f"));
        }

        [TestMethod]
        public void TestNestedCollectMultipleArgs() {
            int i = 1;
            string result = TestOptions(ref i);
            Assert.AreEqual(12, i);
            Assert.IsFalse(result.Contains("a"));
            Assert.IsTrue(result.Contains("b"));
            Assert.IsTrue(result.Contains("f"));
            Assert.IsFalse(result.Contains("g"));
        }

        [TestMethod]
        public void TestCollectArgsFromFile() {
            using (var f = DisposingFile.CreateTempFileWithTail(".nd")) {
                using (var sw = new StreamWriter(f.Filename)) {
                    sw.WriteLine(@"
a b                        // 1 2
c d { e1 e2                // 3 4 5 6 7 // line starting { is split!
    f1 f2 f3               // 8
    g {                    // 9
      h1 h2                // 10
      i1 i2 i3             // 11
    }                      // 12
    j { k1 k2 } l m n o {  // 13
      p } } q              // 14
r s");                     // 15 16

                }
                string[] result = Option.CollectArgsFromFile(f.Filename);
                Assert.AreEqual(16, result.Length);
                Assert.IsTrue(result.Contains("e1"));
                Assert.IsTrue(result.Contains("    f1 f2 f3               // 8"));
                Assert.IsTrue(result.Contains("      i1 i2 i3             // 11"));
                Assert.IsTrue(result.Contains("      p } } q              // 14"));
                Assert.IsTrue(result.Contains("r"));
                Assert.IsTrue(result.Contains("s"));
            }
        }
    }
}
