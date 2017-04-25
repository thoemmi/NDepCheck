using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NDepCheck.Transforming;

namespace NDepCheck.Rendering {
    /// <summary>
    /// Writer for items in standard "DIP" format
    /// </summary>
    public class ItemWriter : IRenderer {
        public static readonly Option MatchOption = new Option("im", "item-match", "&", "Match to select items to write", @default: "all items are written");

        private static readonly Option[] _allOptions = { MatchOption };

        private static int Write(IEnumerable<Dependency> dependencies, TextWriter sw, ItemMatch itemMatch, bool ignoreCase) {
            var items = new HashSet<Item>(
                dependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }).Where(i => ItemMatch.Matches(itemMatch, i))
                );
            List<Item> itemsAsList = items.ToList();
            itemsAsList.Sort((i1, i2) => string.Compare(i1.AsStringWithOrderAndType(), i2.AsStringWithOrderAndType(),
                ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture));

            sw.WriteLine($"// Written {DateTime.Now} by {typeof(ItemWriter).Name} in NDepCheck {Program.VERSION}");
            sw.WriteLine();

            var writtenTypes = new HashSet<ItemType>();
            foreach (var i in itemsAsList) {
                ItemType itemType = i.Type;
                if (writtenTypes.Add(itemType)) {
                    sw.Write("$ ");
                    sw.WriteLine(itemType);
                }
            }
            sw.WriteLine();
            foreach (var i in itemsAsList) {
                sw.WriteLine(i.AsStringWithOrderAndType());
            }

            return itemsAsList.Count;
        }

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount,
                           string argsAsString, string baseFileName, bool ignoreCase) {
            ItemMatch itemMatch = null;
            Option.Parse(globalContext, argsAsString,
                MatchOption.Action((args, j) => {
                    itemMatch = ItemMatch.CreateItemMatchWithGenericType(Option.ExtractRequiredOptionValue(args, ref j, "Missing item match"), globalContext.IgnoreCase);
                    return j;
                }));
            using (var sw = GlobalContext.CreateTextWriter(GetMasterFileName(globalContext, argsAsString, baseFileName))) {
                int n = Write(dependencies, sw.Writer, itemMatch, globalContext.IgnoreCase);
                Log.WriteInfo($"... written {n} items");
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            using (var sw = new StreamWriter(output)) {
                Write(dependencies, sw, itemMatch: null, ignoreCase: false);
            }
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType amo = ItemType.New("AMO(Assembly:Module:Order)");

            var bac = Item.New(amo, "BAC:BAC:0100".Split(':'));
            var kst = Item.New(amo, "KST:KST:0200".Split(':'));
            var kah = Item.New(amo, "KAH:KAH:0300".Split(':'));
            var kah_mi = Item.New(amo, "Kah.MI:KAH:0301".Split(':'));
            var vkf = Item.New(amo, "VKF:VKF:0400".Split(':'));

            items = new[] { bac, kst, kah, kah_mi, vkf };

            dependencies = new[] {
                    FromTo(kst, bac), FromTo(kst, kah_mi), FromTo(kah, bac), FromTo(vkf, bac), FromTo(vkf, kst), FromTo(vkf, kah, 3), FromTo(vkf, kah_mi, 2, 2)
                    // ... more to come
                };
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes items to .txtfiles.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".txt");
        }
    }
}
