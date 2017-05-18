using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.GraphicsRendering {
    public class MatrixRenderer1 : AbstractMatrixRenderer {
        private class ZeroDependency : IWithCt {
            public int Ct => 0;

            public int NotOkCt => 0;
        }

        protected override void Write(ITargetWriter output, int colWidth, int labelWidth, IEnumerable<Item> topItems, string itemFormat,
           Dictionary<Item, int> item2Index, bool withNotOkCt, IEnumerable<Item> sortedItems, string ctFormat, IDictionary<Item, IEnumerable<Dependency>> itemsAndDependencies) {
            WriteFormat1Line(output, Limit("Id", colWidth), Limit("Name", labelWidth),
                topItems.Select(n => GetItemId(n, itemFormat, item2Index) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "")));

            IWithCt ZERO_EDGE = new ZeroDependency();

            foreach (var used in sortedItems) {
                Item used1 = used;
                WriteFormat1Line(output, GetItemId(used, itemFormat, item2Index), Limit(used.Name, labelWidth),
                    topItems.Select( @using =>
                            FormatCt(withNotOkCt, ctFormat, item2Index[@using] > item2Index[used1],
                                itemsAndDependencies[@using].FirstOrDefault(e => e.UsedItem.Equals(used1)) ?? ZERO_EDGE)));
            }
        }

        private static void WriteFormat1Line(ITargetWriter output, string index, string label, IEnumerable<string> columns) {
            char sep = ';';
            output.Write(index);
            output.Write(sep);
            output.Write(label);
            foreach (var col in columns) {
                output.Write(sep);
                output.Write(col);
            }
            output.WriteLine();
        }

        public override void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream stream, string testOption) {
            using (var sw = new TargetStreamWriter(stream)) {
                Render(dependencies, null, sw, null, true);
            }
        }

        public override void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, int? dependenciesCount, string argsAsString, [NotNull] WriteTarget target, bool ignoreCase) {
            int? labelWidthOrNull;
            bool withNotOkCt;
            ItemMatch itemMatchOrNull;
            ParseOptions(globalContext, argsAsString, ignoreCase, out labelWidthOrNull, out withNotOkCt, out itemMatchOrNull);

            using (var sw = target.ChangeExtension(".csv").CreateWriter()) {
                Render(dependencies, null/*TODO: InnerMatch?*/, sw, labelWidthOrNull, withNotOkCt);
            }
        }

        public override WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, string argsAsString, WriteTarget baseTarget) {
            return baseTarget.ChangeExtension(".csv");
        }
    }
}