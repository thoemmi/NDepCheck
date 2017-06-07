using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Ordering {
    public class AddItemOrder : ITransformer {
        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker prefix for order markers", @default: "#");
        public static readonly Option OrderByQuestionableCount = new Option("oq", "order-by-questionable", "", "Order by sum of questionable counts", @default: "Order by count");
        public static readonly Option OrderByBadCount = new Option("ob", "order-by-bad", "", "Order by sum of bad counts", @default: "Order by count");
        public static readonly Option OrderByIncomingValues = new Option("oi", "order-by-incoming", "", "Order by incoming values", @default: "Order by ratio incoming/(incoming+outgoing)");
        public static readonly Option OrderByOutgoingValues = new Option("oo", "order-by-outgoing", "", "Order by outgoing values", @default: "Order by ratio incoming/(incoming+outgoing)");

        private static readonly Option[] _allOptions = { OrderByBadCount, OrderByQuestionableCount, OrderByIncomingValues, OrderByOutgoingValues };

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Set the order property in each item for a bottom to top order. Order is set to a 4-digit integer number, starting at 0001

Configure options: None

Transform options: {Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            // empty
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies, 
            [CanBeNull] string transformOptions, [NotNull] List<Dependency> transformedDependencies, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {

            Func<int, int, decimal> getSortValue = (incoming, outgoing) => incoming / (incoming + outgoing + 0.0001m);
            Func<Dependency, int> orderBy = d => d.Ct;
            string orderMarkerPrefix = "_";

            Option.Parse(globalContext, transformOptions,
                OrderByBadCount.Action((args, j) => {
                    orderBy = d => d.BadCt;
                    return j;
                }),
                OrderByQuestionableCount.Action((args, j) => {
                    orderBy = d => d.QuestionableCt;
                    return j;
                }),
                OrderByIncomingValues.Action((args, j) => {
                    getSortValue = (incoming, outgoing) => incoming;
                    return j;
                }),
                OrderByOutgoingValues.Action((args, j) => {
                    getSortValue = (incoming, outgoing) => outgoing;
                    return j;
                }), AddMarkerOption.Action((args, j) => {
                    orderMarkerPrefix = Option.ExtractRequiredOptionValue(args, ref j, "missing marker prefix").Trim('\'').Trim();
                    return j;
                }));

            // Only items are changed (Order is added)
            transformedDependencies.AddRange(dependencies);

            // Algorithm: 
            // LOOP
            //    Find the item with the least outgoing edges (measured relative to outgoing and ingoing ones)
            //    Put it into result list; and move edges to it from consideration
            // UNTIL list of items is empty

            MatrixDictionary<Item, int> aggregatedCounts =
                MatrixDictionary.CreateCounts(dependencies.Where(d => !Equals(d.UsingItem, d.UsedItem)), orderBy, globalContext.CurrentGraph);

            for (int i = 0; aggregatedCounts.ColumnKeys.Any(); i++) {
                var itemsToSortValues =
                    aggregatedCounts.ColumnKeys.Select(
                        k => new {
                            Item = k,
                            Value = getSortValue(aggregatedCounts.GetRowSum(k), aggregatedCounts.GetColumnSum(k))
                        });
                decimal minToRatio = itemsToSortValues.Min(ir => ir.Value);
                Item minItem = itemsToSortValues.First(ir => ir.Value == minToRatio).Item;

                aggregatedCounts.RemoveColumn(minItem);

                minItem.IncrementMarker(orderMarkerPrefix + i.ToString("D4"));
            }
            return Program.OK_RESULT;
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            var a = transformingGraph.CreateItem(ItemType.SIMPLE, "A");
            var b = transformingGraph.CreateItem(ItemType.SIMPLE, "B");
            var c = transformingGraph.CreateItem(ItemType.SIMPLE, "C");
            var d = transformingGraph.CreateItem(ItemType.SIMPLE, "D");
            //    |   A   B   C   D #
            // ---------------------#----
            //  A |  15 100   0   . # 115
            //  B |   3  20 101   . # 124
            //  C |   7   5  30   . #  42
            //  D |   .   2 300   . # 302
            //  =========================
            //    |  25 127 431   0 #
            return new[] {
                transformingGraph.CreateDependency(a, a, source: null, markers: "", ct:10),
                transformingGraph.CreateDependency(a, a, source: null, markers: "", ct:5),

                transformingGraph.CreateDependency(a, b, source: null, markers: "", ct:100),

                transformingGraph.CreateDependency(b, a, source: null, markers: "", ct:1),
                transformingGraph.CreateDependency(b, a, source: null, markers: "", ct:1),
                transformingGraph.CreateDependency(b, a, source: null, markers: "", ct:1),

                transformingGraph.CreateDependency(b, b, source: null, markers: "", ct:20),

                transformingGraph.CreateDependency(b, c, source: null, markers: "", ct:100),
                transformingGraph.CreateDependency(b, c, source: null, markers: "", ct:1),

                transformingGraph.CreateDependency(c, a, source: null, markers: "", ct:7),
                transformingGraph.CreateDependency(c, b, source: null, markers: "", ct:5),
                transformingGraph.CreateDependency(c, c, source: null, markers: "", ct:30),

                transformingGraph.CreateDependency(d, b, source: null, markers: "", ct:1),
                transformingGraph.CreateDependency(d, b, source: null, markers: "", ct:1),
                transformingGraph.CreateDependency(d, c, source: null, markers: "", ct:300),
            };
        }
    }
}
