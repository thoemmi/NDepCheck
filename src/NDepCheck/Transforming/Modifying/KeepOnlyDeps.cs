using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.Modifying {
    public class KeepOnlyDeps : TransformerWithOptions<Ignore, KeepOnlyDeps.TransformOptions> {
        public class TransformOptions {
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Matches = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> Excludes = new List<DependencyMatch>();
        }

        private static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("keep");

        public override string GetHelp(bool detailedHelp, string filter) {
            return $@"Keep only matching dependencies.

Configuration options: None

Transformer options: {Option.CreateHelp(DependencyMatchOptions.WithOptions(), detailedHelp, filter)}";
        }

        protected override Ignore CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            return Ignore.Om;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext, 
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var transformOptions = new TransformOptions();

            DependencyMatchOptions.Parse(globalContext, transformOptionsString, globalContext.IgnoreCase,
                                         transformOptions.Matches, transformOptions.Excludes);
            return transformOptions;
        }

        public override int Transform([NotNull] GlobalContext globalContext, Ignore Ignore, 
            [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            [NotNull] List<Dependency> transformedDependencies) {
            transformedDependencies.AddRange(
                dependencies.Where(d => d.IsMarkerMatch(transformOptions.Matches, transformOptions.Excludes)));

            return Program.OK_RESULT;
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            Item a = transformingGraph.CreateItem(ItemType.SIMPLE, "A");
            Item b = transformingGraph.CreateItem(ItemType.SIMPLE, "B");
            return new[] {
                transformingGraph.CreateDependency(a, a, source: null, markers: "", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),
                transformingGraph.CreateDependency(a, b, source: null, markers: "use+define", ct:1, questionableCt:0,badCt: 0),
                transformingGraph.CreateDependency(b, a, source: null, markers: "define", ct:5, questionableCt:0, badCt:2, notOkReason: "test"),
            };
        }
    }
}