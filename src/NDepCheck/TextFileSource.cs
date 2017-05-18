namespace NDepCheck {
    public class TextFileSourceLocation : AbstractSourceLocation {
        public TextFileSourceLocation(string sourceName, int? line) : base(sourceName) {
            Line = line;
        }

        public int? Line {
            get;
        }

        public override string AsDipString() => $"{ContainerUri}|{Line}";

        public override string ToString() => base.ToString() + $"/{ContainerUri}{(Line.HasValue ? "/" + Line : "")}";

        public static ISourceLocation MaybeCreate(string[] fields) {
            int line;
            if (fields.Length == 1) {
                return new TextFileSourceLocation(fields[0], null);
            } else if (fields.Length == 2 && int.TryParse(fields[1], out line)) {
                return new TextFileSourceLocation(fields[0], line);
            } else {
                return null;
            }
        }
    }
}