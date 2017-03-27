using JetBrains.Annotations;

namespace NDepCheck.ConstraintSolving {
    public class VariableVector {
        private readonly NumericVariable _x, _y;

        public VariableVector(string name, NumericVariable x, NumericVariable y) {
            Name = name;
            _x = x;
            _y = y;
        }

        public VariableVector(string name, SimpleConstraintSolver solver, double? x = null, double? y = null, float interpolate = 0.5f)
            : this(name, solver.CreateVariable(name + ".X", x, x, interpolate), solver.CreateVariable(name + ".Y", y, y, interpolate)) {
        }

        public string Name { get; private set; }

        public NumericVariable X => _x;

        public NumericVariable Y => _y;

        public VariableVector Restrict([CanBeNull] VariableVector lowerBound) {
            if (lowerBound != null) {
                AtLeastConstraint.CreateAtLeastConstraint(_x, lowerBound._x);
                AtLeastConstraint.CreateAtLeastConstraint(_y, lowerBound._y);
            }
            return this;
        }

        public VariableVector Restrict([CanBeNull] VariableVector lowerBound, [CanBeNull] VariableVector upperBound) {
            if (upperBound != null) {
                AtLeastConstraint.CreateAtLeastConstraint(upperBound._x, _x);
                AtLeastConstraint.CreateAtLeastConstraint(upperBound._y, _y);
            }
            return Restrict(lowerBound);
        }

        public static VariableVector operator +(VariableVector v1, VariableVector v2) {
            return new VariableVector(v1.Name + "+" + v2.Name, v1._x + v2._x, v1._y + v2._y);
        }

        public static VariableVector operator -(VariableVector v1, VariableVector v2) {
            return new VariableVector(v1.Name + "-" + v2.Name, v1._x - v2._x, v1._y - v2._y);
        }

        public static VariableVector operator *(VariableVector v, double d) {
            return new VariableVector(v.Name + "*" + d, v._x * d, v._y * d);
        }

        public static VariableVector operator *(double d, VariableVector v) {
            return v * d;
        }

        public static VariableVector operator /(VariableVector v, double d) {
            return new VariableVector(v.Name + "*" + d, v._x / d, v._y / d);
        }

        public static VariableVector operator -(VariableVector v) {
            return new VariableVector("-" + v.Name, -v._x, -v._y);
        }

        public static VariableVector operator !(VariableVector v) {
            return new VariableVector("~" + v.Name, v._x, -v._y);
        }

        public static VariableVector operator ~(VariableVector v) {
            return new VariableVector("~" + v.Name, -v._x, v._y);
        }

        public VariableVector Horizontal() {
            return new VariableVector("_" + Name, _x, _x.Solver.ZERO);
        }

        public VariableVector Vertical() {
            return new VariableVector("_" + Name, _y.Solver.ZERO, _y);
        }

        public void Set(double x, double y) {
            _x.Set(x);
            _y.Set(y);
        }

        public VariableVector WithYOf([NotNull]VariableVector other) {
            return new VariableVector("X<" + Name + "&Y<" + other.Name, X, other.Y);
        }

        public VariableVector WithXOf([NotNull]VariableVector other) {
            return new VariableVector("X<" + other.Name + "&Y<" + Name, other.X, Y);
        }

        public VariableVector AlsoNamed(string name) {
            Name += ";" + name;
            _x.AddShortName(name + ".X");
            _y.AddShortName(name + ".Y");
            return this;
        }

        internal VariableVector Suffixed(string s) {
            Name += s;
            return this;
        }

        public VariableVector MinX(NumericVariable value) {
            _x.Min(value);
            return this;
        }

        public VariableVector MinX(double value) {
            _x.Min(value);
            return this;
        }

        public VariableVector MinY(NumericVariable value) {
            _y.Min(value);
            return this;
        }

        public VariableVector MinY(double value) {
            _y.Min(value);
            return this;
        }

        public VariableVector SetX(NumericVariable value) {
            _x.Set(value);
            return this;
        }

        public VariableVector SetX(double value) {
            _x.Set(value);
            return this;
        }

        public VariableVector SetY(NumericVariable value) {
            _y.Set(value);
            return this;
        }

        public VariableVector MaxX(NumericVariable value) {
            _x.Max(value);
            return this;
        }

        public VariableVector MaxX(double value) {
            _x.Max(value);
            return this;
        }

        public VariableVector MaxY(NumericVariable value) {
            _y.Max(value);
            return this;
        }

        public VariableVector MaxY(double value) {
            _y.Max(value);
            return this;
        }
    }
}