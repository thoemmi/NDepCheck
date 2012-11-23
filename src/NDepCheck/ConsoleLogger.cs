using System;
using System.Text;

namespace NDepCheck {
    internal class ConsoleLogger : ILogger {
        #region ILogger Members

        public void StartProcessingAssembly(string assemblyFilename) {
        }

        public void WriteError(string msg) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        public void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            WriteError(Format(msg, fileName, startLine));
        }

        public void WriteWarning(string msg) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        public void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            WriteWarning(Format(msg, fileName, startLine));
        }

        public void WriteInfo(string msg) {
            Console.Out.WriteLine(msg);
        }

        public void WriteDebug(string msg) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }

        #endregion

        private static string Format(string msg, string fileName, uint lineNumber) {
            if (fileName != null) {
                var sb = new StringBuilder(msg);
                sb.Append(" (probably at ").Append(fileName);
                if (lineNumber > 0) {
                    sb.Append(":").Append(lineNumber);
                }
                sb.Append(")");
                return sb.ToString();
            } else {
                return msg;
            }
        }
    }
}