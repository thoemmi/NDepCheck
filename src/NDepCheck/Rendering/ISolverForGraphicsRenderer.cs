using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NDepCheck.ConstraintSolving;

namespace NDepCheck.Rendering {
    public interface ISolverForGraphicsRenderer {
        SolverVariableForGraphicsRenderer CreateConstant(string shortName, double value);

        SolverVariableForGraphicsRenderer CreateVariable(string shortName, float interpolate = 0.5f);

        SolverVariableForGraphicsRenderer CreateVariable(string shortName, double? lo, double? hi, float interpolate);
    }

    public abstract class SolverVariableForGraphicsRenderer {
        //public abstract ISolverForGraphicsRenderer Solver { get; }

        public static SolverVariableForGraphicsRenderer operator +(SolverVariableForGraphicsRenderer left, double right) {
            return left + left.CreateConstant(right);
        }

        private SolverVariableForGraphicsRenderer CreateConstant(double d) {
            return Solver.GetOrCreateVariable("const", "" + d, d, d, 0);
        }

        public static SolverVariableForGraphicsRenderer operator +(double left, SolverVariableForGraphicsRenderer right) {
            return right.CreateConstant(left) + right;
        }

        public static SolverVariableForGraphicsRenderer operator -(SolverVariableForGraphicsRenderer left, double right) {
            return left + -right;
        }

        public static SolverVariableForGraphicsRenderer operator -(double left, SolverVariableForGraphicsRenderer right) {
            return left + -right;
        }

        private NumericVariable DeriveVariable(string shortName, string definition) {
            return Solver.GetOrCreateVariable(shortName, definition, null, null, Interpolate);
        }

        public static NumericVariable operator -(NumericVariable left, NumericVariable right) {
            NumericVariable result = left.DeriveVariable("+Sum0rh", $".{left.VarIndex}-.{right.VarIndex}");
            new SumIs0Constraint(-left, right, result);
            return result;
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

    }
}
