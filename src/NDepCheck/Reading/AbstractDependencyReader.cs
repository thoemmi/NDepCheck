using System;
using System.Collections.Generic;
using System.IO;
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
        public abstract IEnumerable<Dependency> ReadDependencies(int depth, bool ignoreCase);

        public abstract void SetReadersInSameReadFilesBeforeReadDependencies([NotNull] IDependencyReader[] readerGang);
    }
}