namespace NDepCheck {
    public class TextFileSource : FileSource {
        public TextFileSource(string sourceName, int? line) : base(sourceName) {
            Line = line;
        }

        public int? Line { get; }

        public override string AsDipString() => $"{ContainerUri}|{Line}";
        
        public override string ToString()=>$"{ContainerUri}{(Line.HasValue ? "/" + Line : "")}";
    }
}