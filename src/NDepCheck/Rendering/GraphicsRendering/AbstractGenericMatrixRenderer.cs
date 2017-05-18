using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.GraphicsRendering {
    public abstract class AbstractMatrixRenderer : IRenderer {
        public static readonly Option MaxNameWidthOption = new Option("mw", "max-name-width", "#", "Maximal width of an item name", @default: "full length of name");
        public static readonly Option WriteBadCountOption = new Option("wb", "write-bad-count", "", "Also output count of bad dependencies", @default: false);
        public static readonly Option InnerMatchOption = new Option("im", "inner-item", "#", "Match to mark item as inner item", @default: "all items are inner");

        private static readonly Option[] _allOptions = { MaxNameWidthOption, WriteBadCountOption, InnerMatchOption };

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
            return RendererSupport.CreateSomeTestItems();
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Write a textual matrix representation of dependencies.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        protected static void ParseOptions([NotNull] GlobalContext globalContext, string argsAsString, bool ignoreCase, 
                                           out int? labelWidthOrNull, out bool withNotOkCt, out ItemMatch innerMatch) {
            int? lw = null;
            bool wct = false;
            ItemMatch im = null;
            Option.Parse(globalContext, argsAsString,
                MaxNameWidthOption.Action((args, j) => {
                    lw = Option.ExtractIntOptionValue(args, ref j, "");
                    return j;
                }),
                WriteBadCountOption.Action((args, j) => {
                    wct = true;
                    return j;
                }),
                InnerMatchOption.Action((args, j) => {
                    im = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing pattern for inner match"), ignoreCase);
                    return j;
                }));
            labelWidthOrNull = lw;
            withNotOkCt = wct;
            innerMatch = im;
        }

        private static List<Item> MoreOrLessTopologicalSort(IEnumerable<Dependency> edges) {
            Dictionary<Item, List<Dependency>> dependencies2ItemsAndDependencies = Dependency.Dependencies2ItemsAndDependenciesList(edges);

            var result = new List<Item>();
            var resultAsHashSet = new HashSet<Item>();
            var candidates = new List<Item>(dependencies2ItemsAndDependencies.Keys.OrderBy(n => n.Name));

            while (candidates.Any()) {

                Item best = FindBest(candidates, true, dependencies2ItemsAndDependencies) ?? FindBest(candidates, false, dependencies2ItemsAndDependencies);

                if (best == null) {
                    throw new Exception("Error in algorithm - no best candidate found");
                }
                result.Add(best);
                resultAsHashSet.Add(best);

                candidates.Remove(best);
                foreach (var es in dependencies2ItemsAndDependencies.Values) {
                    es.RemoveAll(e => e.UsedItem.Equals(best));
                }
            }
            result.Reverse();
            return result;
        }

        private static Item FindBest(List<Item> candidates, bool skipOutgoing, Dictionary<Item, List<Dependency>> itemsAndDependencies) {
            var minimalIncomingNotOkWeight = int.MaxValue;
            var minimalLeavingWeight = int.MaxValue;
            Item result = null;
            foreach (var n in candidates) {
                int leavingWeight = itemsAndDependencies[n].Where(e => !e.UsedItem.Equals(n)).Sum(e => e.Ct);

                if (skipOutgoing) {
                    if (leavingWeight > 0) {
                        continue;
                    }
                }

                int incomingNotOkWeight = candidates.Where(c => !c.Equals(n)).Sum(c => itemsAndDependencies[c].Where(e => e.UsedItem.Equals(n)).Sum(e => e.NotOkCt));

                if (leavingWeight < minimalLeavingWeight
                    || leavingWeight == minimalLeavingWeight && incomingNotOkWeight < minimalIncomingNotOkWeight) {
                    minimalIncomingNotOkWeight = incomingNotOkWeight;
                    minimalLeavingWeight = leavingWeight;
                    result = n;
                }
            }
            return result;
        }

        protected static string Limit(string s, int lg) {
            return (s + Repeat(' ', lg)).Substring(0, lg);
        }

        protected static string Repeat(char c, int lg) {
            return string.Join("", Enumerable.Repeat(c, lg));
        }

        protected static string GetItemId(Item n, string itemFormat, Dictionary<Item, int> item2Index) {
            return string.Format(itemFormat, item2Index[n]);
        }

        protected static string FormatCt(bool withNotOkCt, string ctFormat, bool rightUpper, IWithCt e) {
            return (e.Ct > 0 ? string.Format(ctFormat, rightUpper ? '#' : ' ', e.Ct)
                       : string.Format(ctFormat, ' ', 0))
                   + (withNotOkCt ? ";" + (e.NotOkCt > 0 ? string.Format(ctFormat, rightUpper ? '*' : '~', e.NotOkCt)
                                        : string.Format(ctFormat, ' ', 0))
                       : "");
        }

        protected void Render(IEnumerable<Dependency> edges, ItemMatch innerMatchOrNull,
            [NotNull] ITargetWriter output, int? labelWidthOrNull, bool withNotOkCt) {
            IDictionary<Item, IEnumerable<Dependency>> itemsAndDependencies = Dependency.Dependencies2ItemsAndDependencies(edges);

            var innerAndReachableOuterItems =
                new HashSet<Item>(itemsAndDependencies.Where(n => ItemMatch.IsMatch(innerMatchOrNull, n.Key)).SelectMany(kvp => new[] { kvp.Key }.Concat(kvp.Value.Select(e => e.UsedItem))));

            IEnumerable<Item> sortedItems = MoreOrLessTopologicalSort(edges).Where(n => innerAndReachableOuterItems.Contains(n));

            if (sortedItems.Any()) {

                int m = 0;
                Dictionary<Item, int> item2Index = sortedItems.ToDictionary(n => n, n => ++m);

                IEnumerable<Item> topItems = sortedItems.Where(n => ItemMatch.IsMatch(innerMatchOrNull, n));

                int labelWidth = labelWidthOrNull ?? Math.Max(Math.Min(sortedItems.Max(n => n.Name.Length), 30), 4);
                int colWidth = Math.Max(1 + ("" + edges.Max(e => e.Ct)).Length, // 1+ because of loop prefix
                    1 + ("" + sortedItems.Count()).Length); // 1+ because of ! or % marker
                string itemFormat = "{0," + (colWidth - 1) + ":" + Repeat('0', colWidth - 1) + "}";
                string ctFormat = "{0}{1," + (colWidth - 1) + ":" + Repeat('#', colWidth) + "}";

                Write(output, colWidth, labelWidth, topItems, itemFormat, item2Index, withNotOkCt, sortedItems, ctFormat, itemsAndDependencies);
            } else {
                Log.WriteError("No visible items and dependencies found for output");
            }
        }

        protected abstract void Write(ITargetWriter output, int colWidth, int labelWidth, IEnumerable<Item> topItems,
            string itemFormat, Dictionary<Item, int> item2Index, bool withNotOkCt, IEnumerable<Item> sortedItems,
            string ctFormat, IDictionary<Item, IEnumerable<Dependency>> itemsAndDependencies);

        public abstract void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, int? dependenciesCount, string argsAsString, [NotNull] WriteTarget target, bool ignoreCase);

        public abstract void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream stream, string testOption);

        public string GetHelp() {
            return $"{GetType().Name} usage: -___ outputfileName";
        }

        public abstract WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, string argsAsString, WriteTarget baseTarget);
    }
}