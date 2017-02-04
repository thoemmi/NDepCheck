using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck {
    public class DirectoryOption {
        private readonly IDictionary<string, string> _smallToFullMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public DirectoryOption([NotNull] string path, bool recurse) {
            SearchOption o = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            Add(path, "*.dll.dep", o);
            Add(path, "*.exe.dep", o);
        }

        private void Add([NotNull] string path, [NotNull] string pattern, SearchOption o) {
            foreach (var f in Directory.GetFiles(path, pattern, o)) {
                string fileName = Path.GetFileName(f);
                string fullPath = Path.GetFullPath(f);
                if (_smallToFullMap.ContainsKey(fileName)) {
                    Log.WriteWarning(fileName + " found at two places: " + _smallToFullMap[fileName] + " and " + fullPath + "; second one is ignored");
                } else {
                    _smallToFullMap[fileName] = fullPath;
                }
            }
        }

        [CanBeNull]
        public string GetFullNameFor([NotNull] string filename) {
            string result;
            _smallToFullMap.TryGetValue(filename, out result);
            return result;
        }
    }
}