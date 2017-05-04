using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Reading {
    public abstract class AbstractDependencyReader : IDependencyReader {
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
        protected abstract IEnumerable<Dependency> ReadDependencies([CanBeNull] InputContext inputContext, int depth, bool ignoreCase);

        /// <summary>
        /// Read dependencies from file
        /// </summary>
        /// <param name="depth"></param>
        /// <param name="ignoreCase"></param>
        /// <returns><c>null</c> if already read in</returns>
        [CanBeNull]
        public InputContext ReadDependencies(int depth, bool ignoreCase) {
            var inputContext = new InputContext(FullFileName);
            IEnumerable<Dependency> dependencies = ReadDependencies(inputContext, depth, ignoreCase).ToArray();
            if (!dependencies.Any()) {
                Log.WriteWarning("No dependencies found in " + FullFileName);
            }
            return inputContext;
        }

        public abstract void SetReadersInSameReadFilesBeforeReadDependencies([NotNull] IDependencyReader[] readerGang);
    }
}