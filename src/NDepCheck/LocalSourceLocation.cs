namespace NDepCheck {
    public class LocalSourceLocation : AbstractSourceLocation {
        private readonly string _sourceName;

        public LocalSourceLocation(string containerUri, string sourceName) : base(containerUri) {
            _sourceName = sourceName;
        }

        public override string AsDipString() => base.AsDipString() + $"|{_sourceName}";

        public override string ToString() => base.ToString() + $"/{_sourceName}";

        public static ISourceLocation MaybeCreate(string[] fields) {
            if (fields.Length == 2) {
                return new LocalSourceLocation(fields[0], fields[1]);
            } else {
                return null;
            }
        }
    }
}