using System.IO;
using System.Collections.Generic;

namespace DotNetArchitectureChecker {
    public class DirectoryOption {
        private readonly IDictionary<string, string> _smallToFullMap = new Dictionary<string, string>();
        public DirectoryOption(string path, bool recurse) {
            var o = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            Add(path, "*.dll.dep", o);
            Add(path, "*.exe.dep", o);
        }
        private void Add(string path, string pattern, SearchOption o) {
            foreach (var f in Directory.GetFiles(path, pattern, o)) {
                var fileName = Path.GetFileName(f);
                if (_smallToFullMap.ContainsKey(fileName)) {
                    // WriteWarning ..
                } else {
                    _smallToFullMap[fileName] = Path.GetFullPath(Path.Combine(path, f));
                }
            }
        }
        public string GetFullNameFor(string filename) {
            string result;
            _smallToFullMap.TryGetValue(filename, out result);
            return result;
        }
    }
}
