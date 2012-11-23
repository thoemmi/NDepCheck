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
        public static bool IsVerboseEnabled { get; set; }
        public static bool IsDebugEnabled { get; set; }

        internal static void StartProcessingAssembly(string assemblyFilename) {
            Logger.StartProcessingAssembly(assemblyFilename);
        }

        internal static void WriteError(string msg) {
            Logger.WriteError(msg);
        }

        internal static void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine,
                                        uint endColumn) {
            Logger.WriteError(msg, fileName, startLine, startColumn, endLine, endColumn);
        }

        internal static void WriteWarning(string msg) {
            Logger.WriteWarning(msg);
        }

        internal static void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine,
                                          uint endColumn) {
            Logger.WriteWarning(msg, fileName, startLine, startColumn, endLine, endColumn);
        }

        internal static void WriteInfo(string msg) {
            Logger.WriteInfo(msg);
        }

        internal static void WriteDebug(string msg) {
            Logger.WriteDebug(msg);
        }
    }
}
