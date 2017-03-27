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

        private static string FormatMessage(Dependency dependency, string message) {
            return dependency.Source != null 
                ? new StringBuilder(message).Append(" (probably at ").Append(dependency.Source).Append(")").ToString() 
                : message;
        }

        public void WriteViolation(Dependency dependency) {
            if (dependency.BadCt > 0) {
                WriteError(FormatMessage(dependency, dependency.BadDependencyMessage()));
            } else if (dependency.QuestionableCt > 0) {
                WriteWarning(FormatMessage(dependency, dependency.BadDependencyMessage()));
            }
        }
    }
}