using System.Collections.Generic;

namespace NDepCheck.Rendering {
    public interface IDependencyRenderer : IRenderer<Item, Dependency>, IPlugin {
        void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies);
    }
}
