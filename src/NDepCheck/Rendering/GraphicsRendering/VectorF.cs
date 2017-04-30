using System;
using System.Drawing;
using NDepCheck.ConstraintSolving;

namespace NDepCheck.Rendering.GraphicsRendering {
    public class VectorF {
        private readonly float _x, _y;

        public VectorF(float x, float y, string definition) {
            _x = x;
            _y = y;
            Definition = definition;
        }

        public static VectorF operator +(VectorF v1, VectorF v2) {
            return new VectorF(v1._x + v2._x, v1._y + v2._y, $"({v1.Definition})+({v2.Definition})");
        }

        public static VectorF operator -(VectorF v1, VectorF v2) {
            return new VectorF(v1._x - v2._x, v1._y - v2._y, $"({v1.Definition})-({v2.Definition})");
        }

        public static VectorF operator *(VectorF v, double d) {
            return new VectorF(v._x * (float)d, v._y * (float)d, $"({v.Definition})*{d}");
        }

        public static VectorF operator *(double d, VectorF v) {
            return v * d;
        }

        public static VectorF operator /(VectorF v, double d) {
            return new VectorF(v._x / (float)d, v._y / (float)d, $"({v.Definition})/{d}");
        }

        public static VectorF operator -(VectorF v) {
            return new VectorF(-v._x, -v._y, $"-({v.Definition})");
        }

        public static VectorF operator ~(VectorF v) {
            return new VectorF(v._x, -v._y, $"~({v.Definition})");
        }

        public string Definition { get; private set; }

        public VectorF Horizontal() {
            return new VectorF(_x, 0, $"({Definition}).H");
        }

        public VectorF Vertical() {
            return new VectorF(0, _y, $"({Definition}).V");
        }

        public float GetX() {
            return _x;
        }

        public float GetY() {
            return _y;
        }

        public PointF AsMirroredPointF() {
            return new PointF(GetX(), -GetY());
        }

        public VectorF Unit() {
            return new VectorF(_x / Length(), _y / Length(), $"({Definition}).U");
        }

        public float Length() => (float)Math.Sqrt(_x * _x + _y * _y);

        internal VectorF Suffixed(string s) {
            Definition += s;
            return this;
        }
    }

    public static class VariableVectorExtensions {
        public static VectorF AsVectorF(this VariableVector v) {
            return new VectorF((float)v.X.GetValue(), (float)v.Y.GetValue(), v.X.Definition + "," + v.Y.Definition);
        }
    }
}