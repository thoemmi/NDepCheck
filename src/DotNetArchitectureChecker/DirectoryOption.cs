using System.IO;

namespace DotNetArchitectureChecker {
    public class DirectoryOption {
        public readonly string Path;
        public readonly bool Recurse;
        public DirectoryOption(string path, bool recurse) {
            Path = path;
            Recurse = recurse;
        }
    }
}
