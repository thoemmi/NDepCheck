namespace NDepCheck {
    public class DependencyRuleRepresentation {
        private readonly bool _isQuestionableRule;
        private readonly string _line;
        private readonly int _lineNo;
        private readonly string _ruleFileName;
        private int _hitCount;

        public DependencyRuleRepresentation(string ruleFileName, int lineNo, string line, bool isQuestionableRule) {
            _ruleFileName = ruleFileName;
            _lineNo = lineNo;
            _line = line;
            _isQuestionableRule = isQuestionableRule;
            _hitCount = 0;
        }

        /// <summary>
        /// Was a rule represented by this representation ever matched with a true result?
        /// </summary>
        public bool WasHit => _hitCount > 0;

        public int HitCount => _hitCount;

        public bool IsQuestionableRule => _isQuestionableRule;

        public string RuleFileName => _ruleFileName;

        public int LineNo => _lineNo;

        internal void MarkHit() {
            _hitCount++;
        }

        public override string ToString() {
            return _line + " (at " + RuleFileName + ":" + LineNo + ")";
        }
    }
}