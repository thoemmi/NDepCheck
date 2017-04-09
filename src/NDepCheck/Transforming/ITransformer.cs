using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public interface ITransformer : IPlugin {
        bool RunsPerInputContext { get; }

        void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload);

        int Transform([NotNull] GlobalContext globalContext, [NotNull] string dependenciesFilename, 
                      [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, 
                      [CanBeNull] string transformOptions, [NotNull] string dependencySourceForLogging, 
                      [NotNull] List<Dependency> transformedDependencies);

        void FinishTransform([NotNull] GlobalContext context);

        [NotNull]
        IEnumerable<Dependency> GetTestDependencies();
    }
}
