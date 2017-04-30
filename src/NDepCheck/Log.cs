namespace NDepCheck {
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

        internal static void WriteError(string msg, string nestedFilenames, int lineNo) {
            Logger.WriteError(msg, nestedFilenames, lineNo);
        }

        internal static void WriteWarning(string msg) {
            Logger.WriteWarning(msg);
        }

        internal static void WriteWarning(string msg, string nestedFilenames, int lineNo) {
            Logger.WriteWarning(msg, nestedFilenames, lineNo);
        }

        internal static void WriteInfo(string msg, bool collapse = false) {
            // Identical infos are collapsed - helps with chatty output on checking
            if (!collapse || msg != _previousInfo) {
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