using JetBrains.Annotations;

namespace NDepCheck {
    public class Macro {
        [NotNull]
        public string MacroText {
            get;
        }

        [NotNull]
        public string RuleFileName {
            get;
        }

        public int StartLineNo {
            get;
        }

        internal Macro([NotNull]string macroText, [NotNull]string ruleFileName, int startlineNo) {
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