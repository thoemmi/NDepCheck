using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.DependencyCreating {
    public class AddReverseDeps : TransformerWithOptions<Ignore, AddReverseDeps.TransformOptions> {
        public class TransformOptions {
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Matches = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Excludes = new List<DependencyMatch>();
            [CanBeNull]
            public string MarkerToAdd;
            public bool RemoveOriginal;
            public bool Idempotent;
        }

        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("reverse");

        public static readonly Option RemoveOriginalOption = new Option("ro", "remove-original", "", "If present, original dependency of a newly created reverse dependency is removed", @default: false);
        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker added to newly created reverse dependencies", @default: "none");
        public static readonly Option IdempotentOption = new Option("ip", "idempotent", "", "Do not add if dependency with provided marker already exists", @default: false);

        private static readonly Option[] _transformOptions = DependencyMatchOptions.WithOptions(
            RemoveOriginalOption, AddMarkerOption, IdempotentOption
        );

        public override string GetHelp(bool detailedHelp, string filter) {
            return $@"Add reverse edges.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        protected override Ignore CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            return Ignore.Om;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext, 
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var transformOptions = new TransformOptions();

            DependencyMatchOptions.Parse(globalContext, transformOptionsString, globalContext.IgnoreCase, transformOptions.Matches, transformOptions.Excludes,
                IdempotentOption.Action((args, j) => {
                    transformOptions.Idempotent = true;
                    return j;
                }),
                RemoveOriginalOption.Action((args, j) => {
                    transformOptions.RemoveOriginal = true;
                    return j;
                }),
                AddMarkerOption.Action((args, j) => {
                    transformOptions.MarkerToAdd = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim();
                    return j;
                }));

            return transformOptions;
        }

        public override int Transform([NotNull] GlobalContext globalContext, Ignore Ignore, 
            [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            [NotNull] List<Dependency> transformedDependencies) {
            DependencyPattern idempotentPattern = transformOptions.MarkerToAdd == null ? null : new DependencyPattern("'" + transformOptions.MarkerToAdd, globalContext.IgnoreCase);
            Dictionary<FromTo, Dependency> fromTos = transformOptions.Idempotent ? FromTo.AggregateAllDependencies(globalContext.CurrentGraph, dependencies) : null;

            int added = 0;
            int removed = 0;
            foreach (var d in dependencies) {
                if (!transformOptions.RemoveOriginal) {
                    transformedDependencies.Add(d);
                } else {
                    removed++;
                }
                if (d.IsMarkerMatch(transformOptions.Matches, transformOptions.Excludes)) {
                    if (fromTos == null ||
                        !FromTo.ContainsMatchingDependency(fromTos, d.UsedItem, d.UsingItem, idempotentPattern)) {
                        var newDependency = globalContext.CurrentGraph.CreateDependency(d.UsedItem, d.UsingItem, d.Source, d.MarkerSet, d.Ct,
                                                           d.QuestionableCt, d.BadCt, d.NotOkReason, d.ExampleInfo);
                        if (transformOptions.MarkerToAdd != null) {
                            newDependency.IncrementMarker(transformOptions.MarkerToAdd);
                        }
                        transformedDependencies.Add(newDependency);
                        added++;
                    }
                }
            }
            Log.WriteInfo($"... added {added}{(removed > 0 ? " removed " + removed : "")} dependencies");
            return Program.OK_RESULT;
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            var a = transformingGraph.CreateItem(ItemType.SIMPLE, "A");
            var b = transformingGraph.CreateItem(ItemType.SIMPLE, "B");
            return new[] {
                transformingGraph.CreateDependency(a, a, source: null, markers: "inherit", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),
                transformingGraph.CreateDependency(a, b, source: null, markers: "inherit+define", ct:1, questionableCt:0,badCt: 0),
                transformingGraph.CreateDependency(b, a, source: null, markers: "define", ct:5, questionableCt:0, badCt:2, notOkReason: "test"),
                transformingGraph.CreateDependency(b, b, source: null, markers: "", ct: 5, questionableCt:0, badCt:2, notOkReason: "test"),
            };
        }
    }
}
