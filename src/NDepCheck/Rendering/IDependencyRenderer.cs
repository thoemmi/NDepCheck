using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IDependencyRenderer {
        void RenderToFile([NotNull] [ItemNotNull] IEnumerable<Item> items, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, string baseFilename, int? optionsStringLength);
        void RenderToStream([NotNull] [ItemNotNull] IEnumerable<Item> items, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, Stream output, int? optionsStringLength);
    }
}