using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Rendering {
    internal static class SomeRendererTestData {
        public static IEnumerable<Dependency> CreateSomeTestItems() {
            ItemType simple = ItemType.New("SIMPLE(Name)");
            Item[] localItems = Enumerable.Range(0, 5).Select(i => Item.New(simple, "Item " + i)).ToArray();
            return localItems.SelectMany(
                    (from, i) => localItems.Skip(i).Select(to => new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: 10 * i))).ToArray();
        }

    }
}
