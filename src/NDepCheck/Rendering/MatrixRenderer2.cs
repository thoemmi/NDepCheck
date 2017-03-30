using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NDepCheck.Rendering {
    public class MatrixRenderer2 : AbstractMatrixRenderer, IDependencyRenderer {
        public string Render(IEnumerable<Dependency> dependencies, string argsAsString, string baseFilename) {
            return new GenericMatrixRenderer2().Render(dependencies, argsAsString, baseFilename);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            new GenericMatrixRenderer2().RenderToStreamForUnitTests(dependencies, output);
        }
    }

    public class GenericMatrixRenderer2 : AbstractGenericMatrixRenderer {
        protected override void Write(TextWriter output, int colWidth, int labelWidth, IEnumerable<INode> topNodes, string nodeFormat,
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

        public override void RenderToStreamForUnitTests(IEnumerable<IEdge> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                Render(dependencies, sw, null, true);
            }
        }

        public override string Render(IEnumerable<IEdge> dependencies, string argsAsString, string baseFilename) {
            int? labelWidthOrNull;
            bool withNotOkCt;
            ParseOptions(argsAsString, out labelWidthOrNull, out withNotOkCt);

            using (var sw = GlobalContext.CreateTextWriter(baseFilename, ".csv")) {
                Render(dependencies, sw.Writer, labelWidthOrNull, withNotOkCt);
                return sw.FileName;
            }
        }
    }
}