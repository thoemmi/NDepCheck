namespace DotNetArchitectureChecker {
    public interface ILogger {
        void StartProcessingAssembly(string assemblyFilename);
        void WriteError(string msg);
        void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn);
        void WriteWarning(string msg);
        void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn);
        void WriteInfo(string msg);
        void WriteDebug(string msg);
    }
}
