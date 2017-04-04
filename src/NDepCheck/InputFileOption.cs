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
                var fileNames = new List<string>(Options.ExpandFilename(_positive, ".dll", ".exe"));
                if (_negativeOrNull != null) {
                    var negative = new List<string>(Options.ExpandFilename(_negativeOrNull, ".dll", ".exe")).ConvertAll(Path.GetFullPath);
                    fileNames.RemoveAll(f => negative.Contains(Path.GetFullPath(f)));
                }
                _readers = fileNames.Select(fileName => _readerFactory.CreateReader(fileName, options, needsOnlyItemTails)).ToArray();
            }
            return _readers;
        }

    }
}
