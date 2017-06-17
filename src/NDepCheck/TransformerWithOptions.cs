using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class TransformerWithOptions<TConfigureOptions, TTransformOptions> : ITransformer
        where TConfigureOptions : new() {
        private TConfigureOptions _configureOptions = new TConfigureOptions();

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptionsString,
            bool forceReload) {
            _configureOptions = CreateConfigureOptions(globalContext, configureOptionsString, forceReload);
        }

        protected abstract TConfigureOptions CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload);

        public int Transform([NotNull] GlobalContext globalContext,
            [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, [CanBeNull] string transformOptionsString,
            [NotNull] List<Dependency> transformedDependencies,
            Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            return Transform(globalContext, _configureOptions,
                CreateTransformOptions(globalContext, transformOptionsString, findOtherWorkingGraph),
                dependencies, transformedDependencies);
        }

        protected abstract TTransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph);

        public abstract int Transform([NotNull] GlobalContext globalContext, TConfigureOptions configureOptions,
            [NotNull] TTransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            [NotNull] List<Dependency> transformedDependencies);

        public abstract string GetHelp(bool detailedHelp, string filter);
        public abstract IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph);
    }
}