using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public interface ITransformer : IPlugin {
        bool RunsPerInputContext { get; }

        void Configure(GlobalContext globalContext, [NotNull] string configureOptions);

        int Transform([NotNull] GlobalContext context, string dependenciesFilename, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, [NotNull] string transformOptions, [NotNull] string dependencySourceForLogging, [NotNull] List<Dependency> transformedDependencies);

        void FinishTransform([NotNull] GlobalContext context);

        [NotNull]
        IEnumerable<Dependency> GetTestDependencies();
    }
}
