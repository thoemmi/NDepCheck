using System.Collections.Generic;

namespace NDepCheck.Transforming.Modifying {
    // TODO: INCOMPLETE BASE CLASS for transformers with dm and nm option

    public abstract class DependencyMatchingTransformer : ITransformer {
        public static readonly Option DependencyMatchOption = new Option("dm", "dependency-match", "&", "Match to select dependencies to traverse", @default: "traverse all dependencies", multiple: true);
        public static readonly Option NoMatchOption = new Option("nm", "no-match", "&", "Exclude from traversal ", @default: "no excluded dependencies", multiple: true);

        protected static readonly Option[] _matchingOptions = {
            DependencyMatchOption, NoMatchOption,
        };

        public abstract string GetHelp(bool detailedHelp, string filter);
        public abstract bool RunsPerInputContext { get; }
        public abstract void Configure(GlobalContext globalContext, string configureOptions, bool forceReload);

        public abstract int Transform(GlobalContext globalContext, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies);

        public abstract void AfterAllTransforms(GlobalContext globalContext);
        public abstract IEnumerable<Dependency> GetTestDependencies();
    }
}