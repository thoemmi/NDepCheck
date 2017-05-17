using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public class CountPattern {
        protected static readonly Dictionary<string, Func<int, int, bool>> _ops = new Dictionary<string, Func<int, int, bool>> {
            { ">", (v1, v2) => v1 > v2 },
            { "<", (v1, v2) => v1 < v2 },
            { "<=", (v1, v2) => v1 <= v2 },
            { ">=", (v1, v2) => v1 >= v2 },
            { "<>", (v1, v2) => v1 != v2 },
            { "!=", (v1, v2) => v1 != v2 },
            { "=", (v1, v2) => v1 == v2 },
            { "==", (v1, v2) => v1 == v2 },
            { "|", (v1, v2) => (v1 & v2) != 0 },
            { "^", (v1, v2) => (v1 & v2) == 0 },
        };

        protected static readonly string _regex = $"^(.*)({string.Join("|", _ops.Keys.Select(s => AsRegex(s)))})(.*)$";

        private static string AsRegex(string s) {
            return string.Join("", s.Select(c => "[" + (c == '^' ? @"\^" : "" + c) + "]"));

        }
    }

    public abstract class CountPattern<T> : CountPattern where T : class {
        public struct Eval {
            public readonly T LeftOrNullForConstant;
            public readonly T RightOrNullForConstant;
            public readonly Func<int, int, bool> Predicate;

            public Eval([CanBeNull] string leftOrNullForConst, [CanBeNull] string rightOrNullForConst,
                [NotNull] Func<string, T> createOperand, [NotNull] Func<int, int, bool> predicate) {
                LeftOrNullForConstant = leftOrNullForConst == null
                    ? null
                    : createOperand(leftOrNullForConst);
                RightOrNullForConstant = rightOrNullForConst == null
                    ? null
                    : createOperand(rightOrNullForConst);
                Predicate = predicate;
            }
        }

        public static Eval CreateEval(string pattern, string operandPattern, [NotNull] Func<string, T> createOperand) {
            if (string.IsNullOrWhiteSpace(pattern)) {
                return new Eval(null, null, _ => default(T), (x, y) => true);
            } else {
                string trimmedPattern = pattern.Trim();
                Match m = Regex.Match(trimmedPattern, _regex);
                if (!m.Success) {
                    return trimmedPattern.StartsWith("~")
                        ? CreateEval(trimmedPattern.Substring(1) + "=0", operandPattern, createOperand)
                        : CreateEval(trimmedPattern + ">0", operandPattern, createOperand);
                } else {
                    string left = m.Groups[1].Value.Trim();
                    string op = m.Groups[2].Value;
                    string right = m.Groups[3].Value.Trim();

                    int leftValue;
                    int rightValue;
                    return int.TryParse(left, out leftValue)
                        ? (int.TryParse(right, out rightValue)
                            ? new Eval(null, null, createOperand, _ops[op])
                            : new Eval(null, right, createOperand, _ops[op]))
                        : (int.TryParse(right, out rightValue)
                            ? new Eval(left, null, createOperand, _ops[op])
                            : new Eval(left, right, createOperand, _ops[op]));
                }
            }
        }        
    }
}