namespace NDepCheck {
    public class ProgramFileSource : FileSource {
        public ProgramFileSource(string containerUri, string sourceName, int startLine, int startColumn, int endLine, int endColumn) : base(containerUri) {
            SourceName = sourceName;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public string SourceName { get; }
        public int StartLine { get; }
        public int StartColumn { get; }
        public int EndLine { get; }
        public int EndColumn { get; }

        public override string AsDipString() => $"{SourceName}|{StartLine}|{StartColumn}|{EndLine}|{EndColumn}";

        public override string ToString() => $"{SourceName}/{StartLine}";        
    }
}