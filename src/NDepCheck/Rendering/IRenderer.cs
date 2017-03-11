using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IRenderer<in TItem, in TDependency>
        where TItem : class, INode
        where TDependency : class, IEdge {
        void RenderToFile([ItemNotNull, NotNull] IEnumerable<TItem> items, [ItemNotNull, NotNull] IEnumerable<TDependency> dependencies, string baseFilename, int? optionsStringLength);
        void RenderToStream([ItemNotNull, NotNull] IEnumerable<TItem> items, [ItemNotNull, NotNull] IEnumerable<TDependency> dependencies, Stream stream, int? optionsStringLength);
    }
}