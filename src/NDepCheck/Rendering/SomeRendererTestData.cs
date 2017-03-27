using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Rendering {
    internal static class SomeRendererTestData {
        public static void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType simple = ItemType.New("Simple:Name");
            Item[] localItems = Enumerable.Range(0, 5).Select(i => Item.New(simple, "Item " + i)).ToArray();
            dependencies = localItems.SelectMany(
                    (from, i) => localItems.Skip(i).Select(to => new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: 10 * i))).ToArray();
            items = localItems;
        }

    }
}
