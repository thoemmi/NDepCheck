using System.IO;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public class InputFileOption {
        private readonly string _extension;
        private readonly string _positive;
        private readonly string _negativeOrNull;
        private IEnumerable<AbstractDependencyReader> _readers;

        public InputFileOption([NotNull]string extension, [NotNull]string positive, [CanBeNull]string negativeOrNull) {
            _extension = extension;
            _positive = positive;
            _negativeOrNull = negativeOrNull;
        }

        [NotNull]
        internal IEnumerable<AbstractDependencyReader> CreateOrGetReaders([NotNull]Options options, bool needsOnlyItemTails) {
            if (_readers == null) {
                var filenames = new List<string>(ExpandFilename(_positive));
                if (_negativeOrNull != null) {
                    var negative = new List<string>(ExpandFilename(_negativeOrNull)).ConvertAll(Path.GetFullPath);
                    filenames.RemoveAll(f => negative.Contains(Path.GetFullPath(f)));
                }
                _readers = filenames.Select(filename => AbstractReaderFactory.CreateReader(filename, _extension, options, needsOnlyItemTails)).ToArray();
            }
            return _readers;
        }

        private IEnumerable<string> ExpandFilename(string pattern) {
            if (pattern.StartsWith("@")) {
                using (TextReader nameFile = new StreamReader(pattern.Substring(1))) {
                    for (; ; ) {
                        string name = nameFile.ReadLine();
                        if (name == null) {
                            break;
                        }
                        name = name.Trim();
                        if (name != "") {
                            yield return name;
                        }
                    }
                }
            } else if (pattern.Contains("*") || pattern.Contains("?")) {
                int sepPos = pattern.LastIndexOf(Path.DirectorySeparatorChar);

                string dir = sepPos < 0 ? "." : pattern.Substring(0, sepPos);
                string filePattern = sepPos < 0 ? pattern : pattern.Substring(sepPos + 1);
                foreach (string name in Directory.GetFiles(dir, filePattern)) {
                    yield return name;
                }
            } else if (Directory.Exists(pattern)) {
                foreach (string name in Directory.GetFiles(pattern, "*.dll")) {
                    yield return name;
                }
                foreach (string name in Directory.GetFiles(pattern, "*.exe")) {
                    yield return name;
                }
            } else {
                yield return pattern;
            }
        }
    }
}
