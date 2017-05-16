namespace NDepCheck {
    public class FileSource : ISourceLocation {
        public FileSource(string containerUri) {
            ContainerUri = containerUri;
        }

        public string ContainerUri { get; }

        public virtual string AsDipString() => ContainerUri;

        public override string ToString() => ContainerUri;

        public static ISourceLocation Create(string[] fields) {
            return ProgramFileSource.MaybeCreate(fields)
                   ?? TextFileSource.MaybeCreate(fields)
                   ?? new FileSource(string.Join("/", fields));


        }
    }
}