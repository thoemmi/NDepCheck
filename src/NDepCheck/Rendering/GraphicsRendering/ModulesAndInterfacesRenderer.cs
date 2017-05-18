using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.ConstraintSolving;

namespace NDepCheck.Rendering.GraphicsRendering {
    public class ModulesAndInterfacesRenderer : GraphicsRenderer {
        public static readonly Option InterfaceSelectorOption = new Option("si", "select-interface", "&", "Regexp for interface marker, i.e., items that are drawn as vertical bars", @default: null);
        public static readonly Option OrderFieldOption = OrderSupport.CreateOption();

        private static readonly Font _boxFont = new Font(FontFamily.GenericSansSerif, 10);
        private static readonly Font _interfaceFont = new Font(FontFamily.GenericSansSerif, 7);
        private static readonly Font _lineFont = new Font(FontFamily.GenericSansSerif, 5);

        [NotNull]
        private OrderSupport _orderSupport = new OrderSupport(item => null);

        // TODO: Replace with ItemMatcher
        private Regex _interfaceSelector = new Regex("^I");

        private static string GetName(Item i) {
            return i.Values[0];
        }

        private static string GetModule(Item i) {
            return i.Values[1];
        }

        protected override void PlaceObjects([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies) {
            // ASCII-art sketch of what I want to accomplish:
            //
            //    |         |         |             |          |        |<--------+-----+
            //    |         |         |             |          |<-----------------|     |
            //    |         |         |             |<----------------------------| Top |
            //    |         |         |<------------------------------------------|     |
            //    |         |<----------------------------------------------------+-----+
            //    |         |         |             |          |        |
            //    |         |         |             |          |<-------+-----+
            //    |         |         |             |<------------------|     |
            //    |         |         |<--------------------------------| IMP |
            //    |         |<------------------------------------------|     |
            //    |<----------------------------------------------------+-----+
            //    |         |         |             |          |          |
            //    |         |         |             |<---------+-----+    |
            //    |         |         |<-----------------------|     |    |
            //    |         |<---------------------------------| WLG |    |
            //    |<-------------------------------------------+-----+    |
            //    |         |         |             |          | | |      |
            //    |         |         |<------------|          | | |      |
            //    |         |         | |<::::::::::+-----+    | | |      |
            //    |         |<----------|-----------| VKF |----|--------->|
            //    |<--------------------|-----------+-----+    | | |      |
            //    |         |         | |             | |      | | |      Imp.MI
            //    |<--------------------|---------------|      | | |
            //    |<--------------------|-------------| |      | | |
            //    |         |         | |     ...     | |      | | |
            //    |         |         | |             | |      | | |
            //    |         |         +-----+=================>| | |
            //    |         |         | KAH |---------->|        | |
            //    |         |         +-----+-------->| |        | |
            //    |         |           |             | |        | |
            //    |         +-----+------------------------------->|
            //    |         |     |----------------------------->| |
            //    |         | KST |-------------------->|        | |
            //    |         |     |------------------>| |        | |
            //    |<--------+-----+---->|             | |        | |
            //    |                     |             | |        | |
            //    |        ...          |             | |        | |
            //    |                    Kah         Vkf1 Vkf2  Wlg1 Wlg2
            //    +-----+              .MI          .MI .MI    .MI .MI
            //    | BAC |
            //    +-----+
            //
            // ===> is a dependency from a "lower" to a "higher" module
            // that circumvents the MI. It should most probably be flagged
            // as incorrect and then red in the diagram.
            // :::> is a dependency from a "higher" to a "lower" module
            // via an MI ("module interface"). This is ok, it is only
            // highlighted to show that the Renderer must be able to deal
            // with this.

            // The itemtype is expected to have 3 fields Name:Module:Order.
            // In the example diagram above, we would have items about like the following:
            //        BAC    :BAC:0100
            //        KST    :KST:0200
            //        KAH    :KAH:0300
            //        Kah.MI :KAH:0301
            //        VKF    :VKF:0400
            //        Vkf1.MI:VKF:0401
            //        Vkf2.MI:VKF:0402
            //        WLG    :WLG:0500
            //        Wlg1.MI:WLG:0501
            //        Wlg2.MI:WLG:0502
            //        IMP    :IMP:0600
            //        Imp.MI :IMP:0601
            //        Top    :TOP:0700

            VariableVector itemDistance = new VariableVector(nameof(itemDistance), Solver);
            VariableVector pos = F(0, 30);

            //itemDistance.MaxY(80);
            //itemDistance.SetX(300);

            Arrow(F(0, 0), F(100, 0), 1, Color.Chartreuse, "100px", textFont: _lineFont);
            Box(F(200, 0), _title + "(" + DateTime.Now + ")", boxAnchoring: BoxAnchoring.LowerLeft);

            const int DELTA_Y_MAIN = 8;

            IEnumerable<Item> items = dependencies.SelectMany(e => new[] { e.UsingItem, e.UsedItem }).Distinct();
            IEnumerable<Item> parents = items.Where(i => !IsMI(i));
            IEnumerable<Item> misWithoutParent = items.Where(i => IsMI(i) && parents.All(p => GetModule(p) != GetModule(i)));

            var mainBoxes = new Dictionary<Item, IBox>();
            var interfaceBoxes = new Dictionary<Item, IBox>();
            var mainBoxesNextFreePos = new Dictionary<Item, VariableVector>();
            var interfaceBoxesNextFreePos = new Dictionary<Item, VariableVector>();
            var mainItems = new Dictionary<Item, Item>();

            // Main modules along diagonal, separated by itemDistance


            foreach (var i in parents.Concat(misWithoutParent).OrderBy(_orderSupport.OrderSelector)) {
                string name = GetName(i);

                string countText = "\n<" + Sum(dependencies, d => d.UsingItem.Equals(i) && !d.UsedItem.Equals(i))
                                   + " =" + Sum(dependencies, d => d.UsingItem.Equals(i) && d.UsedItem.Equals(i))
                                   + " >" + Sum(dependencies, d => !d.UsingItem.Equals(i) && d.UsedItem.Equals(i));
                // TODO: Add option and computation to split this into .,?,!

                pos.AlsoNamed(name);
                IBox mainBox = Box(pos, boxAnchoring: BoxAnchoring.LowerLeft, text: name + countText, borderWidth: 3,
                    boxColor: IsMI(i) ? Color.LemonChiffon : Color.Coral, textFont: _boxFont, drawingOrder: 1, fixingOrder: 4);
                //mainBox.Diagonal.Y.Set(100);
                //mainBox.Diagonal.Y.Min(40);
                mainBox.Diagonal.Y.Max(60 + dependencies.Count(d => Equals(d.UsingItem, i)) * DELTA_Y_MAIN); // Help for solving
                mainBox.Diagonal.Y.Min(10 + dependencies.Count(d => Equals(d.UsingItem, i)) * DELTA_Y_MAIN); // Help for solving
                mainBoxes[i] = mainBox;
                mainBoxesNextFreePos[i] = mainBox.LowerLeft;
                {
                    IBox interfaceBox = Box(new VariableVector(name + ".I", Solver).SetX(mainBox.LowerLeft.X),
                                            text: "", boxAnchoring: BoxAnchoring.LowerLeft, borderWidth: 1,
                                            boxColor: Color.Coral, fixingOrder: 3);
                    interfaceBox.Diagonal.SetX(10);

                    interfaceBox.UpperLeft.MinY(mainBox.UpperLeft.Y + 7);
                    interfaceBox.LowerLeft.MaxY(mainBox.LowerLeft.Y - 7);

                    interfaceBoxes[i] = interfaceBox;
                    interfaceBoxesNextFreePos[i] = mainBox.LowerLeft - F(0, 10);
                }

                NumericVariable interfacePos = Solver.CreateConstant("", 18);

                foreach (var mi in items.Where(mi => IsMI(mi) && GetModule(mi) == GetModule(i)).OrderBy(_orderSupport.OrderSelector)) {
                    VariableVector miPos = new VariableVector(name + _interfaceSelector, Solver).SetX(mainBox.CenterLeft.X + interfacePos);

                    var miBox = Box(miPos, text: GetName(mi), boxAnchoring: BoxAnchoring.UpperLeft,
                        boxTextPlacement: BoxTextPlacement.LeftUp, borderWidth: 1, boxColor: Color.LemonChiffon,
                        textFont: _interfaceFont, fixingOrder: 3);
                    mainItems[mi] = i;
                    interfaceBoxes[mi] = miBox;

                    miBox.UpperLeft.MinY(mainBox.UpperLeft.Y + 7);
                    miBox.LowerLeft.MaxY(mainBox.LowerLeft.Y - miBox.TextBox.Y);

                    interfacePos += 18;
                }

                mainBox.Diagonal.MinX(interfacePos);
                itemDistance.MinX(mainBox.Diagonal.X + 12);
                itemDistance.MinY(mainBox.Diagonal.Y + 15);

                pos += itemDistance;
            }

            foreach (var d in dependencies) {
                Item from = d.UsingItem;
                Item to = d.UsedItem;
                if (IsMI(from)) {
                    IBox fromBox = interfaceBoxes[from];
                    Item mainItem = mainItems[from];
                    VariableVector nextFreePos = interfaceBoxesNextFreePos[mainItem];

                    VariableVector fromPos = new VariableVector(from + "->" + to, fromBox.LowerLeft.X, nextFreePos.Y);
                    ArrowToInterfaceBox(fromBox, interfaceBoxes[to], fromPos, d, "(I)");

                    interfaceBoxesNextFreePos[mainItem] -= F(0, 15);
                } else {
                    IBox mainBox = mainBoxes[from];
                    VariableVector fromPos = mainBoxesNextFreePos[from];

                    ArrowToInterfaceBox(mainBox, interfaceBoxes[to], fromPos, d, "");

                    mainBoxesNextFreePos[from] += F(0, DELTA_Y_MAIN);

                    itemDistance.MinY(fromPos.Y - mainBox.LowerLeft.Y);

                    // mainBox.Diagonal.MinY(fromPos.Y - mainBox.LowerLeft.Y); ==> NO SOLUTION; therefore explcit computation above
                }
            }
        }

        private string Sum([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
            var ct = dependencies.Where(filter).Sum(d => d.Ct);
            return ct >= 1000000 ? ct / 1000 + "M" : ct >= 1000 ? ct / 1000 + "K" : "" + ct;
        }

        private void ArrowToInterfaceBox(IBox fromBox, IBox toBox, VariableVector fromPos, Dependency d, string prefix) {
            VariableVector toPos = toBox.GetBestConnector(fromPos).WithYOf(fromPos);
            fromPos = fromBox.GetBestConnector(toPos).WithYOf(fromPos);
            Arrow(fromPos, toPos, 1, color: d.NotOkCt > 0 ? Color.Red :
                d.QuestionableCt > 0 ? Color.Blue : Color.Black, text: prefix + "#=" + d.Ct,
                textLocation: -20, textFont: _lineFont, fixingOrder: 2, edgeInfo: d.ExampleInfo);

            toBox.UpperLeft.MinY(fromPos.Y + 5);
            toBox.LowerLeft.MaxY(fromPos.Y - toBox.TextBox.Y);
        }

        private bool IsMI(Item mi) {
            return _interfaceSelector.IsMatch(GetName(mi));
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies() {
            ItemType amo = ItemType.New("AMO(Assembly:Module:Order)");

            var bac = Item.New(amo, "BAC:BAC:0100".Split(':'));
            var kst = Item.New(amo, "KST:KST:0200".Split(':'));
            var kah = Item.New(amo, "KAH:KAH:0300".Split(':'));
            var kah_mi = Item.New(amo, "Kah.MI:KAH:0301".Split(':'));
            var vkf = Item.New(amo, "VKF:VKF:0400".Split(':'));
            var vkf1_mi = Item.New(amo, "Vkf1.MI:VKF:0401".Split(':'));
            var vkf2_mi = Item.New(amo, "Vkf2.MI:VKF:0402".Split(':'));
            //var vkf3_mi = Item.New(amo, "Vkf3.MI:VKF:0402".Split(':'));
            //var vkf4_mi = Item.New(amo, "Vkf4.MI:VKF:0402".Split(':'));
            var wlg = Item.New(amo, "WLG:WLG:0500".Split(':'));
            var wlg1_mi = Item.New(amo, "Wlg1.MI:WLG:0501".Split(':'));
            var wlg2_mi = Item.New(amo, "Wlg2.MI:WLG:0502".Split(':'));
            //var imp = Item.New(amo, "IMP:IMP:0600".Split(':'));
            var imp_mi = Item.New(amo, "Imp.MI:IMP:0601".Split(':'));
            //var top = Item.New(amo, "Top:TOP:0700".Split(':'));

            return new[] {
                    FromTo(kst, bac), FromTo(kst, kah_mi), FromTo(kst, vkf1_mi), FromTo(kst, vkf2_mi), FromTo(kst, wlg1_mi), FromTo(kst, wlg2_mi),
                    FromTo(kah, bac), FromTo(kah, vkf1_mi), FromTo(kah, vkf2_mi), FromTo(kah, wlg, 4, 3) /* ===> */,
                    FromTo(vkf, bac), FromTo(vkf, kst), FromTo(vkf, kah, 3), FromTo(vkf, kah_mi, 2, 2) /* <:: */, FromTo(vkf, imp_mi), FromTo(vkf1_mi, bac), FromTo(vkf2_mi, bac),
                    // ... more to come
                };
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionableCt = 0) {
            return new Dependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionableCt, exampleInfo: questionableCt > 0 ? from + "==>" + to : "");
        }

        public override void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, int? dependenciesCount, string argsAsString, [NotNull] WriteTarget target, bool ignoreCase) {
            DoRender(globalContext, dependencies, argsAsString, target,
                InterfaceSelectorOption.Action((args, j) => {
                    _interfaceSelector = new Regex(Option.ExtractRequiredOptionValue(args, ref j, "Regex for interface selector missing"));
                    return j;
                }),
                OrderFieldOption.Action((args, j) => {
                    string orderPattern = Option.ExtractRequiredOptionValue(args, ref j, "order field missing");
                    _orderSupport = OrderSupport.Create(orderPattern, ignoreCase);
                    return j;
                }));
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            return @"  A GIF renderer that depicts modules and their interfaces as
  vertical bars that are connected by horizontal arrows.

  {GetHelpExplanations()}

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }
    }
}
