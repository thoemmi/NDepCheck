namespace NDepCheck {
    public class TextFileSource : FileSource {
        public TextFileSource(string sourceName, int? line) : base(sourceName) {
            Line = line;
        }

        public int? Line {
            get;
        }

        public override string AsDipString() => $"{ContainerUri}|{Line}";

        public override string ToString() => base.AsDipString() + $"/{ContainerUri}{(Line.HasValue ? "/" + Line : "")}";

        public static ISourceLocation MaybeCreate(string[] fields) {
            if (fields.Length == 2) {
                int line;
                return new TextFileSource(fields[0], int.TryParse(fields[1], out line) ? (int?) line : null);
            } else {
                return null;
            }
        }
    }
}