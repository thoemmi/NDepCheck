namespace NDepCheck {
    public interface ISourceLocation {
        string ContainerUri {
            get;
        }
        string AsDipString();
    }
}