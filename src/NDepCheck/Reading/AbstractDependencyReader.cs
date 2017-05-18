using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Reading {
    public abstract class AbstractDependencyReader : IDependencyReader {
        protected AbstractDependencyReader([NotNull]string fullFileName, string containerUri) {
            if (string.IsNullOrWhiteSpace(fullFileName)) {
                throw new ArgumentException("fileName must be non-empty", nameof(fullFileName));
            }
            FullFileName = fullFileName;
            ContainerUri = containerUri;
        }

        [NotNull]
        public string FullFileName { get; }

        [NotNull]
        protected string ContainerUri { get; }

        [NotNull]
        public abstract IEnumerable<Dependency> ReadDependencies(int depth, bool ignoreCase);

        public abstract void SetReadersInSameReadFilesBeforeReadDependencies([NotNull] IDependencyReader[] readerGang);
    }
}