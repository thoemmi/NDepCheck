using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck {
    public interface IRenderer : IPlugin {
        WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, string optionsString, WriteTarget baseTarget);

        /// <summary>
        /// Render dependencies to some file
        /// </summary>
        /// <param name="globalContext"></param>
        /// <param name="dependencies"></param>
        /// <param name="options"></param>
        /// <param name="target"></param>
        /// <param name="ignoreCase"></param>
        /// <returns>Returns full name of written masterfile; or null if not written</returns>
        void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, 
                    [NotNull] string options, [NotNull] WriteTarget target, bool ignoreCase);

        /// <summary>
        /// Render dependencies to stream for tests
        /// </summary>
        void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, [NotNull] Stream stream, [CanBeNull] string option);

        IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph renderingGraph);
    }
}