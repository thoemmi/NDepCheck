using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.SpecialItemMarking {
    public class MarkSpecialItems : ITransformer {
        public static readonly Option MatchOption = new Option("im", "item-match", "&", "Match to select items to check", @default: "select all", multiple: true);
        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker added to identified items", @default: null);
        public static readonly Option RecursiveMarkOption = new Option("mr", "mark-recursively", "", "Repeat marking", @default: false);
        public static readonly Option MarkSinksOption = new Option("md", "mark-drains", "", "Marks sinks (or drains)", @default: false);
        public static readonly Option MarkSourcesOption = new Option("ms", "mark-sources", "", "Mark sources", @default: false);
        public static readonly Option IgnoreSingleCyclesOption = new Option("ii", "ignore-single-loops", "", "Ignore single cycles for source and sink detection", @default: false);
        public static readonly Option MarkSingleCyclesOption = new Option("mi", "mark-single-loops", "", "Mark single cycles", @default: false);

        private static readonly Option[] _transformOptions = {
            MatchOption, AddMarkerOption, RecursiveMarkOption,
            MarkSinksOption, MarkSourcesOption, IgnoreSingleCyclesOption, MarkSingleCyclesOption
        };

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Mark items with special properties.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            string transformOptions, [NotNull] List<Dependency> transformedDependencies) {

            var matches = new List<ItemMatch>();
            bool markSources = false;
            bool markSinks = false;
            bool ignoreSelfCyclesInSourcesAndSinks = false;
            bool markSingleCycleNodes = false;
            bool recursive = false;
            string markerToAdd = null;

            Option.Parse(globalContext, transformOptions,
                MatchOption.Action((args, j) => {
                    matches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "missing match definition"), _ignoreCase));
                    return j;
                }), MarkSingleCyclesOption.Action((args, j) => {
                    markSingleCycleNodes = true;
                    return j;
                }), IgnoreSingleCyclesOption.Action((args, j) => {
                    ignoreSelfCyclesInSourcesAndSinks = true;
                    return j;
                }), RecursiveMarkOption.Action((args, j) => {
                    recursive = true;
                    return j;
                }), MarkSourcesOption.Action((args, j) => {
                    markSources = true;
                    return j;
                }), MarkSinksOption.Action((args, j) => {
                    markSinks = true;
                    return j;
                }), AddMarkerOption.Action((args, j) => {
                    markerToAdd = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim();
                    return j;
                }));

            Dependency[] matchingDependencies = dependencies
                .Where(d => !matches.Any()
                        || matches.Any(m => ItemMatch.IsMatch(m, d.UsingItem)) && matches.Any(m => ItemMatch.IsMatch(m, d.UsedItem)))
                .ToArray();

            MatrixDictionary<Item, int> aggregatedCounts = MatrixDictionary.CreateCounts(matchingDependencies, d => d.Ct);

            // Force each item to exist on both matrix axes
            foreach (var from in aggregatedCounts.RowKeys) {
                aggregatedCounts.GetColumnSum(from);
            }
            foreach (var to in aggregatedCounts.ColumnKeys) {
                aggregatedCounts.GetRowSum(to);
            }

            // aggregatedCounts is used destructively in both following Mark runs; this is no problem,
            // because no sink can also be a source in NDepCheck: Nodes without any edges do not
            // appear; and nodes with only self cycles are either not a source and sink (if ignoreSelfCycles=false),
            // or they are both a source and a sink and hence are found in the first Mark run.

            if (markSinks) {
                Mark(aggregatedCounts, ac => ac.RowKeys, i => aggregatedCounts.GetRowSum(i),
                    ignoreSelfCyclesInSourcesAndSinks, recursive, markerToAdd);
            }
            if (markSources) {
                Mark(aggregatedCounts, ac => ac.ColumnKeys, i => aggregatedCounts.GetColumnSum(i),
                    ignoreSelfCyclesInSourcesAndSinks, recursive, markerToAdd);
            }

            var remainingNodes = new HashSet<Item>(aggregatedCounts.RowKeys);
            remainingNodes.UnionWith(aggregatedCounts.ColumnKeys);

            if (markSingleCycleNodes) {
                foreach (var d in matchingDependencies) {
                    if (Equals(d.UsingItem, d.UsedItem)) {
                        d.UsingItem.IncrementMarker(markerToAdd);
                    }
                }
            }

            transformedDependencies.AddRange(dependencies);

            return Program.OK_RESULT;
        }

        private static void Mark(MatrixDictionary<Item, int> aggregatedCounts,
                                   Func<MatrixDictionary<Item, int>, IEnumerable<Item>> getKeys,
                                   Func<Item, int> sum, bool ignoreSelfCyclesInSourcesAndSinks, bool recursive, string markerToAdd) {
            bool itemRemoved;
            do {
                itemRemoved = false;
                foreach (var i in getKeys(aggregatedCounts).ToArray()) {
                    if (sum(i) == (ignoreSelfCyclesInSourcesAndSinks ? aggregatedCounts.Get(i, i) : 0)) {
                        aggregatedCounts.RemoveRow(i);
                        aggregatedCounts.RemoveColumn(i);
                        i.IncrementMarker(markerToAdd);
                        itemRemoved = true;
                    }
                }
            } while (recursive && itemRemoved);
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
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
                new Dependency(a, b, source: null, markers: "", ct: 10, questionableCt: 5, badCt: 3),
                new Dependency(b, c, source: null, markers: "", ct: 1, questionableCt: 0, badCt: 0),

                // Long cycle
                new Dependency(c, d, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2),
                new Dependency(d, c, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2),

                new Dependency(d, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                // Self cycle
                new Dependency(e, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                // Pure sinks
                new Dependency(e, f, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(f, g, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(g, h, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(h, i, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2),
                new Dependency(h, j, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2)
            };
        }
    }
}
