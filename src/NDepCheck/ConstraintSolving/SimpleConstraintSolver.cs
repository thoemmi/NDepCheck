using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck.ConstraintSolving {
    // Things that should be done:
    // - Should constraints point to solver, and Variables not? 
    // -- Advantage: no "freestanding" new XConstraint, but solver.AreEqual(v1, v2).
    // -- Variables would have to carry an initiali Range with them that would be converted to a RangeConstraint when they were connected to the solver.
    // - Introduce plus and minus in Sum0Constraint. Algorithm is more tricky, but only "somewhat".
    // - a = b + c + d should result in only one Sum0Constraint = constraint rewriting might be useful.
    // And for the GraphicsRenderer:
    // - Use as few variables as possible
    // - DONE: Use as few constraints as possible - can (center - diagonal / 2) be optimized?

    public class SolverException : Exception {
        public SolverException(string message) : base(message) { }
    }


    public class SimpleConstraintSolver {
        public readonly double Eps;
        private readonly Dictionary<string, NumericVariable> _allVariables = new Dictionary<string, NumericVariable>();

        private int _now = 2; // 0 is var init; 1 is restrictions in constructors

        public bool DEBUG = true;

        public readonly NumericVariable ZERO;
        public readonly NumericVariable ONE;
        public readonly NumericVariable NEGATIVE_INF;
        public readonly NumericVariable POSITIVE_INF;
        private bool _solved;

        public SimpleConstraintSolver(double eps = 1.0 / 1024.0 / 1024.0 / 1024.0) {
            Eps = eps;
            ZERO = CreateConstant("0", 0);
            ONE = CreateConstant("1", 1);
            NEGATIVE_INF = CreateConstant("-inf", double.NegativeInfinity);
            POSITIVE_INF = CreateConstant("+inf", double.PositiveInfinity);
        }

        public NumericVariable CreateConstant(string shortName, double value) {
            return GetOrCreateVariable(shortName, $"{_allVariables.Count}.{shortName}", value, value, 0);
        }

        public NumericVariable CreateVariable(string shortName, float interpolate = 0.5f) {
            return GetOrCreateVariable(shortName, $"{_allVariables.Count}.{shortName}[{interpolate}]", null, null, interpolate);
        }

        public NumericVariable CreateVariable(string shortName, double? lo, double? hi, float interpolate) {
            return GetOrCreateVariable(shortName, $"{_allVariables.Count}.{shortName}[{lo}..{hi}@{interpolate}]", lo, hi, interpolate);
        }

        internal NumericVariable GetOrCreateVariable([CanBeNull] string shortName, [NotNull] string definition, double? lo, double? hi, float interpolate) {
            if (_solved) {
                throw new InvalidOperationException($"Solver is solved - no new variable '{shortName}', please! (definition='{definition}')");
            }

            NumericVariable result;
            if (!_allVariables.TryGetValue(definition, out result)) {
                _allVariables.Add(definition, result = new NumericVariable(shortName, definition, this, lo, hi, interpolate));
            }
            return result;
        }

        public int Now => _now;

        public void Solve([NotNull] Action checkAbort) {
            // Remove all constraints subsumed by others
            MarkSubsumingConstraints(_allVariables.Values);

            IEnumerable<NumericVariable> variablesWhoseDependentConstraintsShouldBePropagated = _allVariables.Values.ToArray();
            const int MAXLOOP = 10000;

            for (int i = 0; i < MAXLOOP; i++) {
                var modifiedVariables = new HashSet<NumericVariable>();

                foreach (var v in variablesWhoseDependentConstraintsShouldBePropagated) {
                    checkAbort();
                    foreach (var c in v.ActiveConstraints.ToArray()) {
                        IEnumerable<NumericVariable> changed = c.Propagate(this).ToArray();
                        foreach (var ch in changed) {
                            ch.MarkAllConstraintsDirty();
                        }
                        modifiedVariables.UnionWith(changed);
                        _now++;
                    }
                }

                if (!modifiedVariables.Any()) {
                    IEnumerable<NumericVariable> changedVariables = CheckState(_allVariables.Values);
                    modifiedVariables = new HashSet<NumericVariable>((changedVariables ?? Enumerable.Empty<NumericVariable>()).Where(v => v != null));
                    if (!modifiedVariables.Any()) {
                        break;
                    } else {
                        foreach (var ch in modifiedVariables) {
                            ch.MarkAllConstraintsDirty();
                        }
                        AbstractConstraint findADirtyConstraint = modifiedVariables.SelectMany(v => v.ActiveConstraints).FirstOrDefault(c => c.IsDirty);
                        if (findADirtyConstraint == null) {
                            throw new SolverException("No constraint was touched by the variables modified by CheckState");
                        }
                    }
                }

                variablesWhoseDependentConstraintsShouldBePropagated = modifiedVariables;
            }
            _solved = true;
            Log.WriteInfo("Solved constraints for " + _allVariables.Count + " variables.");
        }

        protected virtual IEnumerable<NumericVariable> CheckState(IEnumerable<NumericVariable> allVariables) {
            return null;
        }

        public string GetState(int maxLines) {
            var sb = new StringBuilder();
            foreach (var v in _allVariables.Values) {
                sb.AppendLine(v.ToString());
                foreach (var c in v.ActiveConstraints) {
                    sb.Append("   ");
                    sb.AppendLine(c.ToString());
                    if (maxLines-- < 0) {
                        sb.AppendLine($"...output cut after {maxLines} lines");
                        return sb.ToString();
                    }
                }
            }
            return sb.ToString();
        }

        public void MarkSubsumingConstraints(IEnumerable<NumericVariable> variables = null) {
            foreach (var v in variables ?? _allVariables.Values) {
                foreach (var c in v.ActiveConstraints.ToArray()) {
                    // ActiveConstraints is taken anew in each iteration. Otherwise, two equal constraints
                    // that were active at the beginning will see feel subsumed by the other and hence both become inactive.
                    AbstractConstraint subsumer = v.ActiveConstraints.FirstOrDefault(candidate => candidate != c && candidate.Subsumes(c));
                    if (subsumer != null) {
                        c.MarkAsSubsumedByThatAndOthers(subsumer);
                    }
                }
            }
        }
    }
}