using System.IO;

namespace DependencyChecker {
    public class DirectoryOption {
        public readonly string Path;
        public readonly SearchOption SearchOption;
        public DirectoryOption(string path, bool recurse) {
            Path = path;
            SearchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        }
    }
}
