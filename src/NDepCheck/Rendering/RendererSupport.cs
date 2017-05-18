using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    internal static class RendererSupport {
        public static IEnumerable<Dependency> CreateSomeTestItems() {
            ItemType simple = ItemType.New("SIMPLE(Name)");
            Item[] localItems = Enumerable.Range(0, 5).Select(i => Item.New(simple, "Item " + i)).ToArray();
            return localItems.SelectMany(
                    (from, i) => localItems.Skip(i).Select(to => new Dependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: 10 * i))).ToArray();
        }

        public static string ItemAsString([NotNull] this Item usedItem, bool showItemMarkers, bool isEnd, bool endOfCycle, bool matchedByCountMatch) {
            return (endOfCycle ? "<= " : "")
                   + (showItemMarkers ? usedItem.AsFullString() : usedItem.AsString())
                   + (matchedByCountMatch ? " (*)" : "")
                   + (isEnd ? " $" : "");
        }
    }
}
