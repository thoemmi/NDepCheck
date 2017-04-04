using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IRenderer<in TDependency> where TDependency : class, IEdge {
        string GetMasterFileName(string argsAsString, string baseFileName);

        /// <summary>
        /// Render dependencies to some file
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="argsAsString"></param>
        /// <param name="baseFileName"></param>
        /// <returns>Returns full name of written masterfile; or null if not written</returns>
        void Render([ItemNotNull] [NotNull] IEnumerable<TDependency> dependencies, [NotNull] string argsAsString, 
                    [CanBeNull] string baseFileName);

        /// <summary>
        /// Render dependencies to stream
        /// </summary>
        void RenderToStreamForUnitTests([ItemNotNull, NotNull] IEnumerable<TDependency> dependencies, [NotNull] Stream stream);
    }
}