namespace NDepCheck {
    public class TextFileSource : ISourceLocation {
        public TextFileSource(string sourceName, int? line) {
            SourceName = sourceName;
            Line = line;
        }

        public string SourceName { get; }
        public int? Line { get; }

        public string AsDipString() {
            return $"{SourceName}|{Line}";
        }

        public override string ToString() {
            return $"{SourceName}{(Line.HasValue ? "/" + Line : "")}";
        }
    }
}