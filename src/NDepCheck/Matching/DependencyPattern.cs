using System;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public sealed class DependencyPattern : CountPattern<string> {
        [NotNull]
        private readonly MarkerMatch _markerPattern;

        private const string COUNT_FIELD_NAME_PATTERN = "^[#!?O]$";

        private readonly Eval[] _evals;

        public DependencyPattern(string pattern, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            _evals =
                patternParts[0].Split('&')
                    .Select(element => CreateEval(element, COUNT_FIELD_NAME_PATTERN, s => s))
                    .ToArray();
            _markerPattern = new MarkerMatch(patternParts.Length > 1 ? patternParts[1] : "", ignoreCase);
        }

        public bool IsMatch<TItem>(AbstractDependency<TItem> dependency) where TItem : AbstractItem<TItem> {
            if (!_markerPattern.IsMatch(dependency.MarkerSet)) {
                return false;
            } else {
                return _evals.All(e => e.Predicate(GetValue(dependency, e.LeftOrNullForConstant), GetValue(dependency, e.RightOrNullForConstant)));
            }
        }

        private static int GetValue<TItem>(AbstractDependency<TItem> dependency, [CanBeNull]string operandOrNullForConstant) where TItem : AbstractItem<TItem> {
            switch (operandOrNullForConstant) {
                case null:
                    return 0;
                case "#":
                    return dependency.Ct;
                case "?":
                    return dependency.QuestionableCt;
                case "!":
                    return dependency.BadCt;
                case "O":
                    return Equals(dependency.UsingItem, dependency.UsedItem) ? 1 : 0;
                default:
                    throw new ArgumentException($"Unexpected DependencyPattern operand '{operandOrNullForConstant}'");
            }
        }
    }
}