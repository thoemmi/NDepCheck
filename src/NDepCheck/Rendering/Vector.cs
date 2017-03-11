using System;
using System.Drawing;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public abstract class Vector {
        protected static volatile int _now = 1;

        public abstract Func<double?> X {
            get;
        }
        public abstract Func<double?> Y {
            get;
        }

        internal double? DebugX => X();

        internal double? DebugY => Y();

        public static Vector Fixed(double x, double y) {
            return new FixedVector(x, y);
        }

        public static Vector Variable(string name, double? x, double? y) {
            return new VariableVector(name, x, y);
        }

        public PointF AsPointF() {
            return new PointF(GetX(), GetY());
        }

        public float GetX() {
            double? x = X();
            if (!x.HasValue) {
                throw new InvalidOperationException("X has no value");
            }
            return (float)x.Value;
        }

        public float GetY() {
            double? y = Y();
            if (!y.HasValue) {
                throw new InvalidOperationException("Y has no value");
            }
            return (float)y.Value;
        }

        public static void ForceRecompute() {
            _now++;
        }

        private class FixedVector : Vector {
            private readonly double _x;
            private readonly double _y;

            public FixedVector(double x, double y) {
                _x = x;
                _y = y;
            }

            public override Func<double?> X => () => _x;
            public override Func<double?> Y => () => _y;

            public override string ToString() {
                return $"FV[{_x},{_y}]";
            }
        }

        public static Vector operator +([NotNull] Vector v1, [NotNull] Vector v2) {
            CheckNotNull(v1);
            CheckNotNull(v2);
            return new DependentVector(() => v1.X() + v2.X(), () => v1.Y() + v2.Y());
        }

        private static void CheckNotNull(Vector v) {
            if (v == null) {
                throw new ArgumentNullException(nameof(v));
            }
        }

        public static Vector operator -([NotNull] Vector v1, [NotNull] Vector v2) {
            CheckNotNull(v1);
            CheckNotNull(v2);
            return new DependentVector(() => v1.X() - v2.X(), () => v1.Y() - v2.Y());
        }

        public static Vector operator *([NotNull] Vector v, double d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d);
        }

        public static Vector operator *(double d, [NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d);
        }

        public static Vector operator /([NotNull] Vector v, double d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() / d, () => v.Y() / d);
        }

        public static Vector operator ~([NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => v.X(), () => -v.Y());
        }

        public double To(Vector other) {
            return Math.Sqrt(DistanceSquared(this, other));
        }

        public static double DistanceSquared(Vector v1, Vector v2) {
            double dx = v2.GetX() - v1.GetX();
            double dy = v2.GetY() - v1.GetY();
            return dx * dx + dy * dy;
        }
    }

    public class VariableVector : Vector {
        public string Name {
            get;
        }
        private double? _x;
        private double? _y;

        public VariableVector(string name, double? x = null, double? y = null) {
            Name = name;
            Set(x, y);
        }

        public void Set(double? x, double? y) {
            SetX(x);
            SetY(y);
        }

        public void SetX(double? x) {
            _x = x;
            ForceRecompute();
        }

        public void SetY(double? y) {
            _y = y;
            ForceRecompute();
        }

        public override Func<double?> X => () => _x;
        public override Func<double?> Y => () => _y;

        public override string ToString() {
            return $"VV[{Name}]";
        }
    }

    public class DependentVector : Vector {
        private class DoubleCache {
            private double? _value;
            private int _cachedAt;

            public Func<double?> Cache(Func<double?> f) {
                return () => {
                    if (_cachedAt != _now) {
                        _cachedAt = _now;
                        _value = f();
                    }
                    return _value;
                };
            }
        }

        private readonly DoubleCache _x = new DoubleCache();
        private readonly DoubleCache _y = new DoubleCache();

        public DependentVector(Func<double?> x, Func<double?> y) {
            X = _x.Cache(x);
            Y = _y.Cache(y);
        }

        public override Func<double?> X {
            get;
        }

        public override Func<double?> Y {
            get;
        }

        public override string ToString() {
            return "DV[]";
        }
    }
}