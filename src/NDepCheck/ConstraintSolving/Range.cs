using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NDepCheck.ConstraintSolving {
    public class Range {
        public static readonly Range EMPTY = new Range();

        private readonly double _lo, _hi, _eps;

        public bool IsEmpty => _lo > _hi && !AlmostEquals(_lo, _hi, _eps);

        // Only for empty range
        private Range() {
            _lo = double.PositiveInfinity;
            _hi = double.NegativeInfinity;
            _eps = 1e-300;
        }

        public Range(double lo, double hi, double eps) {
            IsEmptXy(lo, hi, eps);
            _lo = lo;
            _hi = hi;
            _eps = eps;
        }

        private static void IsEmptXy(double lo, double hi, double eps) {
            if (lo > eps && hi > eps) {
                if (lo * (1 - eps) > hi * (1 + eps)) {
                    throw new ArgumentException("lo > hi", nameof(lo));
                }
            } else if (lo < -eps && hi < -eps) {
                if (lo * (1 + eps) > hi * (1 - eps)) {
                    throw new ArgumentException("lo > hi", nameof(lo));
                }
            }
        }

        public double Lo => _lo;

        public double Hi => _hi;

        public bool IsSingleValue => !IsEmpty && AlmostEquals(_lo, _hi, _eps);

        public static bool AlmostEquals(double a, double b, double eps) {
            // ReSharper disable once CompareOfFloatsByEqualityOperator - done for performance
            return a == b || Math.Abs(a - b) < eps || Math.Abs(a - b) < eps * Math.Abs(a + b);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"[{_lo}..{_hi}]";
        }

        [Pure]
        public Range Intersect(Range r) {
            return Intersect(r.Lo, r.Hi);
        }

        [Pure]
        public Range Intersect(double lo, double hi) {
            double newLo = Math.Max(_lo, lo);
            double newHi = Math.Min(_hi, hi);
            if (AlmostEquals(newLo, newHi, _eps)) {
                return new Range(newLo, newLo, _eps);
            } else if (newLo - _eps > newHi + _eps) {
                return EMPTY;
            } else if (AlmostEquals(_lo, newLo, _eps) && AlmostEquals(_hi, newHi, _eps)) {
                return this;
            } else {
                return new Range(newLo, newHi, _eps);
            }
        }

        public static Range operator -(Range r) {
            return new Range(-r.Hi, -r.Lo, r._eps);
        }

        public bool Equals(Range other, double eps) {
            return other != null && (IsEmpty && other.IsEmpty
                                     || AlmostEquals(other.Lo, Lo, eps) && AlmostEquals(other.Hi, Hi, eps));
        }

        public override bool Equals(object obj) {
            return Equals(obj as Range, _eps);
        }

        public override int GetHashCode() {
            unchecked {
                return (_lo.GetHashCode() * 397) ^ _hi.GetHashCode();
            }
        }

        public bool IsSubsetOf(Range other) {
            return Lo >= other.Lo && Hi <= other.Hi;
        }
    }
}