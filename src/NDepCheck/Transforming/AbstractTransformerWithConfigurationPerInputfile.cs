using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public abstract class AbstractTransformerPerContainerUriWithFileConfiguration<TConfigurationPerContainer>
            : AbstractTransformerWithFileConfiguration<TConfigurationPerContainer> {
        #region Transform

        public override int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, [NotNull] List<Dependency> transformedDependencies) {

            BeforeAllTransforms(globalContext, transformOptions);

            IEnumerable<IGrouping<string, Dependency>> dependenciesByContainer = dependencies.GroupBy(d => d.Source?.ContainerUri);
            int result = Program.OK_RESULT;
            foreach (var container in dependenciesByContainer) {
                int r = TransformContainer(globalContext, container, container.Key, transformedDependencies);
                result = Math.Max(result, r);
            }

            AfterAllTransforms(globalContext);

            return result;
        }

        public abstract void BeforeAllTransforms([NotNull] GlobalContext globalContext, [CanBeNull] string transformOptions);

        public abstract int TransformContainer([NotNull] GlobalContext globalContext,
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, string containerName, [NotNull] List<Dependency> transformedDependencies);

        public abstract void AfterAllTransforms([NotNull] GlobalContext globalContext);

        #endregion Transform
    }
}