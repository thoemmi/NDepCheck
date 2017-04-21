using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using NDepCheck.Reading;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestReaders {

        [TestMethod]
        public void TestDipWithProxies() {
            string inFile = Path.GetTempFileName() + ".dip";
            using (new TempFileProvider(inFile)) {
                using (TextWriter tw = new StreamWriter(inFile)) {
                    tw.Write(@"$ NKK(Name:Key1:Key2)
                        NKK:a:keyA1:?     => ;1;0;0;src.abc|1            => NKK:?:keyA1:?
                        NKK:?:keyA1:?     => ;2;1;0;src.abc|3;example123 => NKK:a:keyA2:?
                        NKK:a:keyA2:?     => ;3;0;0;src.abc|5            => NKK:a:keyA1:KEYa1
                        NKK:?:?:KEYa2     => ;4;0;0;src.abc|7            => NKK:a:keyA2:KEYa2
                        NKK:?:keyA2:KEYa2 => ;5;1;0;src.abc|9            => NKK:b::KEYb
                        NKK:?:?:KEYb      => ;6;0;0;src.abc|11           => NKK:?:?:KEYa1
                        NKK:?::KEYb       => ;7;0;0;src.abc|13           => NKK:?:keyA2:?");
                }

                InputContext inputContext =
                    new DipReaderFactory().CreateReader(inFile, new GlobalContext(), false).ReadDependencies(0);
                Assert.IsNotNull(inputContext);
                IEnumerable<Dependency> deps = inputContext.Dependencies;
                Item[] items = deps.SelectMany(d => new[] { d.UsingItem, d.UsedItem }).Distinct().ToArray();
                Assert.AreEqual(3, items.Length);
                Assert.IsTrue(items.Contains(Item.New(ItemType.Find("NKK"), "a", "keyA1", "KEYa1")));
                Assert.IsTrue(items.Contains(Item.New(ItemType.Find("NKK"), "a", "keyA2", "KEYa2")));
                Assert.IsTrue(items.Contains(Item.New(ItemType.Find("NKK"), "b", "", "KEYb")));
            }
        }
    }
}
