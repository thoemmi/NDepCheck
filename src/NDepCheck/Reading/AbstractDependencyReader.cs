using System;

using System.Collections.Generic;
using System.IO;
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
        protected readonly string _fullFileName;

        protected AbstractDependencyReader([NotNull]string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("fileName must be non-empty", nameof(fileName));
            }
            _fullFileName = Path.GetFullPath(fileName);
        }

        [NotNull]
        public string FullFileName => _fullFileName;

        [NotNull]
        protected abstract IEnumerable<Dependency> ReadDependencies([CanBeNull] InputContext inputContext, int depth);

        /// <summary>
        /// Read dependencies from file
        /// </summary>
        /// <param name="depth"></param>
        /// <returns><c>null</c> if already read in</returns>
        [CanBeNull]
        public InputContext ReadDependencies(int depth) {
            var inputContext = new InputContext(FullFileName);
            Dependency[] dependencies = ReadDependencies(inputContext, depth).ToArray();
            if (!dependencies.Any()) {
                Log.WriteWarning("No dependencies found in " + FullFileName);
            }
            return inputContext;
        }
    }
}