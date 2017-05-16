using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.ConstraintSolving;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestConstraints {
        private const double EPS = 1E-40;

        private static readonly Action _noAbortCheck = () => { };

        [TestMethod]
        public void TestRangeOperators() {
            var r1 = new Range(1, 5, EPS);
            var r2 = new Range(double.NegativeInfinity, 3, EPS);
            var r3 = new Range(double.NegativeInfinity, double.PositiveInfinity, EPS);
            var r4 = new Range(4, double.PositiveInfinity, EPS);

            var minusr1 = -r1;
            var minusr2 = -r2;
            var minusr3 = -r3;
            var minusr4 = -r4;

            Assert.AreEqual(new Range(-5, -1, EPS), minusr1);
            Assert.AreEqual(new Range(-3, double.PositiveInfinity, EPS), minusr2);
            Assert.AreEqual(r3, minusr3);
            Assert.AreEqual(new Range(double.NegativeInfinity, -4, EPS), minusr4);

            Assert.AreEqual(new Range(1, 3, EPS), r1.Intersect(r2));
            Assert.AreEqual(r1, r1.Intersect(r3));
            Assert.AreEqual(new Range(4, 5, EPS), r1.Intersect(r4));
        }

        [TestMethod]
        public void TestRangeIsSubsetOf() {
            var r1 = new Range(1, 5, EPS);
            var r2 = new Range(double.NegativeInfinity, 3, EPS);
            var r3 = new Range(double.NegativeInfinity, double.PositiveInfinity, EPS);
            var r4 = new Range(4, double.PositiveInfinity, EPS);
            var r5 = new Range(2, 3, EPS);

            Assert.IsTrue(r1.IsSubsetOf(r1));
            Assert.IsFalse(r1.IsSubsetOf(r2));
            Assert.IsTrue(r1.IsSubsetOf(r3));
            Assert.IsFalse(r1.IsSubsetOf(r4));
            Assert.IsFalse(r1.IsSubsetOf(r5));

            Assert.IsFalse(r2.IsSubsetOf(r1));
            Assert.IsTrue(r2.IsSubsetOf(r2));
            Assert.IsTrue(r2.IsSubsetOf(r3));
            Assert.IsFalse(r2.IsSubsetOf(r4));
            Assert.IsFalse(r2.IsSubsetOf(r5));

            Assert.IsFalse(r3.IsSubsetOf(r1));
            Assert.IsFalse(r3.IsSubsetOf(r2));
            Assert.IsTrue(r3.IsSubsetOf(r3));
            Assert.IsFalse(r3.IsSubsetOf(r4));
            Assert.IsFalse(r3.IsSubsetOf(r5));

            Assert.IsFalse(r4.IsSubsetOf(r1));
            Assert.IsFalse(r4.IsSubsetOf(r2));
            Assert.IsTrue(r4.IsSubsetOf(r3));
            Assert.IsTrue(r4.IsSubsetOf(r4));
            Assert.IsFalse(r4.IsSubsetOf(r5));

            Assert.IsTrue(r5.IsSubsetOf(r1));
            Assert.IsTrue(r5.IsSubsetOf(r2));
            Assert.IsTrue(r5.IsSubsetOf(r3));
            Assert.IsFalse(r5.IsSubsetOf(r4));
            Assert.IsTrue(r5.IsSubsetOf(r5));
        }

        [TestMethod]
        public void TestVariableMinimization() {
            var solver = new SimpleConstraintSolver();
            var a = solver.CreateVariable("a");
            var b = solver.CreateVariable("b");
            var c1 = a + b;
            var c2 = a + b;
            Assert.AreSame(c1, c2);
        }

        [TestMethod]
        public void TestConstraintMinimization() {
            var solver = new SimpleConstraintSolver();
            var a = solver.CreateVariable("a");
            var b = solver.CreateVariable("b");
            var c1 = a + b;
            var c2 = a + b;

            Assert.AreSame(c1, c2);

            solver.MarkSubsumingConstraints();

            // There is only one Sum0Constraint at all variables
            Assert.AreEqual(1, a.ActiveConstraints.Count());
            Assert.AreEqual(1, b.ActiveConstraints.Count());
            Assert.AreEqual(1, c1.ActiveConstraints.Count());

        }

        [TestMethod]
        public void TestSimpleSum() {
            var solver = new SimpleConstraintSolver();
            var a = solver.CreateVariable("a");
            var b = solver.CreateVariable("b");
            var c = a + b;

            a.RestrictRange(2, 2);
            b.RestrictRange(3, 3);
            solver.Solve(_noAbortCheck );
            Assert.AreEqual(5, c.Value.Lo);
            Assert.AreEqual(5, c.Value.Hi);
        }

        [TestMethod]
        public void TestSimpleSumDownwards() {
            var solver = new SimpleConstraintSolver();
            var a = solver.CreateVariable("a");
            var b = solver.CreateVariable("b");
            var c = a + b;

            a.RestrictRange(2, 2);
            c.RestrictRange(5, 5);
            solver.Solve(_noAbortCheck );
            Assert.AreEqual(new Range(3, 3, EPS), b.Value);
        }

        [TestMethod]
        public void TestMinus() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                var c = a - b;

                a.RestrictRange(2, 3);
                b.RestrictRange(5, 6);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(-4, -2, EPS), c.Value);
            }
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                var c = a - b;

                a.RestrictRange(2, 3);
                c.RestrictRange(5, 6);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(-4, -2, EPS), b.Value);
            }
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                var c = a - b;

                b.RestrictRange(2, 3);
                c.RestrictRange(5, 6);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(7, 9, EPS), a.Value);
            }
        }

        [TestMethod]
        public void TestTimes() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = a * 3;

                a.RestrictRange(2, 3);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(6, 9, EPS), b.Value);
            }
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = a * 3;

                b.RestrictRange(2, 3);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(2 / 3.0, 1, EPS), a.Value);
            }
        }

        [TestMethod]
        public void TestRangeConstraint() {
            {
                var solver = new SimpleConstraintSolver();
                NumericVariable a = solver.CreateVariable("a");

                var r = new Range(3, 4, EPS);
                RangeConstraint.CreateRangeConstraint(a, r);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(r, a.Value);
            }
        }

        [TestMethod]
        public void TestEqualityConstraint() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                EqualityConstraint.CreateEqualityConstraint(a, b);

                var r = new Range(3, 4, EPS);
                a.RestrictRange(r);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(r, b.Value);
            }
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                EqualityConstraint.CreateEqualityConstraint(a, b);

                var r = new Range(3, 4, EPS);
                b.RestrictRange(r);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(r, a.Value);
            }
        }

        [TestMethod]
        public void TestIsInverseConstraint() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                IsInverseConstraint.CreateIsInverseConstraint(a, b);

                var r = new Range(3, 4, EPS);
                a.RestrictRange(r);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(-r, b.Value);
            }
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                IsInverseConstraint.CreateIsInverseConstraint(a, b);

                var r = new Range(3, 4, EPS);
                b.RestrictRange(r);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(-r, a.Value);
            }
        }

        [TestMethod]
        public void TestAtLeastConstraint() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                AtLeastConstraint.CreateAtLeastConstraint(a, b);

                a.RestrictRange(new Range(3, 4, EPS));
                solver.Solve(_noAbortCheck );
                // (3..4) >= (x..y) means that y is at most 4
                Assert.AreEqual(new Range(double.NegativeInfinity, 4, EPS), b.Value);
            }
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                AtLeastConstraint.CreateAtLeastConstraint(a, b);

                b.RestrictRange(new Range(3, 4, EPS));
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(3, double.PositiveInfinity, EPS), a.Value);
            }
        }

        [TestMethod]
        public void TestProportionalConstraint() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                ProportionalConstraint.CreateProportionalConstraint(5, a, b);

                b.RestrictRange(new Range(30, 40, EPS));
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(6, 8, EPS), a.Value);
            }
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                ProportionalConstraint.CreateProportionalConstraint(5, a, b);

                a.RestrictRange(new Range(30, 40, EPS));
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(150, 200, EPS), b.Value);
            }
        }

        [TestMethod]
        public void TestSumIs0Constraint() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                var c = solver.CreateVariable("c");
                SumIs0Constraint.CreateSumIs0Constraint(a, b, c);

                a.RestrictRange(new Range(30, 50, EPS));
                b.RestrictRange(new Range(10, double.PositiveInfinity, EPS));
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(double.NegativeInfinity, -40, EPS), c.Value);
            }
        }

        private void CartesianToPolar(NumericVariable[] input, NumericVariable[] output) {
            double x = input[0].GetValue();
            double y = input[1].GetValue();
            NumericVariable r = output[0];
            NumericVariable phi = output[1];

            r.Set(Math.Sqrt(x * x + y * y));
            phi.Set(Math.Atan2(y, x));
        }

        [TestMethod]
        public void TestUnidirectionalComputationConstraint() {
            {
                var solver = new SimpleConstraintSolver();
                var x = solver.CreateVariable("x");
                var y = solver.CreateVariable("y");
                var r = solver.CreateVariable("r");
                var phi = solver.CreateVariable("phi");
                UnidirectionalComputationConstraint.CreateUnidirectionalComputationConstraint(new[] { x, y }, new[] { r, phi }, CartesianToPolar);

                var rx = new Range(20, 40, EPS);
                x.RestrictRange(rx);
                var ry = new Range(30, 50, EPS);
                y.RestrictRange(ry);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(new Range(50, 50, EPS), r.Value);
            }
            {
                //var solver = new SimpleConstraintSolver();
                //var x = solver.Create("a");
                //var y = solver.Create("b");
                //var r = solver.Create("r");
                //var phi = solver.Create("phi");
                //new UnidirectionalComputationConstraint(new[] { x, y }, new[] { r, phi }, CartesianToPolar);
            }
        }

        private void OneMore(NumericVariable[] input, NumericVariable[] output) {
            output[0].Set(input[0].GetValue() + 1);
        }

        [TestMethod]
        public void TestConstraintPropagation() {
            {
                var solver = new SimpleConstraintSolver();
                var a = solver.CreateVariable("a");
                var b = solver.CreateVariable("b");
                var c = solver.CreateVariable("c");
                var d = solver.CreateVariable("d");
                UnidirectionalComputationConstraint.CreateUnidirectionalComputationConstraint(new[] { a }, new[] { b }, OneMore);
                UnidirectionalComputationConstraint.CreateUnidirectionalComputationConstraint(new[] { b }, new[] { c }, OneMore);
                UnidirectionalComputationConstraint.CreateUnidirectionalComputationConstraint(new[] { c }, new[] { d }, OneMore);
                a.Set(10);
                solver.Solve(_noAbortCheck );
                Assert.AreEqual(10, a.GetValue());
                Assert.AreEqual(11, b.GetValue());
                Assert.AreEqual(12, c.GetValue());
                Assert.AreEqual(13, d.GetValue());
            }
        }
    }
}