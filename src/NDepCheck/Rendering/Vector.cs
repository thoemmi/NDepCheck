using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public abstract class Vector {
        protected static volatile int _now = 1;

        protected class DoubleCache {
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

        public abstract Func<double?> X {
            get;
        }
        public abstract Func<double?> Y {
            get;
        }

        internal double? DebugX => X();

        internal double? DebugY => Y();

        public static Vector Fixed(double? x, double? y) {
            return new FixedVector(x, y);
        }

        public static BoundedVector Bounded(string name, double interpolateMinMax = 0.0) {
            return new BoundedVector(name, interpolateMinMax);
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
            private readonly double? _x;
            private readonly double? _y;

            public FixedVector(double? x, double? y) {
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

        public static Vector operator -([NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => -v.X(), () => -v.Y());
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

        public Vector Horizontal() {
            return new DependentVector(X, () => 0);
        }

        public Vector Vertical() {
            return new DependentVector(() => 0, Y);
        }
    }

    public class BoundedVector : Vector {
        private readonly double _interpolateMinMax;
        private readonly DoubleCache _x = new DoubleCache();
        private readonly DoubleCache _y = new DoubleCache();

        [ItemNotNull]
        private readonly List<Vector> _lowerBounds = new List<Vector>();
        [ItemNotNull]
        private readonly List<Vector> _upperBounds = new List<Vector>();

        public string Name {
            get;
        }

        public BoundedVector(string name, double interpolateMinMax = 0.0) {
            _interpolateMinMax = interpolateMinMax;
            Name = name;
            X = _x.Cache(ComputeX);
            Y = _y.Cache(ComputeY);
        }

        public BoundedVector Set(double? x, double? y) {
            return Restrict(x, y, x, y);
        }

        public BoundedVector Set([NotNull] Vector v) {
            return Restrict(v, v);
        }

        public BoundedVector Restrict(double? minX, double? minY, double? maxX = null, double? maxY = null) {
            Restrict(Fixed(minX, minY), Fixed(maxX, maxY));
            return this;
        }

        public BoundedVector Restrict([NotNull] Vector min, [CanBeNull] Vector max = null) {
            _lowerBounds.Add(min);
            if (max != null) {
                _upperBounds.Add(max);
            }
            ForceRecompute();
            return this;
        }

        private double? ComputeX() {
            double? minX = null;
            foreach (var b in _lowerBounds) {
                double? x = b.X();
                if (x.HasValue) {
                    minX = minX.HasValue ? Math.Max(x.Value, minX.Value) : x;
                }
            }
            double? maxX = null;
            foreach (var b in _upperBounds) {
                double? x = b.X();
                if (x.HasValue) {
                    maxX = maxX.HasValue ? Math.Min(x.Value, maxX.Value) : x;
                }
            }
            if (minX.HasValue) {
                if (maxX.HasValue) {
                    if (minX > maxX) {
                        throw new InvalidOperationException($"No possible X value for BoundedVector {Name}: minX={minX} > maxX={maxX}");
                    }
                    return minX * (1 - _interpolateMinMax) + maxX * _interpolateMinMax;
                } else {
                    return minX;
                }
            } else {
                return maxX;
            }
        }

        private double? ComputeY() {
            double? minY = null;
            foreach (var b in _lowerBounds) {
                double? y = b.Y();
                if (y.HasValue) {
                    minY = minY.HasValue ? Math.Max(y.Value, minY.Value) : y;
                }
            }
            double? maxY = null;
            foreach (var b in _upperBounds) {
                double? y = b.Y();
                if (y.HasValue) {
                    maxY = maxY.HasValue ? Math.Min(y.Value, maxY.Value) : y;
                }
            }
            if (minY.HasValue) {
                if (maxY.HasValue) {
                    if (minY > maxY) {
                        throw new InvalidOperationException($"No possible Y value for BoundedVector {Name}: minY={minY} > maxY={maxY}");
                    }
                    return minY * (1 - _interpolateMinMax) + maxY * _interpolateMinMax;
                } else {
                    return minY;
                }
            } else {
                return maxY;
            }
        }

        public override Func<double?> X {
            get;
        }

        public override Func<double?> Y {
            get;
        }

        public override string ToString() {
            return $"VV[{Name}]";
        }
    }

    public class DependentVector : Vector {
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