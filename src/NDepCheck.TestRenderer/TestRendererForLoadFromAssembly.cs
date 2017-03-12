using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NDepCheck.Rendering;

namespace NDepCheck.TestRenderer {
    public class TestRendererForLoadFromAssembly : GraphicsDependencyRenderer {
        protected override Color GetBackGroundColor => Color.Yellow;

        protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
            var origin = new BoundedVector("origin");
            double deltaAngle = 2 * Math.PI / items.Count();

            var diagonals = new Store<Item, Vector>();

            int n = 0;
            foreach (var i in items) {
                int k = n++;
                double angle = k * deltaAngle;
                var pos = new DependentVector(() => origin.X() + 500 * Math.Sin(angle), () => origin.X() + 500 * Math.Cos(angle));
                ItemBoxes.Put(i, Box(pos, diagonals.Put(i, B(i.Name).Restrict(F(null, 15))), i.Name, borderWidth: 2));
            }

            foreach (var d in dependencies) {
                IBox from = ItemBoxes.Get(d.UsingItem);
                IBox to = ItemBoxes.Get(d.UsedItem);

                if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                    //Arrow(from.Center, to.Center, 1, text: "#=" + d.Ct, textLocation: 0.3);
                    Arrow(from.GetBestConnector(to.Center), to.GetBestConnector(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                    //Arrow(from.GetNearestAnchor(to.Center), to.Center, 1, text: "#=" + d.Ct);
                }
            }

            origin.Set(F(0, 0));
        }

        protected override Size GetSize() {
            return new Size(2000, 1500);
        }
    }
}

