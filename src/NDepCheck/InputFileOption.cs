using System.IO;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Reading;

namespace NDepCheck {
    public class InputFileOption {
        [NotNull]
        private readonly string _positive;
        [CanBeNull]
        private readonly string _negativeOrNull;
        [NotNull]
        private readonly IReaderFactory _readerFactory;

        private IEnumerable<AbstractDependencyReader> _readers;

        public InputFileOption([NotNull] string positive, [CanBeNull] string negativeOrNull, [NotNull] IReaderFactory readerFactory) {
            _readerFactory = readerFactory;
            _positive = positive;
            _negativeOrNull = negativeOrNull;
        }

        [NotNull]
        public IEnumerable<AbstractDependencyReader> CreateOrGetReaders([NotNull]GlobalContext options, bool needsOnlyItemTails) {
            if (_readers == null) {
                var fileNames = new List<string>(ExpandFilename(_positive));
                if (_negativeOrNull != null) {
                    var negative = new List<string>(ExpandFilename(_negativeOrNull)).ConvertAll(Path.GetFullPath);
                    fileNames.RemoveAll(f => negative.Contains(Path.GetFullPath(f)));
                }
                _readers = fileNames.Select(fileName => _readerFactory.CreateReader(fileName, options, needsOnlyItemTails)).ToArray();
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
