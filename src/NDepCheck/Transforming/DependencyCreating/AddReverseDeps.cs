using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.DependencyCreating {
    public class AddReverseDeps : ITransformer {
        public static readonly Option DependencyMatchOption = new Option("dm", "dependency-match", "&", "Match to select dependencies to reverse", @default: "reverse all edges", multiple: true);
        public static readonly Option NoMatchOption = new Option("nm", "dont-match", "&", "Match to exclude dependencies", @default: "do not exclude any", multiple: true);

        public static readonly Option RemoveOriginalOption = new Option("ro", "remove-original", "", "If present, original dependency of a newly created reverse dependency is removed", @default:false);
        public static readonly Option MarkerToAddOption = new Option("ma", "marker-to-add", "&", "Marker added to newly created reverse dependencies", @default: "none");
        public static readonly Option IdempotentOption = new Option("ip", "idempotent", "", "Do not add if dependency with provided marker already exists", @default: false);

        private static readonly Option[] _transformOptions = {
            DependencyMatchOption, NoMatchOption,
            RemoveOriginalOption, MarkerToAddOption, IdempotentOption
        };

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Add reverse edges.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform(GlobalContext globalContext, [CanBeNull] string dependenciesFilename, IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();
            string markerToAdd = null;
            bool removeOriginal = false;
            bool idempotent = false;

            Option.Parse(globalContext, transformOptions,
                DependencyMatchOption.Action((args, j) => {
                    string pattern = Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency pattern", allowOptionValue: true);
                    matches.Add(DependencyMatch.Create(pattern, _ignoreCase));
                    return j;
                }),
                NoMatchOption.Action((args, j) => {
                    string pattern = Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency pattern", allowOptionValue: true);
                    excludes.Add(DependencyMatch.Create(pattern, _ignoreCase));
                    return j;
                }),
                IdempotentOption.Action((args, j) => {
                    idempotent = true;
                    return j;
                }),
                RemoveOriginalOption.Action((args, j) => {
                    removeOriginal = true;
                    return j;
                }), 
                MarkerToAddOption.Action((args, j) => {
                    markerToAdd = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim();
                    return j;
                }));

            DependencyPattern idempotentPattern = markerToAdd == null ? null : new DependencyPattern("'" + markerToAdd, _ignoreCase);
            Dictionary<FromTo, Dependency> fromTos = idempotent ? FromTo.AggregateAllEdges(dependencies) : null;

            foreach (var d in dependencies) {
                if (!removeOriginal) {
                    transformedDependencies.Add(d);
                }
                if (d.IsMatch(matches, excludes)) {
                    if (fromTos == null ||
                        !FromTo.ContainsMatchingDependency(fromTos, d.UsedItem, d.UsingItem, idempotentPattern)) {
                        var newDependency = new Dependency(d.UsedItem, d.UsingItem, d.Source, d.Markers, d.Ct,
                                                           d.QuestionableCt, d.BadCt, d.ExampleInfo, d.InputContext);
                        if (markerToAdd != null) {
                            newDependency.AddMarker(markerToAdd);
                        }
                        transformedDependencies.Add(newDependency);
                    }
                }
            }
            return Program.OK_RESULT;
        }

        public void AfterAllTransforms(GlobalContext globalContext) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source: null, markers: "inherit", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source: null, markers: "inherit+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source: null, markers: "define", ct:5, questionableCt:0, badCt:2),
                new Dependency(b, b, source: null, markers: "", ct: 5, questionableCt:0, badCt:2),
            };
        }
    }
}
