namespace NDepCheck {
    public interface ILogger {
        void WriteError(string msg);
        void WriteError(string msg, string filename, uint lineNumber);
        void WriteWarning(string msg);
        void WriteWarning(string msg, string filename, uint lineNumber);
        void WriteInfo(string msg);
        void WriteDebug(string msg);
        void WriteViolation(RuleViolation ruleViolation);
    }

    public static class Log {
        public static ILogger Logger { get; set; }
        public static bool IsVerboseEnabled { get; set; }
        public static bool IsDebugEnabled { get; set; }

        internal static void WriteError(string msg) {
            Logger.WriteError(msg);
        }

        internal static void WriteError(string msg, string filename, uint lineNumber) {
            Logger.WriteError(msg, filename, lineNumber);
        }

        internal static void WriteWarning(string msg) {
            Logger.WriteWarning(msg);
        }

        internal static void WriteWarning(string msg, string filename, uint lineNumber) {
            Logger.WriteWarning(msg, filename, lineNumber);
        }

        internal static void WriteInfo(string msg) {
            Logger.WriteInfo(msg);
        }

        internal static void WriteDebug(string msg) {
            Logger.WriteDebug(msg);
        }

        internal static void WriteViolation(RuleViolation ruleViolation) {
            Logger.WriteViolation(ruleViolation);
        }
    }
}
