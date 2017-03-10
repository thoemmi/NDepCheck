using System;
using System.Collections.Generic;
using System.Linq;
using NDepCheck.Rendering;

namespace NDepCheck.TestAssembly.Rendering {
    public class SimpleTestRenderer : Renderer {
        protected override void CreateImage(IEnumerable<Item> items, IEnumerable<Dependency> dependencies) {
            var origin = new VariableVector("origin");
            double deltaAngle = 2 * Math.PI / items.Count();
            const double r = 100;

            double angle = 0;

            var diagonals = new Store<Item, Vector>();

            foreach (var i in items) {
                double angle1 = angle;
                var pos = new DependentVector(() => origin.X() + r * Math.Sin(angle), () => origin.X() + r * Math.Cos(angle1));
                ItemRectangles.Put(i, CreateRectangle(pos, diagonals.Put(i, C(1,1)), text: i.Name));
                angle += deltaAngle;
            }

            foreach (var d in dependencies) {
                IRectangle from = ItemRectangles.Get(d.UsingItem);
                IRectangle to = ItemRectangles.Get(d.UsedItem);

                CreateLine(from.GetAnchor(to.Center), to.GetAnchor(from.Center), 1);
            }

            throw new NotImplementedException();
        }
    }
}
