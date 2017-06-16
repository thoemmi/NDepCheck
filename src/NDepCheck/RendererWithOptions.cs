using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class RendererWithOptions<TRenderOptions> : IRenderer {
        public void Render([NotNull] GlobalContext globalContext,
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, [NotNull] string options,
            [NotNull] WriteTarget target, bool ignoreCase) {
            Render(globalContext, dependencies, CreateRenderOptions(globalContext, options), target, ignoreCase);
        }

        protected abstract TRenderOptions CreateRenderOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string options);

        public abstract void Render([NotNull] GlobalContext globalContext, 
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            TRenderOptions options, [NotNull] WriteTarget target, bool ignoreCase);

        public abstract string GetHelp(bool detailedHelp, string filter);
        public abstract IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph);

        public WriteTarget GetMasterFileName(GlobalContext globalContext, string optionsString, WriteTarget baseTarget) {
            return GetMasterFileName(globalContext, CreateRenderOptions(globalContext, optionsString), baseTarget);
        }

        public abstract WriteTarget GetMasterFileName(GlobalContext globalContext, TRenderOptions options,
            WriteTarget baseTarget);

        public abstract void RenderToStreamForUnitTests(GlobalContext globalContext, IEnumerable<Dependency> dependencies, Stream stream, string option);
    }
}