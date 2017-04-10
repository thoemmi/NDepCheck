using System.IO;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Reading;

namespace NDepCheck {
    public abstract class InputOption {
        private IEnumerable<AbstractDependencyReader> _readers;

        protected abstract AbstractDependencyReader[] CreateReaders(GlobalContext options, bool needsOnlyItemTails);

        [NotNull]
        public IEnumerable<AbstractDependencyReader> CreateOrGetReaders([NotNull]GlobalContext options, bool needsOnlyItemTails) {
            if (_readers == null) {
                _readers = CreateReaders(options, needsOnlyItemTails);
            }
            return _readers;
        }
    }

    public class InputFileOption : InputOption {
        [NotNull]
        private readonly string _positive;
        [CanBeNull]
        private readonly string _negativeOrNull;
        [NotNull]
        private readonly IReaderFactory _readerFactory;

        public InputFileOption([NotNull] string positive, [CanBeNull] string negativeOrNull, [NotNull] IReaderFactory readerFactory) {
            _readerFactory = readerFactory;
            _positive = positive;
            _negativeOrNull = negativeOrNull;
        }

        protected override AbstractDependencyReader[] CreateReaders(GlobalContext options, bool needsOnlyItemTails) {
            var fileNames = new List<string>(Option.ExpandFilename(_positive, ".dll", ".exe"));
            if (_negativeOrNull != null) {
                var negative = new List<string>(Option.ExpandFilename(_negativeOrNull, ".dll", ".exe")).ConvertAll(Path.GetFullPath);
                fileNames.RemoveAll(f => negative.Contains(Path.GetFullPath(f)));
            }
            return fileNames.Select(fileName => _readerFactory.CreateReader(fileName, options, needsOnlyItemTails)).ToArray();
        }
    }
}
