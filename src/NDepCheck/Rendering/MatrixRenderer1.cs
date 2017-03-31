using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NDepCheck.Rendering {
    public class MatrixRenderer1 : AbstractMatrixRenderer, IDependencyRenderer {
        private readonly GenericMatrixRenderer1 _delegate = new GenericMatrixRenderer1();

        public void Render(IEnumerable<Dependency> dependencies, string argsAsString, string baseFileName) {
            _delegate.Render(dependencies, argsAsString, baseFileName);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            _delegate.RenderToStreamForUnitTests(dependencies, output);
        }

        public string GetMasterFileName(string argsAsString, string baseFileName) {
            return _delegate.GetMasterFileName(argsAsString, baseFileName);
        }
    }

    public class GenericMatrixRenderer1 : AbstractGenericMatrixRenderer {
        private class ZeroEdge : IWithCt {
            public int Ct => 0;

            public int NotOkCt => 0;
        }

        protected override void Write(TextWriter output, int colWidth, int labelWidth, IEnumerable<INode> topNodes, string nodeFormat,
           Dictionary<INode, int> node2Index, bool withNotOkCt, IEnumerable<INode> sortedNodes, string ctFormat, IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges) {
            WriteFormat1Line(output, Limit("Id", colWidth), Limit("Name", labelWidth),
                topNodes.Select(n => NodeId(n, nodeFormat, node2Index) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "")));

            IWithCt ZERO_EDGE = new ZeroEdge();

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

        public override void RenderToStreamForUnitTests(IEnumerable<IEdge> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                Render(dependencies, sw, null, true);
            }
        }

        public override void Render(IEnumerable<IEdge> dependencies, string argsAsString, string baseFileName) {
            int? labelWidthOrNull;
            bool withNotOkCt;
            ParseOptions(argsAsString, out labelWidthOrNull, out withNotOkCt);

            using (var sw = new StreamWriter(GetCSVFileName(baseFileName))) {
                Render(dependencies, sw, labelWidthOrNull, withNotOkCt);
            }
        }

        private string GetCSVFileName(string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".csv");
        }

        public override string GetMasterFileName(string argsAsString, string baseFileName) {
            return GetCSVFileName(baseFileName);
        }
    }
}