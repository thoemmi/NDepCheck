using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Modifying {
    public class KeepOnlyDeps : DependencyMatchingTransformer {
        private bool _ignoreCase;

        public override string GetHelp(bool detailedHelp, string filter) {
            return $@"Keep only matching dependencies.

Configuration options: None

Transformer options: {Option.CreateHelp(_matchingOptions, detailedHelp, filter)}";
        }

        public override bool RunsPerInputContext => true;

        public override void Configure(GlobalContext globalContext, string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public override int Transform(GlobalContext globalContext, string dependenciesFilename,
            IEnumerable<Dependency> dependencies, string transformOptions, string dependencySourceForLogging,
            List<Dependency> transformedDependencies) {
            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();

            Option.Parse(globalContext, transformOptions, DependencyMatchOption.Action((args, j) => {
                string pattern = Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency pattern",
                    allowOptionValue: true);
                matches.Add(DependencyMatch.Create(pattern, _ignoreCase));
                return j;
            }), NoMatchOption.Action((args, j) => {
                string pattern = Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency pattern",
                    allowOptionValue: true);
                excludes.Add(DependencyMatch.Create(pattern, _ignoreCase));
                return j;
            }));

            transformedDependencies.AddRange(dependencies.Where(d => d.IsMatch(matches, excludes)));

            return Program.OK_RESULT;
        }

        public override void AfterAllTransforms(GlobalContext globalContext) {
            // empty
        }

        public override IEnumerable<Dependency> GetTestDependencies() {
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