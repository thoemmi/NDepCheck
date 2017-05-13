namespace NDepCheck {
    public class FileSource : ISourceLocation {
        public FileSource(string containerUri) {
            ContainerUri = containerUri;
        }

        public string ContainerUri { get; }
        public virtual string AsDipString() => ContainerUri;
    }
}