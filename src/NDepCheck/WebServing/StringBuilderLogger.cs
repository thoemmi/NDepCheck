using System.Text;

namespace NDepCheck.WebServing {
    public class StringBuilderLogger : AbstractLogger {
        private readonly StringBuilder _sb = new StringBuilder();

        protected override void DoWriteError(string msg) {
            _sb.AppendLine($"**** {msg}");
        }

        protected override void DoWriteWarning(string msg) {
            _sb.AppendLine($"++++ {msg}");
        }

        public override void WriteInfo(string msg) {
            _sb.AppendLine($"---- {msg}");
        }

        public override void WriteDebug(string msg) {
            _sb.AppendLine($";;;; {msg}");
        }

        public string GetString() {
            return _sb.ToString();
        }
    }
}