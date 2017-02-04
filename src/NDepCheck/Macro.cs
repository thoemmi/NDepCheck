namespace NDepCheck {
    public class Macro {
        public readonly string MacroText;
        public readonly string RuleFileName;
        public readonly int StartLineNo;

        internal Macro(string macroText, string ruleFileName, int startlineNo) {
            MacroText = macroText;
            RuleFileName = ruleFileName;
            StartLineNo = startlineNo;
        }

        public override bool Equals(object obj) {
            var other = obj as Macro;
            if (other == null) {
                return false;
            } else {
                return other.MacroText == MacroText;
            }
        }

        public override int GetHashCode() {
            return MacroText.GetHashCode();
        }
    }
}