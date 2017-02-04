using System;
using System.Text;

namespace NDepCheck {
    internal class ConsoleLogger : ILogger {
        public void WriteError(string msg) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        public void WriteError(string msg, string nestedFilenames, int lineNumber) {
            if (string.IsNullOrEmpty(nestedFilenames)) {
                WriteError(msg);
            } else {
                WriteError(msg + $" ({nestedFilenames}:{lineNumber})");
            }
        }

        public void WriteViolation(RuleViolation ruleViolation) {
            Console.ForegroundColor = ruleViolation.ViolationType == ViolationType.Warning ? ConsoleColor.Yellow : ConsoleColor.Red;
            Console.Out.WriteLine(FormatMessage(ruleViolation.Dependency, ruleViolation.ViolationType));
            Console.ResetColor();
        }

        public void WriteWarning(string msg) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        public void WriteWarning(string msg, string nestedFilenames, int lineNumber) {
            if (string.IsNullOrEmpty(nestedFilenames)) {
                WriteWarning(msg);
            } else {
                WriteWarning(msg + $" ({nestedFilenames}:{lineNumber})");
            }
        }

        public void WriteInfo(string msg) {
            Console.Out.WriteLine(msg);
        }

        public void WriteDebug(string msg) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        private static string FormatMessage(Dependency dependency, ViolationType violationType) {
            var message = violationType == ViolationType.Warning ? dependency.QuestionableMessage() : dependency.IllegalMessage();
            if (dependency.FileName != null) {
                var sb = new StringBuilder(message);
                sb.Append(" (probably at ").Append(dependency.FileName);
                if (dependency.StartLine > 0) {
                    sb.Append(":").Append(dependency.StartLine);
                }
                sb.Append(")");
                return sb.ToString();
            } else {
                return message;
            }
        }
    }
}