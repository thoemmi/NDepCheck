using System;

namespace DotNetArchitectureChecker {
    internal class ConsoleLogger : ILogger {
        #region ILogger Members

        public void StartProcessingAssembly(string assemblyFilename) {
        }

        public void WriteError(string msg) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Out.WriteLine("**** " + msg);
            Console.ResetColor();
        }

        public void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            WriteError(Format(msg, fileName, startLine));
        }

        public void WriteWarning(string msg) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Out.WriteLine("???? " + msg);
            Console.ResetColor();
        }

        public void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            WriteWarning(Format(msg, fileName, startLine));
        }

        public void WriteInfo(string msg) {
            Console.Out.WriteLine("---- " + msg);
        }

        public void WriteDebug(string msg) {
            Console.Out.WriteLine("//// " + msg);
        }

        #endregion

        private static string Format(string msg, string fileName, uint lineNumber) {
            if (fileName != null) {
                msg += " (probably at " + fileName;
                if (lineNumber > 0)
                    msg += ":" + lineNumber;
                msg += ")";
            }
            return msg;
        }
    }
}