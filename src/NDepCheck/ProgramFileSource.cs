namespace NDepCheck {
    public class ProgramFileSource : ISourceLocation {
        public ProgramFileSource(string sourceName, int startLine, int startColumn, int endLine, int endColumn) {
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

        public string AsDipString() {
            return $"{SourceName}|{StartLine}|{StartColumn}|{EndLine}|{EndColumn}";
        }

        public override string ToString() {
            return $"{SourceName}/{StartLine}";
        }
    }
}