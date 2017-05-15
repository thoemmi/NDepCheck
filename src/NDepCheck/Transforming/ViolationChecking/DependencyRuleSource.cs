using System.Diagnostics;
using JetBrains.Annotations;

namespace NDepCheck {
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public class DependencyRuleSource {
        private readonly string _line;
        private int _hitCount;

        public DependencyRuleSource([NotNull] string ruleSourceName, int lineNo, [NotNull] string line, 
                                            bool isQuestionableRule, [NotNull] string trimmedUsingPattern) {
            RuleSourceName = ruleSourceName;
            LineNo = lineNo;
            _line = line;
            IsQuestionableRule = isQuestionableRule;
            TrimmedUsingPattern = trimmedUsingPattern;
            _hitCount = 0;
        }

        /// <summary>
        /// Was a rule represented by this representation ever matched with a true result?
        /// </summary>
        public bool WasHit => _hitCount > 0;

        public int HitCount => _hitCount;

        public bool IsQuestionableRule { get; }

        [NotNull]
        public string TrimmedUsingPattern { get; }

        [NotNull]
        public string RuleSourceName { get; }

        public int LineNo { get; }

        internal void MarkHit() {
            _hitCount++;
        }

        public override string ToString() {
            return _line + " (at " + RuleSourceName + ":" + LineNo + ")";
        }
    }
}