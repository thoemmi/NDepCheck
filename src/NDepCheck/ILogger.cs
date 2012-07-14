using System;

namespace NDepCheck {
    public interface ILogger {
        void StartProcessingAssembly(string assemblyFilename);
        void WriteError(string msg);
        void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn);
        void WriteWarning(string msg);
        void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn);
        void WriteInfo(string msg);
        void WriteDebug(string msg);
    }

    public static class Log {
        public static ILogger Logger { get; set; }

        internal static void StartProcessingAssembly(string assemblyFilename) {
            Logger.StartProcessingAssembly(assemblyFilename);
        }

        internal static void Error(string format, params object[] args) {
            Logger.WriteError(String.Format(format, args));
        }

        internal static void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine,
                                        uint endColumn) {
            Logger.WriteError(msg, fileName, startLine, startColumn, endLine, endColumn);
        }

        internal static void Warning(string format, params object[] args) {
            Logger.WriteWarning(String.Format(format, args));
        }

        internal static void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine,
                                          uint endColumn) {
            Logger.WriteWarning(msg, fileName, startLine, startColumn, endLine, endColumn);
        }

        internal static void Info(string format, params object[] args) {
            Logger.WriteInfo(String.Format(format, args));
        }

        internal static void Debug(string format, params object[] args) {
            Logger.WriteDebug(String.Format(format, args));
        }
    }
}
