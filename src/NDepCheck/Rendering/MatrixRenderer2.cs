using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NDepCheck.Rendering {
    public class MatrixRenderer2 : AbstractMatrixRenderer, IDependencyRenderer {
        public void RenderToFile(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string baseFilename, int? optionsStringLength) {
            new GenericMatrixRenderer2().RenderToFile(items, dependencies, baseFilename, optionsStringLength);
        }

        public void RenderToStream(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, Stream output, int? optionsStringLength) {
            new GenericMatrixRenderer2().RenderToStream(items, dependencies, output, optionsStringLength);
        }
    }

    public class GenericMatrixRenderer2 : AbstractGenericMatrixRenderer {
        protected override void Write(StreamWriter output, int colWidth, int labelWidth, IEnumerable<INode> topNodes, string nodeFormat,
            Dictionary<INode, int> node2Index, bool withNotOkCt, IEnumerable<INode> sortedNodes, string ctFormat, IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges) {
            var emptyCtCols = Repeat(' ', colWidth) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "");
            WriteFormat2Line(output, Limit("Id", colWidth), Limit("Name", labelWidth), Limit("Id", colWidth), Limit("Name", labelWidth), emptyCtCols);
            foreach (var @using in topNodes) {
                WriteFormat2Line(output, NodeId(@using, nodeFormat, node2Index), Limit(@using.Name, labelWidth), Limit("", colWidth), Limit("", labelWidth), emptyCtCols);
                foreach (var used in sortedNodes) {
                    var edge = nodesAndEdges[@using].FirstOrDefault(e => !e.Hidden && e.UsedNode.Equals(used));
                    if (edge != null) {
                        WriteFormat2Line(output, NodeId(@using, nodeFormat, node2Index), Limit(@using.Name, labelWidth), NodeId(used, nodeFormat, node2Index),
                            Limit(used.Name, labelWidth),
                            FormatCt(withNotOkCt, ctFormat, node2Index[@using] > node2Index[used], edge));
                    }
                }
            }
        }

        private static void WriteFormat2Line(TextWriter output, string id1, string name1, string id2, string name2, string cts) {
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