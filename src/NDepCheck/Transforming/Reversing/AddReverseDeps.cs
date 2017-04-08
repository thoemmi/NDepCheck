using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Reversing {
    public class AddReverseDeps : ITransformer {
        public static readonly Option MatchOption = new Option("dm", "dependency-match", "&", "Match to select dependencies to reverse", @default: "reverse all edges", multiple: true);
        public static readonly Option RemoveOriginalOption = new Option("ro", "remove-original", "", "If present, original dependency of a newly created reverse dependency is removed", @default:false);
        public static readonly Option MarkerToAddOption = new Option("ma", "marker-to-add", "&", "Marker added to newly created reverse dependencies", @default: "none");

        private static readonly Option[] _transformOptions = { MatchOption, RemoveOriginalOption, MarkerToAddOption };

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp) {
            return $@"Add reverse edges.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp)}";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            // Only items are changed (Order is added)

            var matches = new List<DependencyMatch>();
            string markerToAdd = null;
            bool removeOriginal = false;

            Option.Parse(transformOptions,
                MatchOption.Action((args, j) => {
                    matches.Add(new DependencyMatch(Option.ExtractOptionValue(args, ref j), _ignoreCase));
                    return j;
                }), 
                RemoveOriginalOption.Action((args, j) => {
                    removeOriginal = true;
                    return j;
                }), 
                MarkerToAddOption.Action((args, j) => {
                    markerToAdd = Option.ExtractOptionValue(args, ref j).Trim('\'').Trim();
                    return j;
                }));

            foreach (var d in dependencies) {
                if (!removeOriginal) {
                    transformedDependencies.Add(d);
                }
                if (!matches.Any() || matches.Any(m => m.Matches(d))) {
                    var newDependency = new Dependency(d.UsedItem, d.UsingItem, d.Source,
                        d.Markers, d.Ct, d.QuestionableCt, d.BadCt, d.ExampleInfo, d.InputContext);
                    if (markerToAdd != null) {
                        newDependency.AddMarker(markerToAdd);
                    }
                    transformedDependencies.Add(newDependency);
                }
            }
            return Program.OK_RESULT;
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source: null, usage: "inherit", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source: null, usage: "inherit+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source: null, usage: "define", ct:5, questionableCt:0, badCt:2),
                new Dependency(b, b, source: null, usage: "", ct: 5, questionableCt:0, badCt:2),
            };
        }
    }
}
