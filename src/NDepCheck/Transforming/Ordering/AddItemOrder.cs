using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Ordering {
    public class AddItemOrder : ITransformer {
        public string GetHelp(bool detailedHelp) {
            return
                @"Adds a field to each item for a bottom to top order. The field is a 4-digit integer number, starting at 0001";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            // empty
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies, 
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            // Only items are changed (Order is added)
            transformedDependencies.AddRange(dependencies);

            // Algorithm: 
            // LOOP
            //    Find the item with the least outgoing edges (measured relative to outgoing and ingoing ones)
            //    Put it into result list; and move edges to it from consideration
            // UNTIL list of items is empty

            MatrixDictionary<Item, int> aggregatedCounts = MatrixDictionary.CreateCounts(dependencies.Where(d => !Equals(d.UsingItem, d.UsedItem)), d => d.Ct);

            for (int i = 0; aggregatedCounts.ColumnKeys.Any(); i++) {
                var itemsToRatios =
                    aggregatedCounts.ColumnKeys.Select(
                        k => new {
                            Item = k,
                            Ratio = aggregatedCounts.GetRowSum(k) / (aggregatedCounts.GetColumnSum(k) + aggregatedCounts.GetRowSum(k) + 0.001m)
                        });
                decimal minToRatio = itemsToRatios.Min(ir => ir.Ratio);
                Item minItem = itemsToRatios.First(ir => ir.Ratio == minToRatio).Item;

                aggregatedCounts.RemoveColumn(minItem);

                minItem.SetOrder(i.ToString("D4"));
            }
            return Program.OK_RESULT;
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            var c = Item.New(ItemType.SIMPLE, "C");
            var d = Item.New(ItemType.SIMPLE, "D");
            //    |   A   B   C   D #
            // ---------------------#----
            //  A |  15 100   0   . # 115
            //  B |   3  20 101   . # 124
            //  C |   7   5  30   . #  42
            //  D |   .   2 300   . # 302
            //  =========================
            //    |  25 127 431   0 #
            return new[] {
                new Dependency(a, a, source: null, markers: "", ct:10),
                new Dependency(a, a, source: null, markers: "", ct:5),

                new Dependency(a, b, source: null, markers: "", ct:100),

                new Dependency(b, a, source: null, markers: "", ct:1),
                new Dependency(b, a, source: null, markers: "", ct:1),
                new Dependency(b, a, source: null, markers: "", ct:1),

                new Dependency(b, b, source: null, markers: "", ct:20),

                new Dependency(b, c, source: null, markers: "", ct:100),
                new Dependency(b, c, source: null, markers: "", ct:1),

                new Dependency(c, a, source: null, markers: "", ct:7),
                new Dependency(c, b, source: null, markers: "", ct:5),
                new Dependency(c, c, source: null, markers: "", ct:30),

                new Dependency(d, b, source: null, markers: "", ct:1),
                new Dependency(d, b, source: null, markers: "", ct:1),
                new Dependency(d, c, source: null, markers: "", ct:300),
            };
        }
    }
}
