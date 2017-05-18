using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.Modifying {
    public class KeepOnlyDeps : ITransformer {
        private static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("keep");

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Keep only matching dependencies.

Configuration options: None

Transformer options: {Option.CreateHelp(DependencyMatchOptions.WithOptions(), detailedHelp, filter)}";
        }

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform([NotNull] GlobalContext globalContext,
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, string transformOptions,
            [NotNull] List<Dependency> transformedDependencies) {

            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();

            DependencyMatchOptions.Parse(globalContext, transformOptions, _ignoreCase, matches, excludes);

            transformedDependencies.AddRange(dependencies.Where(d => d.IsMatch(matches, excludes)));

            return Program.OK_RESULT;
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
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