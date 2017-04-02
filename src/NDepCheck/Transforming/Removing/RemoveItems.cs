using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Removing {
    public class RemoveItems : ITransformer {
        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp) {
            return @"Remove nodes - UNTESTED.

Configuration options: None

Transformer options: [-m typename pattern] [-s] [-d] [-r]
  -m      Regular expression matching node to remove; default: match none
  -s      Remove pure sources
  -d      Remove pure sinks (drains)
  -r      Recursively continue with removal if new eligible nodes emerge
";
        }

        public bool RunsPerInputContext => true;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            ItemMatch match = null;
            bool removeSources = false;
            bool removeSinks = false;
            bool recursive = false;

            Options.Parse(transformOptions, new OptionAction('m', (args, j) => {
                string itemTypeName = Options.ExtractOptionValue(args, ref j);
                string itemPattern = Options.ExtractNextValue(args, ref j);
                ItemType itemType = ItemType.Find(itemTypeName);
                if (itemType == null) {
                    throw new ArgumentException($"Cannot find type {itemTypeName}");
                }
                match = new ItemMatch(itemType, itemPattern, _ignoreCase);
                return j;
            }), new OptionAction('r', (args, j) => {
                recursive = true;
                return j;
            }), new OptionAction('s', (args, j) => {
                removeSources = true;
                return j;
            }), new OptionAction('d', (args, j) => {
                removeSinks = true;
                return j;
            }));

            MatrixDictionary<Item, int> aggregatedCounts =
                MatrixDictionary.CreateCounts(dependencies.Where(d => !Equals(d.UsingItem, d.UsedItem)), d => d.Ct);

            if (removeSinks) {
                bool foundSink;
                do {
                    foundSink = false;
                    foreach (var i in aggregatedCounts.FromKeys.ToArray()) {
                        if (aggregatedCounts.GetFromSum(i) == 0) {
                            aggregatedCounts.RemoveFrom(i);
                            foundSink = true;
                        }
                    }
                } while (recursive && foundSink);
            }
            if (removeSources) {
                bool foundSource;
                do {
                    foundSource = false;
                    foreach (var i in aggregatedCounts.ToKeys.ToArray()) {
                        if (aggregatedCounts.GetToSum(i) == 0) {
                            aggregatedCounts.RemoveTo(i);
                            foundSource = true;
                        }
                    }
                } while (recursive && foundSource);
            }

            var remainingNodes = new HashSet<Item>(aggregatedCounts.FromKeys);
            remainingNodes.UnionWith(aggregatedCounts.ToKeys);

            if (match != null) {
                remainingNodes.RemoveWhere(i => match.Match(i));
            }

            transformedDependencies.AddRange(
                dependencies.Where(d => remainingNodes.Contains(d.UsingItem) && remainingNodes.Contains(d.UsedItem)));

            return Program.OK_RESULT;
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source:null, usage: "inherit", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source:null, usage: "inherit+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source:null, usage: "define", ct:5, questionableCt:0, badCt:2),
                new Dependency(b, b, source:null, usage: null, ct: 5, questionableCt:0, badCt:2),
            };
        }
    }
}
