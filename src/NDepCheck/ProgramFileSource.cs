namespace NDepCheck {
    public class ProgramFileSourceLocation : AbstractSourceLocation {
        public ProgramFileSourceLocation(string containerUri, string sourceName, int startLine, int startColumn, int endLine, int endColumn) : base(containerUri) {
            SourceName = sourceName;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public string SourceName {
            get;
        }
        public int StartLine {
            get;
        }
        public int StartColumn {
            get;
        }
        public int EndLine {
            get;
        }
        public int EndColumn {
            get;
        }

        public override string AsDipString() => base.AsDipString() + $"|{SourceName}|{StartLine}|{StartColumn}|{EndLine}|{EndColumn}";

        public override string ToString() => $"{SourceName}/{StartLine}";

        public static ISourceLocation MaybeCreate(string[] fields) {
            if (fields.Length == 6) {
                int startLine, startColumn, endLine, endColumn;
                int.TryParse(fields[2], out startLine);
                int.TryParse(fields[3], out startColumn);
                int.TryParse(fields[4], out endLine);
                int.TryParse(fields[5], out endColumn);
                return new ProgramFileSourceLocation(fields[0], fields[1], startLine, startColumn, endColumn, endLine);
            } else {
                return null;
            }

        }
    }
}