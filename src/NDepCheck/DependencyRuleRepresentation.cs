namespace NDepCheck {
    internal class DependencyRuleRepresentation {
        private readonly bool _isQuestionableRule;
        private readonly string _line;
        private readonly uint _lineNo;
        private readonly string _ruleFileName;
        private int _hitCount;

        internal DependencyRuleRepresentation(string ruleFileName, uint lineNo, string line, bool isQuestionableRule) {
            _ruleFileName = ruleFileName;
            _lineNo = lineNo;
            _line = line;
            _isQuestionableRule = isQuestionableRule;
            _hitCount = 0;
        }

        /// <summary>
        /// Was a rule represented by this representation ever matched with a true result?
        /// </summary>
        public bool WasHit {
            get { return _hitCount > 0; }
        }

        public int HitCount {
            get { return _hitCount; }
        }

        public bool IsQuestionableRule {
            get { return _isQuestionableRule; }
        }

        internal void MarkHit() {
            _hitCount++;
        }

        public override string ToString() {
            return _line + " (at " + _ruleFileName + ":" + _lineNo + ")";
        }
    }
}