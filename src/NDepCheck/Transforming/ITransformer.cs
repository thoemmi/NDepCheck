using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public interface ITransformer : IPlugin {
        bool RunsPerInputContext { get; }

        void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload);

        int Transform([NotNull] GlobalContext globalContext, [CanBeNull] string dependenciesFilename, 
                      [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, 
                      [CanBeNull] string transformOptions, [NotNull] string dependencySourceForLogging, 
                      [NotNull] List<Dependency> transformedDependencies);

        void AfterAllTransforms([NotNull] GlobalContext globalContext);

        [NotNull]
        IEnumerable<Dependency> GetTestDependencies();
    }
}
