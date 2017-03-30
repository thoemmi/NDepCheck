using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NDepCheck.ConstraintSolving;
using NDepCheck.Rendering;

namespace NDepCheck.TestRenderer {
    public class TestRendererForLoadFromAssembly : GraphicsDependencyRenderer {
        protected override Color GetBackGroundColor => Color.Yellow;

        protected override void PlaceObjects(IEnumerable<Dependency> dependencies) {
            IEnumerable<Item> items = dependencies.SelectMany(e => new[] { e.UsingItem, e.UsedItem }).Distinct();
            double deltaAngle = 2 * Math.PI / items.Count();
            Func<int, double> r =
                  items.Any(i => i.Name.StartsWith("star")) ? i => 100.0 + (i % 2 == 0 ? 60 : 0)
                : items.Any(i => i.Name.StartsWith("spiral")) ? (Func<int, double>)(i => 80.0 + 20 * i)
                : /*circle*/ i => 100;

            int n = 0;
            foreach (var i in items) {
                int k = n++;
                double phi = k * deltaAngle;
                // Define position in polar coordinates with origin, radius (r) and angle (ohi).
                var pos = new VariableVector("pos_" + k, Solver, r(k) * Math.Sin(phi), r(k) * Math.Cos(phi));

                i.DynamicData.Box = Box(pos, i.Name, B(i.Name).Restrict(F(null, 15), F(null, 15)), borderWidth: 2);
            }

            foreach (var d in dependencies) {
                IBox from = d.UsingItem.DynamicData.Box;
                IBox to = d.UsedItem.DynamicData.Box;

                if (d.Ct > 0 && !Equals(d.UsingItem, d.UsedItem)) {
                    Arrow(from.GetBestConnector(to.Center), to.GetBestConnector(from.Center), 1, text: "#=" + d.Ct, textLocation: 0.2);
                }
            }
        }

        public override void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType simple = ItemType.New("Simple:Name");
            Item[] localItems = Enumerable.Range(0, 5).Select(i => Item.New(simple, "Item " + i)).ToArray();
            dependencies = localItems.SelectMany(
                    (from, i) => localItems.Skip(i).Select(to => new Dependency(from, to, new TextFileSource("Test", i), "Test", ct: 10 * i))).ToArray();
            items = localItems;
        }

        public override string GetHelp(bool detailedHelp) {
            return "TestRendererForLoadFromAssembly";
        }
    }
}

