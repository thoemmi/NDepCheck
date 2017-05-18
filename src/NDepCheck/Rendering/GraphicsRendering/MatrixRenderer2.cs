using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.GraphicsRendering {
    public class MatrixRenderer2 : AbstractMatrixRenderer {
        protected override void Write(ITargetWriter output, int colWidth, int labelWidth, IEnumerable<Item> topItems, string itemFormat,
            Dictionary<Item, int> item2Index, bool withNotOkCt, IEnumerable<Item> sortedItems, string ctFormat, IDictionary<Item, IEnumerable<Dependency>> itemsAndDependencies) {
            var emptyCtCols = Repeat(' ', colWidth) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "");
            WriteFormat2Line(output, Limit("Id", colWidth), Limit("Name", labelWidth), Limit("Id", colWidth), Limit("Name", labelWidth), emptyCtCols);
            foreach (var @using in topItems) {
                WriteFormat2Line(output, GetItemId(@using, itemFormat, item2Index), Limit(@using.Name, labelWidth), Limit("", colWidth), Limit("", labelWidth), emptyCtCols);
                foreach (var used in sortedItems) {
                    Dependency edge = itemsAndDependencies[@using].FirstOrDefault(e => e.UsedItem.Equals(used));
                    if (edge != null) {
                        WriteFormat2Line(output, GetItemId(@using, itemFormat, item2Index), Limit(@using.Name, labelWidth), GetItemId(used, itemFormat, item2Index),
                            Limit(used.Name, labelWidth),
                            FormatCt(withNotOkCt, ctFormat, item2Index[@using] > item2Index[used], edge));
                    }
                }
            }
        }

        private static void WriteFormat2Line(ITargetWriter output, string id1, string name1, string id2, string name2, string cts) {
            output.Write(id1);
            output.Write(';');
            output.Write(name1);
            output.Write(';');
            output.Write(id2);
            output.Write(';');
            output.Write(name2);
            output.Write(';');
            output.WriteLine(cts);
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

            using (ITargetWriter sw = GetCSVTarget(target).CreateWriter()) {
                Render(dependencies, null/*TODO: InnerMatch?*/, sw, labelWidthOrNull, withNotOkCt);
            }
        }

        private static WriteTarget GetCSVTarget(WriteTarget target) {
            return target.ChangeExtension(".csv");
        }

        public override WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, string argsAsString, WriteTarget baseTarget) {
            return GetCSVTarget(baseTarget);
        }
    }
}