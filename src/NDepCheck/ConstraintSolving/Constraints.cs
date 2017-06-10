using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.ConstraintSolving {
    public abstract class AbstractConstraint {
        private static int _globalCt;
        protected readonly int _ct = _globalCt++;

        protected int _lastPropagatedAt;
        private List<AbstractConstraint> _subsumers;
        private bool _isDirty = true;
        private readonly NumericVariable[] _outputVariables;

        protected AbstractConstraint(params NumericVariable[] bidirectionalVariables) : this (bidirectionalVariables, bidirectionalVariables) {
        }

        protected AbstractConstraint(NumericVariable[] inputVariables, NumericVariable[] outputVariables) {
            _outputVariables = outputVariables;

            foreach (var v in inputVariables) {
                v.AddAsDependentConstraintForUseInConstraintConstructorOnly(this);
            }
        }

        public bool IsSubsumed => _subsumers != null;

        protected string Info => "#" + _ct + (IsDirty ? " D " : "   ") + (IsSubsumed ? "--" : "@" + _lastPropagatedAt);

        public bool IsDirty => _isDirty;

        public void MarkAsSubsumedByThatAndOthers(AbstractConstraint c) {
            if (_subsumers == null) {
                _subsumers = new List<AbstractConstraint> { c };
            } else {
                _subsumers.Add(c);
            }
        }

        protected abstract bool Update(NumericVariable v);

        [ItemNotNull]
        public IEnumerable<NumericVariable> Propagate(SimpleConstraintSolver solver) {
            if (IsDirty) {
                IEnumerable<NumericVariable> outputVariablesFromOldestToNewest =
                    _outputVariables.OrderBy(v => v.LastChangedAt);
                IEnumerable<NumericVariable> changed = Update(outputVariablesFromOldestToNewest);
                _lastPropagatedAt = solver.Now;
                _isDirty = false;
                return changed.Where(ch => ch != null);
            } else {
                return Enumerable.Empty<NumericVariable>();
            }
        }

        [ItemCanBeNull]
        protected virtual IEnumerable<NumericVariable> Update(IEnumerable<NumericVariable> outputVariablesFromOldestToNewest) {
            var changed = new HashSet<NumericVariable>();
            foreach (var v in outputVariablesFromOldestToNewest) {
                if (Update(v)) {
                    changed.Add(v);
                }
            }
            return changed;
        }

        public void MarkDirty() => _isDirty = true;

        protected bool BaseEquals(AbstractConstraint other) {
            return true;
        }

        protected virtual int BaseGetHashCode() {
            return 1;
        }

        protected static bool EqualVariableList(NumericVariable[] leftVariables, NumericVariable[] rightVariables) {
            if (leftVariables.Length != rightVariables.Length) {
                return false;
            } else {
                for (int i = 0; i < leftVariables.Length; i++) {
                    if (leftVariables[i] != rightVariables[i]) {
                        return false;
                    }
                }
                return true;
            }
        }

        public virtual bool Subsumes(AbstractConstraint other) {
            return Equals(other);
        }
    }

    public abstract class UnaryConstraint : AbstractConstraint {
        [NotNull]
        private readonly NumericVariable _inputVariable;

        protected UnaryConstraint([NotNull]NumericVariable inputVariable) : base(inputVariable) {
            _inputVariable = inputVariable;
        }

        public bool BaseEquals(UnaryConstraint other) {
            return _inputVariable == other._inputVariable && base.BaseEquals(other);
        }

        protected override int BaseGetHashCode() {
            return base.BaseGetHashCode() ^ _inputVariable.GetHashCode();
        }
    }

    public sealed class RangeConstraint : UnaryConstraint {
        public static RangeConstraint CreateRangeConstraint(NumericVariable inputVariable, Range range) {
            return new RangeConstraint(inputVariable, range);
        }

        private readonly Range _range;

        private RangeConstraint(NumericVariable inputVariable, Range range) : base(inputVariable) {
            _range = range;
        }

        public Range Range => _range;

        protected override bool Update(NumericVariable v) {
            return v.RestrictRange(_range);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{Info} Range {_range}";
        }

        public override bool Equals(object obj) {
            var other = obj as RangeConstraint;
            return other != null && Range.Equals(other.Range) && BaseEquals(other);
        }

        public override int GetHashCode() {
            return BaseGetHashCode() ^ (_range?.GetHashCode() ?? 0);
        }

        public override bool Subsumes(AbstractConstraint other) {
            if (base.Subsumes(other)) {
                return true;
            } else {
                var otherRangeConstraint = other as RangeConstraint;
                return otherRangeConstraint != null && otherRangeConstraint.Range.IsSubsetOf(Range);
            }
        }
    }

    public abstract class BinaryConstraint : AbstractConstraint {
        [NotNull]
        protected readonly NumericVariable _variable1, _variable2;

        protected BinaryConstraint([NotNull]NumericVariable variable1, [NotNull]NumericVariable variable2) : base(variable1, variable2) {
            _variable1 = variable1;
            _variable2 = variable2;
        }

        public bool BaseEquals(BinaryConstraint other) {
            return _variable1 == other._variable1 && _variable2 == other._variable2 && base.BaseEquals(other);
        }

        protected override int BaseGetHashCode() {
            return base.BaseGetHashCode() ^ _variable1.GetHashCode() ^ _variable2.GetHashCode();
        }

    }

    public sealed class EqualityConstraint : BinaryConstraint {
        public static EqualityConstraint CreateEqualityConstraint(NumericVariable variable1, NumericVariable variable2) {
            return new EqualityConstraint(variable1, variable2);
        }

        private EqualityConstraint(NumericVariable variable1, NumericVariable variable2) : base(variable1, variable2) {
        }

        protected override bool Update(NumericVariable v) {
            return v.RestrictRange((v == _variable1 ? _variable2 : _variable1).Value);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{Info} Equality .{_variable1.VarIndex}===.{_variable2.VarIndex}";
        }

        public override bool Equals(object obj) {
            var other = obj as EqualityConstraint;
            return other != null && BaseEquals(other);
        }

        public override int GetHashCode() {
            return 1 ^ BaseGetHashCode();
        }
    }

    public sealed class IsInverseConstraint : BinaryConstraint {
        public static IsInverseConstraint CreateIsInverseConstraint(NumericVariable variable1, NumericVariable variable2) {
            return new IsInverseConstraint(variable1, variable2);
        }

        private IsInverseConstraint(NumericVariable variable1, NumericVariable variable2) : base(variable1, variable2) {
        }

        protected override bool Update(NumericVariable v) {
            return v.RestrictRange(-(v == _variable1 ? _variable2 : _variable1).Value);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{Info} IsInverse .{_variable1.VarIndex}=-=.{_variable2.VarIndex}";
        }

        public override bool Equals(object obj) {
            var other = obj as IsInverseConstraint;
            return other != null && BaseEquals(other);
        }

        public override int GetHashCode() {
            return 2 ^ BaseGetHashCode();
        }
    }

    public sealed class AtLeastConstraint : BinaryConstraint {
        public static AtLeastConstraint CreateAtLeastConstraint(NumericVariable variable1, NumericVariable variable2) {
            return new AtLeastConstraint(variable1, variable2);
        }

        private AtLeastConstraint(NumericVariable variable1, NumericVariable variable2) : base(variable1, variable2) {
        }

        protected override bool Update(NumericVariable v) {
            if (v == _variable1) {
                // v1 >= v2
                return _variable1.RestrictRange(_variable2.Value.Lo, double.PositiveInfinity);
            } else {
                // v2 <= v1
                return _variable2.RestrictRange(double.NegativeInfinity, _variable1.Value.Hi);
            }
        }

        public override string ToString() {
            return $"{Info} AtLeast .{_variable1.VarIndex}>=.{_variable2.VarIndex}";
        }

        public override bool Equals(object obj) {
            var other = obj as AtLeastConstraint;
            return other != null && BaseEquals(other);
        }

        public override int GetHashCode() {
            return 3 ^ BaseGetHashCode();
        }
    }

    public sealed class ProportionalConstraint : BinaryConstraint {
        public static ProportionalConstraint CreateProportionalConstraint(double d, NumericVariable variable1, NumericVariable variable2) {
            return new ProportionalConstraint(d, variable1, variable2);
        }

        // d * variable1 = variable2
        private readonly double _d;

        private ProportionalConstraint(double d, NumericVariable variable1, NumericVariable variable2) : base(variable1, variable2) {
            _d = d;
        }

        protected override bool Update(NumericVariable v) {
            if (v == _variable1) {
                return _variable1.RestrictRange(_variable2.Value.Lo, _variable2.Value.Hi, 1 / _d);
            } else {
                return _variable2.RestrictRange(_variable1.Value.Lo, _variable1.Value.Hi, _d);
            }
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{Info} Proportional {_d}*.{_variable1.VarIndex}==.{_variable2.VarIndex}";
        }

        public override bool Equals(object obj) {
            var other = obj as ProportionalConstraint;
            return other != null && Range.AlmostEquals(other._d, _d, _variable1.Solver.Eps) && BaseEquals(other);
        }

        public override int GetHashCode() {
            return _d.GetHashCode() ^ BaseGetHashCode();
        }
    }

    public sealed class SumIs0Constraint : AbstractConstraint {
        public static SumIs0Constraint CreateSumIs0Constraint(params NumericVariable[] inputVariables) {
            return new SumIs0Constraint(inputVariables);
        }

        private readonly NumericVariable[] _inputVariables;

        //public SumIs0Constraint(IEnumerable<DoubleHolder> plus) {
        //    _plus = plus.ToArray();
        //}

        private SumIs0Constraint(params NumericVariable[] inputVariables) : base(inputVariables) {
            _inputVariables = inputVariables;
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{Info} Sum0 " + string.Join("+", _inputVariables.Select(v => "." + v.VarIndex)) + "==0";
        }

        public override bool Equals(object obj) {
            var other = obj as SumIs0Constraint;
            return other != null && EqualVariableList(_inputVariables, other._inputVariables);
        }

        public override int GetHashCode() {
            return _inputVariables.Length ^ BaseGetHashCode();
        }

        //public SumIs0Constraint Plus(DoubleHolder d) {
        //    return new SumIs0Constraint(_plus.Concat(new[] { d }));
        //}

        protected override bool Update(NumericVariable v) {
            bool changed = false;

            // The following propagation uses these ideas:
            // For each variable v, take the sum S of all OTHER Lo values. A high value that is higher
            // than -S will never cancel S to zero, let alone sums that are greater than S. Hence,
            // v.Hi can be restricted to <= -S.
            // Likewise, v.Lo can be restricted to >= -S where S is the sum of all other Hi values.

            // O(n^2) algorithm - a O(n) algorithm is possible ... later
            double loSum = 0;

            // OnceExcluded is necessary for a constraint like X + X + Y = 0: When we already know
            // that X = [100..100] and Y = [-200..-200], this should succeed. However, when during 
            // Upgrade(X) we skip both X's (via w != v), the sum will not be 100-200 == 100, but 
            // -200; and hence
            // X will then be restricted to [200..+inf], which, together with the previously known
            // value X = 100 will erroneously imply that there is no solution for X.
            // By the way, when in X + X + Y = 0, we have X = [-inf...+inf] and Y = [-200..-200] at 
            // the beginning, we will NOT conclude that X = [100..100] - that would be equation
            // solving, which we do not support at this level.

            bool onceExcluded = false;
            foreach (var w in _inputVariables) {
                if (onceExcluded || w != v) {
                    double wLo = w.Value.Lo;
                    if (double.IsNegativeInfinity(wLo)) {
                        // Nothing can be said about the sum of lows - cancel this computation;
                        goto SKIP_LO;
                    }
                    loSum += wLo;
                } else {
                    onceExcluded = true;
                }
            }
            changed |= v.RestrictRange(double.NegativeInfinity, -loSum);
            SKIP_LO:

            double hiSum = 0;
            onceExcluded = false;
            foreach (var w in _inputVariables) {
                if (onceExcluded || w != v) {
                    double wHi = w.Value.Hi;
                    if (double.IsPositiveInfinity(wHi)) {
                        // Nothing can be said about the sum of his - cancel this computation;
                        goto SKIP_HI;
                    }
                    hiSum += wHi;
                } else {
                    onceExcluded = true;
                }
            }
            changed |= v.RestrictRange(-hiSum, double.PositiveInfinity);
            SKIP_HI:
            return changed;
        }
    }

    public class UnidirectionalComputationConstraint : AbstractConstraint {
        public static UnidirectionalComputationConstraint CreateUnidirectionalComputationConstraint(NumericVariable[] input, NumericVariable[] output, Action<NumericVariable[], NumericVariable[]> computation) {
            return new UnidirectionalComputationConstraint(input, output, computation);
        }

        public static UnidirectionalComputationConstraint CreateUnidirectionalComputationConstraint(NumericVariable[] input, NumericVariable[] output, Action computation) {
            return new UnidirectionalComputationConstraint(input, output, computation);
        }

        private readonly NumericVariable[] _input;
        private readonly NumericVariable[] _output;
        private readonly Action<NumericVariable[], NumericVariable[]> _computation;
        private readonly double _solverEps;

        private UnidirectionalComputationConstraint(NumericVariable[] input, NumericVariable[] output,
            Action computation) : this(input, output, (i, o) => computation()) {
        }

        private UnidirectionalComputationConstraint(NumericVariable[] input, NumericVariable[] output,
            Action<NumericVariable[], NumericVariable[]> computation) : base(input, output) {
            if (input == null || input.Length == 0) {
                throw new ArgumentNullException(nameof(input));
            }
            if (output == null || output.Length == 0) {
                throw new ArgumentNullException(nameof(output));
            }
            if (computation == null) {
                throw new ArgumentNullException(nameof(computation));
            }
            _input = input;
            _output = output;
            _computation = computation;
            _solverEps = _input[0].Solver.Eps;
        }

        protected override bool Update(NumericVariable v) {
            throw new NotImplementedException("Update(IEnumerable<NumericVariable>) is overridden");
        }

        protected override IEnumerable<NumericVariable> Update(IEnumerable<NumericVariable> outputVariablesFromOldestToNewest) {
            // TODO - the following need not be either sufficient or necessary ... check in computation !!!
            if (_input.All(v => !double.IsInfinity(v.GetValue()))
                && _output.Any(v => !v.Value.IsSingleValue)) {
                Range[] copiedOutputValuesBeforeComputation = _output.Select(v => {
                   return new Range(v.Value.Lo, v.Value.Hi, _solverEps);
                }).ToArray();
                _computation(_input, _output);
                return _output.Select((v, i) => v.Value.Equals(copiedOutputValuesBeforeComputation[i]) ? null : v);
            } else {
                return Enumerable.Empty<NumericVariable>();
            }
        }

        public override bool Equals(object obj) {
            var other = obj as UnidirectionalComputationConstraint;
            return other != null && _computation == other._computation && EqualVariableList(_input, other._input) && EqualVariableList(_output, other._output);
        }

        public override int GetHashCode() {
            return (_input.Length + 10 * _output.Length) ^ BaseGetHashCode();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{Info} Uni " + string.Join(",", _input.Select(v => "." + v.VarIndex)) + "==>>" + string.Join(",", _output.Select(v => "." + v.VarIndex));
        }
    }
}
