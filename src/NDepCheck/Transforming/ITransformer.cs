using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public interface ITransformer : IPlugin {
        int Transform([NotNull] GlobalContext context, string dependenciesFileName, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, [NotNull] string transformOptions, [NotNull] string dependencySourceForLogging, [NotNull] Dictionary<FromTo, Dependency> newDependenciesCollector);

        [NotNull]
        IEnumerable<Dependency> GetTestDependencies();

        void FinishTransform([NotNull] GlobalContext context);

        bool RunsPerInputContext { get; }
        void Configure(GlobalContext globalContext, [NotNull] string configureOptions);
    }
}
