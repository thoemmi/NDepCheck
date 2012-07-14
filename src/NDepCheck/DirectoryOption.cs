using System.IO;
using System.Collections.Generic;

namespace NDepCheck {
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
                    string fullPath = Path.GetFullPath(f);
                if (_smallToFullMap.ContainsKey(fileName)) {
                    Log.Warning("{0} found at two places: {1} and {2}; second one is ignored", fileName, _smallToFullMap[fileName], fullPath);
                } else {
                    _smallToFullMap[fileName] = fullPath;
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
