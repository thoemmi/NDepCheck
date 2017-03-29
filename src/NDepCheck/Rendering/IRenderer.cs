using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IRenderer<in TItem, in TDependency>
        where TItem : class, INode
        where TDependency : class, IEdge {
        string Render([ItemNotNull] [NotNull] IEnumerable<TItem> items, [ItemNotNull] [NotNull] IEnumerable<TDependency> dependencies, [NotNull] string argsAsString, [CanBeNull] string baseFilename);
        void RenderToStreamForUnitTests([ItemNotNull, NotNull] IEnumerable<TItem> items, [ItemNotNull, NotNull] IEnumerable<TDependency> dependencies, [NotNull] Stream stream);
    }
}