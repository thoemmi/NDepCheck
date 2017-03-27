namespace NDepCheck {
    public interface ILogger {
        void WriteError(string msg);
        void WriteError(string msg, string nestedFilenames, int lineNumber);
        void WriteWarning(string msg);
        void WriteWarning(string msg, string nestedFilenames, int lineNumber);
        void WriteInfo(string msg);
        void WriteDebug(string msg);
        void WriteViolation(Dependency dependency);
    }

    public static class Log {
        public enum Level { Standard, Verbose, Chatty }

        public static ILogger Logger {
            get; set;
        } = new ConsoleLogger();

        private static Level _level = Level.Standard;

        public static void SetLevel(Level level) {
            _level = level;
        }

        public static bool IsChattyEnabled => _level >= Level.Chatty;

        public static bool IsVerboseEnabled => _level >= Level.Verbose;

        private static string _previousInfo;

        internal static void WriteError(string msg) {
            Logger.WriteError(msg);
        }

        internal static void WriteError(string msg, string nestedFilenames, int lineNumber) {
            Logger.WriteError(msg, nestedFilenames, lineNumber);
        }

        internal static void WriteWarning(string msg) {
            Logger.WriteWarning(msg);
        }

        internal static void WriteWarning(string msg, string nestedFilenames, int lineNumber) {
            Logger.WriteWarning(msg, nestedFilenames, lineNumber);
        }

        internal static void WriteInfo(string msg) {
            // Identical infos are collapsed - helps with chatty output on checking
            if (msg != _previousInfo) {
                Logger.WriteInfo(msg);
            }
            _previousInfo = msg;
        }

        internal static void WriteDebug(string msg) {
            Logger.WriteDebug(msg);
        }

        internal static void WriteViolation(Dependency dependency) {
            Logger.WriteViolation(dependency);
        }
    }
}
