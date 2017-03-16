using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.ConstraintSolving {
    public class NumericVariable {
        private static int _globalCt;
        protected readonly int _varIndex = _globalCt++;

        public string Definition { get; }

        public float Interpolate { get; }

        [NotNull]
        private readonly SimpleConstraintSolver _solver;

        [ItemNotNull]
        private readonly List<AbstractConstraint> _dependentConstraints = new List<AbstractConstraint>();

        private int _lastChangedAt = 1;
        public bool Fixed { get; private set; }

        internal NumericVariable(string shortName, string definition, [NotNull] SimpleConstraintSolver solver, double? lo, double? hi, float interpolate) {
            Definition = definition;
            ShortName = shortName;
            _solver = solver;
            Interpolate = interpolate;
            Value = new Range(lo ?? double.NegativeInfinity, hi ?? double.PositiveInfinity, solver.Eps);
        }

        public NumericVariable AddShortName(string name) {
            ShortName += ";" + name;
            return this;
        }

        public int LastChangedAt => _lastChangedAt;

        [NotNull]
        public Range Value { get; private set; }

        /// <summary>
        /// Compute a current estimate of the variable as follows: If either of the current estimate's 
        /// boundaries is infinity, the other boundary is returned. If both boundaries are defined,
        /// the interpolated value between them (as defined by <see cref="Interpolate"/>) is used;
        /// <c>Interpolate</c> == 0 means that the lower boundary is returned, <c>Inpterpolate</c> == 1
        /// returns the upper bound, and in general, <c>(1 - Interpolate) * Value.Lo + Interpolate * Value.Hi</c>
        /// is returned.
        /// </summary>
        /// <returns>Current estimate of the variable</returns>
        public double GetValue() {
            if (double.IsInfinity(Value.Hi)) {
                return Value.Lo;
            } else if (double.IsInfinity(Value.Lo)) {
                return Value.Hi;
            } else {
                return (1 - Interpolate) * Value.Lo + Interpolate * Value.Hi;
            }
        }

        public SimpleConstraintSolver Solver => _solver;

        public IEnumerable<AbstractConstraint> DependentConstraints => _dependentConstraints;

        public IEnumerable<AbstractConstraint> ActiveConstraints => _dependentConstraints.Where(c => !c.IsSubsumed);

        public int VarIndex => _varIndex;

        public string ShortName { get; set; }

        public override string ToString() {
            return $".{_varIndex} '{ShortName}'={Value} {(Fixed?'F':' ')} Definition={Definition} lastChange@{_lastChangedAt}";
        }

        public static NumericVariable operator +(NumericVariable left, NumericVariable right) {
            NumericVariable result = left.DeriveVariable("-Sum0rh", $".{left.VarIndex} + .{right.VarIndex}");
            new SumIs0Constraint(left, right, -result);
            return result;
        }

        private NumericVariable DeriveVariable(string shortName, string definition) {
            return Solver.GetOrCreateVariable(shortName, definition, null, null, Interpolate);
        }

        public static NumericVariable operator -(NumericVariable left, NumericVariable right) {
            NumericVariable result = left.DeriveVariable("+Sum0rh", $".{left.VarIndex}-.{right.VarIndex}");
            new SumIs0Constraint(-left, right, result);
            return result;
        }

        public static NumericVariable operator +(NumericVariable left, double right) {
            return left + left.CreateConstant(right);
        }

        private NumericVariable CreateConstant(double d) {
            return Solver.GetOrCreateVariable("const", "" + d, d, d, 0);
        }

        public static NumericVariable operator +(double left, NumericVariable right) {
            return right.CreateConstant(left) + right;
        }

        public static NumericVariable operator -(NumericVariable left, double right) {
            return left + -right;
        }

        public static NumericVariable operator -(double left, NumericVariable right) {
            return left + -right;
        }

        public static NumericVariable operator *(NumericVariable v, double d) {
            NumericVariable result = v.DeriveVariable("Prop*rh", $".{v.VarIndex}*{d}");
            new ProportionalConstraint(d, v, result);
            return result;
        }

        public static NumericVariable operator *(double d, NumericVariable v) {
            return v * d;
        }

        public static NumericVariable operator /(NumericVariable v, double d) {
            NumericVariable result = v.DeriveVariable("Prop/rh", $".{v.VarIndex}/{d}");
            new ProportionalConstraint(d, result, v);
            return result;
        }

        public static NumericVariable operator -(NumericVariable v) {
            NumericVariable result = v.DeriveVariable("Invrh", $"-.{v.VarIndex}");
            new IsInverseConstraint(v, result);
            return result;
        }

        /// <summary>
        /// The variable's minimum is restricted to the (eventual) value of another variable.
        /// </summary>
        /// <returns><c>this</c></returns>
        public NumericVariable Min(NumericVariable value) {
            new AtLeastConstraint(this, value);
            return this;
        }

        /// <summary>
        /// The variable's value is restricted to be equal to the (eventual) value of another variable.
        /// </summary>
        /// <returns><c>this</c></returns>
        public NumericVariable Set(NumericVariable value) {
            new EqualityConstraint(this, value);
            return this;
        }

        /// <summary>
        /// The variable's maximum is restricted to the (eventual) value of another variable.
        /// </summary>
        /// <returns><c>this</c></returns>
        public NumericVariable Max(NumericVariable value) {
            new AtLeastConstraint(value, this);
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lo"></param>
        /// <param name="hi"></param>
        /// <param name="d">Factor multipled into lo and hi; with a negative factor, the meaning of lo and hi is reversed; 
        /// e.g., RestrictRange(3, 4, -2) restricts the variable to the range [-8..-6]</param>
        /// <returns>true if the current range estimate has been changed by the new restriction.</returns>
        public bool RestrictRange(double lo, double hi, double d = 1) {
            double loD, hiD;
            if (d >= 0) {
                loD = lo * d;
                hiD = hi * d;
            } else {
                loD = hi * d;
                hiD = lo * d;
            }

            Range oldValue = Value;
            Range newValue = oldValue.Intersect(loD, hiD);

            //if (newValue.IsEmpty && oldValue.IsSingleValue) {
            //    newValue = new Range(oldValue.Lo - Math.Abs(oldValue.Lo) * 1e-5, oldValue.Hi + Math.Abs(oldValue.Hi) * 1e-5, 1e-5).Intersect(loD, hiD);
            //}
            if (newValue.IsEmpty) {
                throw new SolverException("No possible solution for variable " + this);
            }
            if (Equals(newValue, oldValue)) {
                return false;
            } else {
                Value = newValue;
                _lastChangedAt = _solver.Now;
                return true;
            }
        }

        /// <summary>
        /// The variable's value is restricted to a fixed range.
        /// </summary>
        /// <returns><c>true</c> if the current range estimate has been changed by the new restriction.</returns>
        public bool RestrictRange(Range range) {
            return RestrictRange(range.Lo, range.Hi);
        }

        /// <summary>
        /// The variable's minimum is restricted to a fixed value.
        /// </summary>
        /// <returns>The variable if its current range estimate has been changed by the new restriction</returns>
        public NumericVariable Min(double value) {
            return RestrictRange(value, double.PositiveInfinity) ? this : null;
        }

        /// <summary>
        /// The variable's value is restricted to a fixed value.
        /// </summary>
        /// <returns>The variable if its current range estimate has been changed by the new restriction</returns>
        public NumericVariable Set(double value) {
            return RestrictRange(value, value) ? this : null;
        }

        /// <summary>
        /// The variable's maximum is restricted to a fixed value.
        /// </summary>
        /// <returns>The variable if its current range estimate has been changed by the new restriction</returns>
        public NumericVariable Max(double value) {
            return RestrictRange(double.NegativeInfinity, value) ? this : null;
        }

        /// <summary>
        /// The variable is restricted to the value of <see cref="GetValue"/> if that value is finite.
        /// </summary>
        /// <returns><c>true</c> if the current range estimate has been changed by the new restriction.</returns>
        public bool Fix() {
            double fixedValue = GetValue();
            bool changed = !double.IsInfinity(fixedValue) && RestrictRange(fixedValue, fixedValue);
            if (changed) {
                Fixed = true;
            }
            return changed;
        }

        internal void AddAsDependentConstraintForUseInConstraintConstructorOnly(AbstractConstraint constraint) {
            _dependentConstraints.Add(constraint);
        }

        internal void MarkAllConstraintsDirty() {
            foreach (var c in _dependentConstraints) {
                c.MarkDirty();
            }
        }
    }
}