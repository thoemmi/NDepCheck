using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;

namespace NDepCheck.Rendering.OLD {
    public class MissingValueException : Exception {
        public MissingValueException(string message) : base(message) { }
    }

    public abstract class Vector {
        public string Name { get; }
        protected static volatile int _now = 1;

        protected Vector(string name) {
            Name = name;
        }

        protected class DoubleCache {
            private double? _value;
            private int _cachedAt;

            public Func<double?> Cache([CanBeNull] Func<double?> f) {
                return () => {
                    if (_cachedAt != _now) {
                        _cachedAt = _now;
                        _value = f?.Invoke();
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

        public static Vector Fixed(double? x, double? y, string name = null) {
            return new FixedVector(x, y, name ?? $"[{x}|{y}]");
        }

        public static BoundedVector Bounded([NotNull] string name, double interpolateMinMax = 0.0) {
            return new BoundedVector(name, interpolateMinMax);
        }

        public PointF AsMirroredPointF() {
            return new PointF(GetX(), -GetY());
        }

        public float GetX() {
            double? x = X();
            if (!x.HasValue) {
                throw new MissingValueException($"{Name}.X has no value");
            }
            return (float)x.Value;
        }

        public float GetY() {
            double? y = Y();
            if (!y.HasValue) {
                throw new MissingValueException($"{Name}.Y has no value");
            }
            return (float)y.Value;
        }

        public static void ForceRecompute() {
            _now++;
        }

        private class FixedVector : Vector {
            private readonly double? _x;
            private readonly double? _y;

            public FixedVector(double? x, double? y, string name) : base(name) {
                _x = x;
                _y = y;
            }

            public override Func<double?> X => () => _x;
            public override Func<double?> Y => () => _y;

            public override string ToString() {
                return $"FV[{Name}]";
            }
        }

        public static Vector operator +([NotNull] Vector v1, [NotNull] Vector v2) {
            CheckNotNull(v1);
            CheckNotNull(v2);
            return new DependentVector(() => v1.X() + v2.X(), () => v1.Y() + v2.Y(), v1.Name + "+" + v2.Name);
        }

        private static void CheckNotNull(Vector v) {
            if (v == null) {
                throw new ArgumentNullException(nameof(v));
            }
        }

        public static Vector operator -([NotNull] Vector v1, [NotNull] Vector v2) {
            CheckNotNull(v1);
            CheckNotNull(v2);
            return new DependentVector(() => v1.X() - v2.X(), () => v1.Y() - v2.Y(), v1.Name + "-" + v2.Name);
        }

        public static Vector operator *([NotNull] Vector v, double d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d, v.Name + "*" + d);
        }

        public static Vector operator *(double d, [NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() * d, () => v.Y() * d, d + "*" + v.Name);
        }

        public static Vector operator /([NotNull] Vector v, double d) {
            CheckNotNull(v);
            return new DependentVector(() => v.X() / d, () => v.Y() / d, v.Name + "/" + d);
        }

        public static Vector operator -([NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => -v.X(), () => -v.Y(), "-" + v.Name);
        }

        public static Vector operator ~([NotNull] Vector v) {
            CheckNotNull(v);
            return new DependentVector(() => v.X(), () => -v.Y(), "~" + v.Name);
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
            return new DependentVector(X, () => 0, Name + ".H()");
        }

        public Vector Vertical() {
            return new DependentVector(() => 0, Y, Name + ".V()");
        }

        public Vector WithVerticalOffsetOf([NotNull]Vector other) {
            return new DependentVector(other.X, Y, "X@" + other.Name + "&Y@" + Name);
        }

        public Vector WithHorizontalHeightOf([NotNull]Vector other) {
            return new DependentVector(X, other.Y, "X@" + Name + "&Y@" + other.Name);
        }

        public DependentVector Unit() {
            return new DependentVector(() => X() / Length(), () => Y() / Length(), Name + "°");
        }

        private Func<double?> Length => () => {
                                        var x = X();
                                        var y = Y();
                                        return x.HasValue && y.HasValue ? (double?) Math.Sqrt(x.Value * x.Value + y.Value * y.Value) : null;
                                    };
    }

    public class BoundedVector : Vector {
        private readonly double _interpolateMinMax;
        private readonly DoubleCache _x = new DoubleCache();
        private readonly DoubleCache _y = new DoubleCache();

        [ItemNotNull]
        private readonly List<Vector> _lowerBounds = new List<Vector>();
        [ItemNotNull]
        private readonly List<Vector> _upperBounds = new List<Vector>();

        public BoundedVector([NotNull] string name, double interpolateMinMax = 0.0) : base(name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentNullException(nameof(name));
            }
            _interpolateMinMax = interpolateMinMax;
            X = _x.Cache(ComputeX);
            Y = _y.Cache(ComputeY);
        }

        public BoundedVector Set(double? x, double? y) {
            return Restrict(Fixed(x, y, "Restriction on " + Name));
        }

        public BoundedVector Set([NotNull] Vector v) {
            return Restrict(v, v);
        }

        public BoundedVector Restrict([CanBeNull] Func<double?> minX = null, [CanBeNull] Func<double?> minY = null,
                                      [CanBeNull] Func<double?> maxX = null, [CanBeNull] Func<double?> maxY = null) {
            Restrict(new DependentVector(minX, minY, "maxR[" + Name + "]"), new DependentVector(maxX, maxY, "maxR[" + Name + "]"));
            return this;
        }

        public BoundedVector Restrict([CanBeNull] Vector min = null, [CanBeNull] Vector max = null) {
            if (min != null) {
                _lowerBounds.Add(min);
                ForceRecompute();
            }
            if (max != null) {
                _upperBounds.Add(max);
                ForceRecompute();
            }
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
                        throw new MissingValueException($"No possible X value for BoundedVector {Name}: minX={minX} > maxX={maxX}");
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
                        throw new MissingValueException($"No possible Y value for BoundedVector {Name}: minY={minY} > maxY={maxY}");
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
            return $"BV[{Name}]";
        }
    }

    public class DependentVector : Vector {
        private readonly DoubleCache _x = new DoubleCache();
        private readonly DoubleCache _y = new DoubleCache();

        public DependentVector([CanBeNull] Func<double?> x, [CanBeNull] Func<double?> y, [CanBeNull] string name) : base(name) {
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
            return $"DV[{Name}]";
        }
    }
}