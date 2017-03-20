using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NDepCheck.Rendering {
    public class MatrixRenderer1 : AbstractMatrixRenderer, IDependencyRenderer {
        public void Render(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string argsAsString) {
            new GenericMatrixRenderer1().Render(items, dependencies, argsAsString);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, Stream output) {
            new GenericMatrixRenderer1().RenderToStreamForUnitTests(items, dependencies, output);
        }
    }

    public class GenericMatrixRenderer1 : AbstractGenericMatrixRenderer {
        protected override void Write(StreamWriter output, int colWidth, int labelWidth, IEnumerable<INode> topNodes, string nodeFormat,
           Dictionary<INode, int> node2Index, bool withNotOkCt, IEnumerable<INode> sortedNodes, string ctFormat, IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges) {
            WriteFormat1Line(output, Limit("Id", colWidth), Limit("Name", labelWidth),
                topNodes.Select(n => NodeId(n, nodeFormat, node2Index) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "")));

            IWithCt ZERO_EDGE = new DependencyGrapher.ZeroEdge();
            foreach (var used in sortedNodes) {
                INode used1 = used;
                WriteFormat1Line(output, NodeId(used, nodeFormat, node2Index), Limit(used.Name, labelWidth),
                    topNodes.Select( @using =>
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

        public override void RenderToStreamForUnitTests(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                Render(items, dependencies, sw, null, true);
            }
        }

        public override void Render(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, string argsAsString) {
            string filename;
            int? labelWidthOrNull;
            bool withNotOkCt;
            ParseOptions(argsAsString, out filename, out labelWidthOrNull, out withNotOkCt);

            string csvFilename = Path.ChangeExtension(filename, ".csv");
            Log.WriteInfo("Writing " + csvFilename);
            using (var sw = new StreamWriter(csvFilename)) {
                Render(items, dependencies, sw, labelWidthOrNull, withNotOkCt);
            }
        }
    }
}