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

        [NotNull]
        public ItemType UsingItemType {
            get;
        }

        [NotNull]
        public ItemType UsedItemType {
            get;
        }

        internal Macro([NotNull]string macroText, [NotNull]string ruleFileName, int startlineNo, [NotNull] ItemType usingItemType, [NotNull] ItemType usedItemType) {
            MacroText = macroText;
            RuleFileName = ruleFileName;
            StartLineNo = startlineNo;
            UsingItemType = usingItemType;
            UsedItemType = usedItemType;
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