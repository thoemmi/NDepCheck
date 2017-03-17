using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IDependencyRenderer {
        void Render([NotNull] [ItemNotNull] IEnumerable<Item> items, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, string argsAsString);
        void RenderToStreamForUnitTests([NotNull] [ItemNotNull] IEnumerable<Item> items, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            Stream output);
        void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies);
        string GetHelp();
    }
}