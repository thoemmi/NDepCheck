using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IRenderer<in TDependency> where TDependency : class, IEdge {
        string GetMasterFileName(string argsAsString, string baseFileName);
        void Render([ItemNotNull] [NotNull] IEnumerable<TDependency> dependencies, [NotNull] string argsAsString, [CanBeNull] string baseFileName);
        void RenderToStreamForUnitTests([ItemNotNull, NotNull] IEnumerable<TDependency> dependencies, [NotNull] Stream stream);
    }
}