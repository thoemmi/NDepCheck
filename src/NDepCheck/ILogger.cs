namespace NDepCheck {
    public interface ILogger {
        void WriteError(string msg);
        void WriteError(string msg, string nestedFilenames, int lineNo);
        void WriteWarning(string msg);
        void WriteWarning(string msg, string nestedFilenames, int lineNo);
        void WriteInfo(string msg);
        void WriteDebug(string msg);
        void WriteViolation(Dependency dependency);
    }
}
