using System.Collections.Generic;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.DependencyCreating {
    public class AddReverseDeps : ITransformer {
        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("reverse");

        public static readonly Option RemoveOriginalOption = new Option("ro", "remove-original", "", "If present, original dependency of a newly created reverse dependency is removed", @default:false);
        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker added to newly created reverse dependencies", @default: "none");
        public static readonly Option IdempotentOption = new Option("ip", "idempotent", "", "Do not add if dependency with provided marker already exists", @default: false);

        private static readonly Option[] _transformOptions = DependencyMatchOptions.WithOptions(
            RemoveOriginalOption, AddMarkerOption, IdempotentOption
        );

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Add reverse edges.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, [NotNull] List<Dependency> transformedDependencies) {

            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();
            string markerToAdd = null;
            bool removeOriginal = false;
            bool idempotent = false;

            DependencyMatchOptions.Parse(globalContext, transformOptions, _ignoreCase, matches, excludes,
                IdempotentOption.Action((args, j) => {
                    idempotent = true;
                    return j;
                }),
                RemoveOriginalOption.Action((args, j) => {
                    removeOriginal = true;
                    return j;
                }), 
                AddMarkerOption.Action((args, j) => {
                    markerToAdd = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim();
                    return j;
                }));

            DependencyPattern idempotentPattern = markerToAdd == null ? null : new DependencyPattern("'" + markerToAdd, _ignoreCase);
            Dictionary<FromTo, Dependency> fromTos = idempotent ? FromTo.AggregateAllDependencies(dependencies) : null;

            int added = 0;
            int removed = 0;
            foreach (var d in dependencies) {
                if (!removeOriginal) {
                    transformedDependencies.Add(d);
                } else {
                    removed++;
                }
                if (d.IsMatch(matches, excludes)) {
                    if (fromTos == null ||
                        !FromTo.ContainsMatchingDependency(fromTos, d.UsedItem, d.UsingItem, idempotentPattern)) {
                        var newDependency = new Dependency(d.UsedItem, d.UsingItem, d.Source, d.MarkerSet, d.Ct,
                                                           d.QuestionableCt, d.BadCt, d.ExampleInfo);
                        if (markerToAdd != null) {
                            newDependency.IncrementMarker(markerToAdd);
                        }
                        transformedDependencies.Add(newDependency);
                        added++;
                    }
                }
            }
            Log.WriteInfo($"... added {added}{(removed > 0 ? " removed " + removed : "")} dependencies");
            return Program.OK_RESULT;
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
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
