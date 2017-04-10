using System;

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Reading {
    public interface IReaderFactory : IPlugin {
        AbstractDependencyReader CreateReader(string fileName, GlobalContext options, bool needsOnlyItemTails);
    }

    public abstract class AbstractReaderFactory : IReaderFactory {
        [NotNull]
        public abstract AbstractDependencyReader CreateReader([NotNull]string fileName, [NotNull]GlobalContext options, bool needsOnlyItemTails);

        public abstract string GetHelp(bool detailedHelp, string filter);
    }

    public abstract class AbstractDependencyReader {
        [NotNull]
        protected readonly string _fileName;

        protected AbstractDependencyReader([NotNull]string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("fileName must be non-empty", nameof(fileName));
            }
            _fileName = fileName;
        }

        [NotNull]
        public string FileName => _fileName;

        [NotNull]
        protected abstract IEnumerable<Dependency> ReadDependencies([CanBeNull] InputContext inputContext, int depth);

        /// <summary>
        /// Read dependencies from file
        /// </summary>
        /// <param name="depth"></param>
        /// <returns><c>null</c> if already read in</returns>
        [CanBeNull]
        public InputContext ReadDependencies(int depth) {
            var inputContext = new InputContext(FileName);
            Dependency[] dependencies = ReadDependencies(inputContext, depth).ToArray();
            if (!dependencies.Any()) {
                Log.WriteWarning("No dependencies found in " + FileName);
            }
            return inputContext;
        }
    }
}