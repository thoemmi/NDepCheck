using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NDepCheck.Rendering;

namespace NDepCheck.TestRenderer {
    public class TestRendererForLoadFromAssembly : GraphicsDependencyRenderer {
        protected override Color GetBackGroundColor => Color.Yellow;

        protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
            // Only for fun, I set the origin at the very end. This means that all placement vectors are
            // DependentVectors, which do not not know their actual values throughout PlaceObjects.
            var origin = new BoundedVector("origin");
            double deltaAngle = 2 * Math.PI / items.Count();

            var diagonals = new Store<Item, Vector>();

            int n = 0;
            foreach (var i in items) {
                int k = n++;
                double angle = k * deltaAngle;
                var pos = new DependentVector(() => origin.X() + 500 * Math.Sin(angle), () => origin.X() + 500 * Math.Cos(angle));
                i.DynamicData.MainBox = Box(pos, diagonals.Put(i, B(i.Name).Restrict(F(null, 15))), i.Name, borderWidth: 2);
            }

            foreach (var d in dependencies) {
                IBox from = d.UsingItem.DynamicData.MainBox;
                IBox to = d.UsedItem.DynamicData.MainBox;

                if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                    Arrow(from.GetBestConnector(to.Center), to.GetBestConnector(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                }
            }

            origin.Set(F(0, 0));
        }

        protected override Size GetSize() {
            return new Size(2000, 1500);
        }

        public override void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType simple = ItemType.New("Simple:Name");
            Item[] localItems = Enumerable.Range(0, 5).Select(i => Item.New(simple, "Item " + i)).ToArray();
            dependencies = localItems.SelectMany(
                    (from, i) => localItems.Skip(i).Select(to => new Dependency(from, to, "Test", i, 0, ct: 10 * i))).ToArray();
            items = localItems;
        }
    }
}

