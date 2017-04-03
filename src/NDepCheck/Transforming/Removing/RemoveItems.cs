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
  -i      Ignore self cycles in sources and sinks
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
            bool ignoreSelfCyclesInSourcesAndSinks = false;
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
            }), new OptionAction('i', (args, j) => {
                ignoreSelfCyclesInSourcesAndSinks = true;
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

            MatrixDictionary<Item, int> aggregatedCounts = MatrixDictionary.CreateCounts(dependencies, d => d.Ct);

            // Force each item to exist on both axes
            foreach (var from in aggregatedCounts.RowKeys) {
                aggregatedCounts.GetColumnSum(from);
            }
            foreach (var to in aggregatedCounts.ColumnKeys) {
                aggregatedCounts.GetRowSum(to);
            }

            if (removeSinks) {
                Remove(aggregatedCounts, ac => ac.RowKeys, i => aggregatedCounts.GetRowSum(i), ignoreSelfCyclesInSourcesAndSinks, recursive);
            }
            if (removeSources) {
                Remove(aggregatedCounts, ac => ac.ColumnKeys, i => aggregatedCounts.GetColumnSum(i), ignoreSelfCyclesInSourcesAndSinks, recursive);
            }

            var remainingNodes = new HashSet<Item>(aggregatedCounts.RowKeys);
            remainingNodes.UnionWith(aggregatedCounts.ColumnKeys);

            if (match != null) {
                remainingNodes.RemoveWhere(i => match.Match(i));
            }

            transformedDependencies.AddRange(
                dependencies.Where(d => remainingNodes.Contains(d.UsingItem) && remainingNodes.Contains(d.UsedItem)));

            return Program.OK_RESULT;
        }

        private static void Remove(MatrixDictionary<Item, int> aggregatedCounts,
                                   Func<MatrixDictionary<Item, int>, IEnumerable<Item>> getKeys, 
                                   Func<Item, int> sum, bool ignoreSelfCyclesInSourcesAndSinks, bool recursive) {
            bool itemRemoved;
            do {
                itemRemoved = false;
                foreach (var i in getKeys(aggregatedCounts).ToArray()) {
                    if (sum(i) == (ignoreSelfCyclesInSourcesAndSinks ? aggregatedCounts.Get(i, i) : 0)) {
                        aggregatedCounts.RemoveRow(i);
                        aggregatedCounts.RemoveColumn(i);
                        itemRemoved = true;
                    }
                }
            } while (recursive && itemRemoved);
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            Item a = Item.New(ItemType.SIMPLE, "Ax");
            Item b = Item.New(ItemType.SIMPLE, "Bx");
            Item c = Item.New(ItemType.SIMPLE, "Cloop");
            Item d = Item.New(ItemType.SIMPLE, "Dloop");
            Item e = Item.New(ItemType.SIMPLE, "Eselfloop");
            Item f = Item.New(ItemType.SIMPLE, "Fy");
            Item g = Item.New(ItemType.SIMPLE, "Gy");
            Item h = Item.New(ItemType.SIMPLE, "Hy");
            Item i = Item.New(ItemType.SIMPLE, "Iy");
            Item j = Item.New(ItemType.SIMPLE, "Jy");
            return new[] {
                // Pure sources
                new Dependency(a, b, source: null, usage: null, ct: 10, questionableCt: 5, badCt: 3),
                new Dependency(b, c, source: null, usage: null, ct: 1, questionableCt: 0, badCt: 0),

                // Long cycle
                new Dependency(c, d, source: null, usage: null, ct: 5, questionableCt: 0, badCt: 2),
                new Dependency(d, c, source: null, usage: null, ct: 5, questionableCt: 0, badCt: 2),

                new Dependency(d, e, source: null, usage: null, ct: 5, questionableCt: 3, badCt: 2),
                // Self cycle
                new Dependency(e, e, source: null, usage: null, ct: 5, questionableCt: 3, badCt: 2),
                // Pure sinks
                new Dependency(e, f, source: null, usage: null, ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(f, g, source: null, usage: null, ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(g, h, source: null, usage: null, ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(h, i, source: null, usage: null, ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(h, j, source: null, usage: null, ct: 5, questionableCt: 3, badCt: 2)
            };
        }
    }
}
