using System;
using System.Collections.Generic;
using System.IO;

namespace NDepCheck {
    public class DirectoryOption {
        private readonly IDictionary<string, string> _smallToFullMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

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
                    Log.WriteWarning(fileName + " found at two places: " + _smallToFullMap[fileName] + " and " + fullPath + "; second one is ignored");
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