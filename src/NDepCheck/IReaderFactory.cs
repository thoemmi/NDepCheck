using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck {
    public interface IReaderFactory : IPlugin {
        [NotNull, ItemNotNull]
        IEnumerable<string> SupportedFileExtensions { get; }
        [NotNull]
        IDependencyReader CreateReader([NotNull] string fileName, bool needsOnlyItemTails);
    }

    public interface IDependencyReader {
        string FullFileName { get; }

        void SetReadersInSameReadFilesBeforeReadDependencies(IDependencyReader[] readerGang);
        IEnumerable<Dependency> ReadDependencies(Environment readingEnvironment, int v, bool ignoreCase);
    }
}