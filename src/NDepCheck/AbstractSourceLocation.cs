namespace NDepCheck {
    public abstract class AbstractSourceLocation : ISourceLocation {
        protected AbstractSourceLocation(string containerUri) {
            ContainerUri = containerUri;
        }

        public string ContainerUri { get; }

        public virtual string AsDipString() => ContainerUri;

        public override string ToString() => ContainerUri;

        public static ISourceLocation Create(string[] fields) {
            return ProgramFileSourceLocation.MaybeCreate(fields)
                   ?? TextFileSourceLocation.MaybeCreate(fields)
                   ?? LocalSourceLocation.MaybeCreate(fields)
                   ?? new TextFileSourceLocation(string.Join("/", fields), null);
        }
    }
}