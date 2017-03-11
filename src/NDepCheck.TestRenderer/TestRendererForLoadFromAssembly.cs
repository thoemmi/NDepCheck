using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NDepCheck.Rendering;

namespace NDepCheck.TestRenderer {
    public class TestRendererForLoadFromAssembly : GraphicsRenderer<Item,Dependency> {
        protected override Color GetBackGroundColor => Color.Yellow;

        protected override void PlaceObjects(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
            var origin = new VariableVector("origin");
            double deltaAngle = 2 * Math.PI / items.Count();
            Func<int, double> r =
                  items.Any(i => i.Name.StartsWith("star")) ? i => 100.0 + (i % 2 == 0 ? 60 : 0)
                : items.Any(i => i.Name.StartsWith("spiral")) ? (Func<int, double>)(i => 80.0 + 20 * i)
                : /*circle*/ i => 100;

            var diagonals = new Store<Item, Vector>();

            int n = 0;
            foreach (var i in items) {
                int k = n++;
                double angle = k * deltaAngle;
                var pos = new DependentVector(() => origin.X() + r(k) * Math.Sin(angle), () => origin.X() + r(k) * Math.Cos(angle));
                ItemBoxes.Put(i, Box(pos, diagonals.Put(i, V(i.Name, null, 15)), i.Name, borderWidth: 2));
            }

            foreach (var d in dependencies) {
                IBox from = ItemBoxes.Get(d.UsingItem);
                IBox to = ItemBoxes.Get(d.UsedItem);

                if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                    //Arrow(from.Center, to.Center, 1, text: "#=" + d.Ct, textLocation: 0.3);
                    Arrow(from.GetBestAnchor(to.Center), to.GetBestAnchor(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                    //Arrow(from.GetNearestAnchor(to.Center), to.Center, 1, text: "#=" + d.Ct);
                }
            }

            origin.Set(0, 0);
        }

        protected override Size GetSize() {
            return new Size(400, 300);
        }
    }
}

