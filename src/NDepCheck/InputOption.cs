using System.IO;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Reading;

namespace NDepCheck {
    public abstract class InputOption {
        private IEnumerable<AbstractDependencyReader> _readers;

        protected abstract AbstractDependencyReader[] CreateReaders(GlobalContext options, bool needsOnlyItemTails);

        public abstract InputFileOption AddNegative(string negativeOrNull);

        [NotNull]
        public IEnumerable<AbstractDependencyReader> CreateOrGetReaders([NotNull]GlobalContext options, bool needsOnlyItemTails) {
            if (_readers == null) {
                _readers = CreateReaders(options, needsOnlyItemTails);
            }
            return _readers;
        }
    }

    public class InputFileOption : InputOption {
        [CanBeNull]
        private readonly string _positive;
        [NotNull, ItemNotNull]
        private readonly List<string> _negative = new List<string>();
        [NotNull]
        private readonly IReaderFactory _readerFactory;

        public InputFileOption([CanBeNull] string positive, [NotNull] IReaderFactory readerFactory) {
            _readerFactory = readerFactory;
            _positive = positive;
        }

        public override InputFileOption AddNegative(string negativeOrNull) {
            if (negativeOrNull != null) {
                _negative.Add(negativeOrNull);
            }
            return this;
        }

        protected override AbstractDependencyReader[] CreateReaders(GlobalContext options, bool needsOnlyItemTails) {
            var fileNames = new List<string>(Option.ExpandFilename(_positive, ".dll", ".exe"));
            List<string> negative = new List<string>(_negative.SelectMany(s => Option.ExpandFilename(s, ".dll", ".exe")))
                                    .ConvertAll(Path.GetFullPath);
            fileNames.RemoveAll(f => negative.Contains(Path.GetFullPath(f)));
            return fileNames.Select(fileName => _readerFactory.CreateReader(fileName, options, needsOnlyItemTails)).ToArray();
        }
    }
}
