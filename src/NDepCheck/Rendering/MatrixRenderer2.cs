using System.Collections.Generic;
using System.IO;
using System.Linq;
using NDepCheck.Transforming;

namespace NDepCheck.Rendering {
    public class MatrixRenderer2 : AbstractMatrixRenderer, IDependencyRenderer {
        private readonly GenericMatrixRenderer2 _delegate = new GenericMatrixRenderer2();

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount, string argsAsString, string baseFileName, bool ignoreCase) {
            _delegate.Render(globalContext, dependencies, dependenciesCount, argsAsString, baseFileName, ignoreCase);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            _delegate.RenderToStreamForUnitTests(dependencies, output);
        }

        public string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            return _delegate.GetMasterFileName(globalContext, argsAsString, baseFileName);
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
                    var edge = nodesAndEdges[@using].FirstOrDefault(e => e.UsedNode.Equals(used));
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
                Render(dependencies, null, sw, null, true);
            }
        }

        public override void Render(GlobalContext globalContext, IEnumerable<IEdge> dependencies, int? dependenciesCount, string argsAsString, string baseFileName, bool ignoreCase) {
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