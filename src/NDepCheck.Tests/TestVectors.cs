using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestVectors {
        [TestMethod]
        public void TestCaching() {
            var vv = new VariableVector("a");
            int[] recomputeDv = { 0, 0 };
            var dv1 = new DependentVector(() => {
                recomputeDv[0]++;
                return vv.X() + 1;
            }, () => {
                recomputeDv[1]++;
                return vv.Y() + 1;
            });
            var dv2 = new DependentVector(() => dv1.X() + 2, () => dv1.Y() + 2);
            var dv3 = new DependentVector(() => dv1.X() + 3, () => dv1.Y() + 3);

            vv.Set(10, 10);

            Assert.AreEqual(10 + 1 + 2, dv2.X());
            Assert.AreEqual(10 + 1 + 3, dv3.X());
            Assert.AreEqual(1, recomputeDv[0]);
            Assert.AreEqual(0, recomputeDv[1]);

            Assert.AreEqual(10 + 1 + 2, dv2.X());
            Assert.AreEqual(10 + 1 + 3, dv3.X());
            Assert.AreEqual(1, recomputeDv[0]);
            Assert.AreEqual(0, recomputeDv[1]);

            Assert.AreEqual(10 + 1 + 2, dv2.Y());
            Assert.AreEqual(10 + 1 + 3, dv3.Y());
            Assert.AreEqual(1, recomputeDv[0]);
            Assert.AreEqual(1, recomputeDv[1]);

            for (int i = 0; i < 3; i++) {
                Assert.AreEqual(10 + 1 + 2, dv2.X());
                Assert.AreEqual(10 + 1 + 2, dv2.Y());
                Assert.AreEqual(10 + 1 + 3, dv3.X());
                Assert.AreEqual(10 + 1 + 3, dv3.Y());
                Assert.AreEqual(1, recomputeDv[0]);
                Assert.AreEqual(1, recomputeDv[1]);
            }

            vv.Set(20, null);

            for (int i = 0; i < 3; i++) {
                Assert.AreEqual(20 + 1 + 2, dv2.X());
                Assert.AreEqual(20 + 1 + 3, dv3.X());
                Assert.IsNull(dv2.Y());
                Assert.IsNull(dv3.Y());
                Assert.AreEqual(2, recomputeDv[0]);
                Assert.AreEqual(2, recomputeDv[1]);
            }

            Vector.ForceRecompute();

            for (int i = 0; i < 3; i++) {
                Assert.AreEqual(20 + 1 + 2, dv2.X());
                Assert.AreEqual(20 + 1 + 3, dv3.X());
                Assert.IsNull(dv2.Y());
                Assert.IsNull(dv3.Y());
                Assert.AreEqual(3, recomputeDv[0]);
                Assert.AreEqual(3, recomputeDv[1]);
            }
        }

        private void ResetCounts(int[] recomputeDv) {
            for (int i = 0; i < recomputeDv.Length; i++) {
                recomputeDv[i] = 0;
            }
        }
    }
}
