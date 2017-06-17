using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public abstract class AbstractTransformerPerContainerUriWithFileConfiguration<TConfigurationPerContainer, TConfigureOptions, TTransformOptions>
            : AbstractTransformerWithFileConfiguration<TConfigurationPerContainer, TConfigureOptions, TTransformOptions> 
            where TConfigureOptions : new() {
        #region Transform

        public override int Transform([NotNull] GlobalContext globalContext, TConfigureOptions configureOptions, 
            [NotNull] TTransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, 
            [NotNull] List<Dependency> transformedDependencies) {

            IEnumerable<IGrouping<string, Dependency>> dependenciesByContainer = dependencies.GroupBy(d => d.Source?.ContainerUri);

            BeforeAllTransforms(globalContext, configureOptions, transformOptions, dependenciesByContainer.Select(g => g.Key));

            int result = Program.OK_RESULT;
            foreach (var container in dependenciesByContainer) {
                int r = TransformContainer(globalContext, configureOptions, transformOptions, container, container.Key, transformedDependencies);
                result = Math.Max(result, r);
            }

            AfterAllTransforms(globalContext, configureOptions, transformOptions);

            return result;
        }

        public abstract void BeforeAllTransforms([NotNull] GlobalContext globalContext, TConfigureOptions configureOptions, [NotNull] TTransformOptions transformOptions, IEnumerable<string> containerNames);

        public abstract int TransformContainer([NotNull] GlobalContext globalContext, TConfigureOptions configureOptions,
            [NotNull] TTransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, 
            string containerName, [NotNull] List<Dependency> transformedDependencies);

        public abstract void AfterAllTransforms([NotNull] GlobalContext globalContext, TConfigureOptions configureOptions, [NotNull] TTransformOptions transformOptions);

        #endregion Transform
    }
}