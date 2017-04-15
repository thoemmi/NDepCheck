using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public interface IRenderer<in TDependency> where TDependency : class, IEdge {
        string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName);

        /// <summary>
        /// Render dependencies to some file
        /// </summary>
        /// <param name="globalContext"></param>
        /// <param name="dependencies"></param>
        /// <param name="dependenciesCount"></param>
        /// <param name="argsAsString"></param>
        /// <param name="baseFileName"></param>
        /// <param name="ignoreCase"></param>
        /// <returns>Returns full name of written masterfile; or null if not written</returns>
        void Render([NotNull] GlobalContext globalContext, [ItemNotNull] [NotNull] IEnumerable<TDependency> dependencies, 
                    int? dependenciesCount, [NotNull] string argsAsString, [CanBeNull] string baseFileName, bool ignoreCase);

        /// <summary>
        /// Render dependencies to stream
        /// </summary>
        void RenderToStreamForUnitTests([ItemNotNull, NotNull] IEnumerable<TDependency> dependencies, [NotNull] Stream stream);
    }
}