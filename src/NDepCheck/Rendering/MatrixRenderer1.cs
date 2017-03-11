using System.Collections.Generic;
using System.IO;
using System.Linq;
using NDepCheck.Rendering;

namespace NDepCheck {
    public class MatrixRenderer1 : IDependencyRenderer {
        public void RenderToFile(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string baseFilename, int? optionsStringLength) {
            new GenericMatrixRenderer1().RenderToFile(items, dependencies, baseFilename, optionsStringLength);
        }

        public void RenderToStream(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, Stream output, int? optionsStringLength) {
            new GenericMatrixRenderer1().RenderToStream(items, dependencies, output, optionsStringLength);
        }
    }

    public class GenericMatrixRenderer1 : AbstractMatrixRenderer {
        protected override void Write(StreamWriter output, int colWidth, int labelWidth, IEnumerable<INode> topNodes, string nodeFormat,
           Dictionary<INode, int> node2Index, bool withNotOkCt, IEnumerable<INode> sortedNodes, string ctFormat, IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges) {
            WriteFormat1Line(output, Limit("Id", colWidth), Limit("Name", labelWidth),
                topNodes.Select(n => NodeId(n, nodeFormat, node2Index) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "")));

            IWithCt ZERO_EDGE = new DependencyGrapher.ZeroEdge();
            foreach (var used in sortedNodes) {
                INode used1 = used;
                WriteFormat1Line(output, NodeId(used, nodeFormat, node2Index), Limit(used.Name, labelWidth),
                    topNodes.Select(
                        @using =>
                            FormatCt(withNotOkCt, ctFormat, node2Index[@using] > node2Index[used1],
                                nodesAndEdges[@using].FirstOrDefault(e => !e.Hidden && e.UsedNode.Equals(used1)) ?? ZERO_EDGE)));
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

        public override void RenderToStream(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, Stream stream, int? optionsStringLength) {
            using (var sw = new StreamWriter(stream)) {
                Render(items, dependencies, sw, optionsStringLength);
            }
        }

        public override void RenderToFile(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, string baseFilename, int? optionsStringLength) {
            string filename = Path.ChangeExtension(baseFilename, ".csv");
            using (var sw = new StreamWriter(filename)) {
                Render(items, dependencies, sw, optionsStringLength);
            }
        }
    }
}