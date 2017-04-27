using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Modifying {
    public class KeepOnlyDeps : ITransformer {
        private static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions();

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Keep only matching dependencies.

Configuration options: None

Transformer options: {Option.CreateHelp(DependencyMatchOptions.WithOptions(), detailedHelp, filter)}";
        }

        public bool RunsPerInputContext => true;

        public void Configure(GlobalContext globalContext, string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform(GlobalContext globalContext, string dependenciesFilename,
            IEnumerable<Dependency> dependencies, string transformOptions, string dependencySourceForLogging,
            List<Dependency> transformedDependencies) {

            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();

            DependencyMatchOptions.Parse(globalContext, transformOptions, _ignoreCase, matches, excludes);

            transformedDependencies.AddRange(dependencies.Where(d => d.IsMatch(matches, excludes)));

            return Program.OK_RESULT;
        }

        public void AfterAllTransforms(GlobalContext globalContext) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            Item a = Item.New(ItemType.SIMPLE, "A");
            Item b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source: null, markers: "", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source: null, markers: "use+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source: null, markers: "define", ct:5, questionableCt:0, badCt:2),
            };
        }
    }
}