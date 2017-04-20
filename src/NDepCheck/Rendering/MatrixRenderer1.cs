using System.Collections.Generic;
using System.IO;
using System.Linq;
using NDepCheck.Transforming;

namespace NDepCheck.Rendering {
    public class MatrixRenderer1 : AbstractMatrixRenderer {
        private class ZeroDependency : IWithCt {
            public int Ct => 0;

            public int NotOkCt => 0;
        }

        protected override void Write(TextWriter output, int colWidth, int labelWidth, IEnumerable<Item> topNodes, string nodeFormat,
           Dictionary<Item, int> node2Index, bool withNotOkCt, IEnumerable<Item> sortedNodes, string ctFormat, IDictionary<Item, IEnumerable<Dependency>> nodesAndEdges) {
            WriteFormat1Line(output, Limit("Id", colWidth), Limit("Name", labelWidth),
                topNodes.Select(n => NodeId(n, nodeFormat, node2Index) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "")));

            IWithCt ZERO_EDGE = new ZeroDependency();

            foreach (var used in sortedNodes) {
                Item used1 = used;
                WriteFormat1Line(output, NodeId(used, nodeFormat, node2Index), Limit(used.Name, labelWidth),
                    topNodes.Select( @using =>
                            FormatCt(withNotOkCt, ctFormat, node2Index[@using] > node2Index[used1],
                                nodesAndEdges[@using].FirstOrDefault(e => e.UsedNode.Equals(used1)) ?? ZERO_EDGE)));
            }
        }

        private static void WriteFormat1Line(TextWriter output, string index, string label, IEnumerable<string> columns) {
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

        public override void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                Render(dependencies, null, sw, null, true);
            }
        }

        public override void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount, string argsAsString, string baseFileName, bool ignoreCase) {
            int? labelWidthOrNull;
            bool withNotOkCt;
            ItemMatch itemMatchOrNull;
            ParseOptions(globalContext, argsAsString, ignoreCase, out labelWidthOrNull, out withNotOkCt, out itemMatchOrNull);

            using (var sw = new StreamWriter(GetCSVFileName(baseFileName))) {
                Render(dependencies, null/*TODO: InnerMatch?*/, sw, labelWidthOrNull, withNotOkCt);
            }
        }

        private string GetCSVFileName(string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".csv");
        }

        public override string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            return GetCSVFileName(baseFileName);
        }
    }
}