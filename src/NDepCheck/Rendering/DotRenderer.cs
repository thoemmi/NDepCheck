using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    /// <summary>
    /// Class that creates AT&amp;T DOT (graphviz) output from dependencies - see <a href="http://graphviz.org/">http://graphviz.org/</a>.
    /// </summary>
    public class DotRenderer : IDependencyRenderer {
        private readonly GenericDotRenderer _delegate = new GenericDotRenderer();

        public void Render(IEnumerable<Dependency> dependencies, string argsAsString, string baseFileName) {
            _delegate.Render(dependencies, argsAsString, baseFileName);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            _delegate.RenderToStreamForUnitTests(dependencies, output);
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            SomeRendererTestData.CreateSomeTestItems(out items, out dependencies);
        }

        public string GetHelp(bool detailedHelp) {
            return _delegate.GetHelp();
        }

        public string GetMasterFileName(string argsAsString, string baseFileName) {
            return _delegate.GetMasterFileName(argsAsString, baseFileName);
        }
    }

    public class GenericDotRenderer : IRenderer<IEdge> {
        private void Render(/*IEnumerable<INode> nodes, */IEnumerable<IEdge> edges, [NotNull] TextWriter output, int? stringLengthForIllegalEdges) {
            IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges = Dependency.Edges2NodesAndEdges(edges);

            output.WriteLine("digraph D {");
            output.WriteLine("ranksep = 1.5;");

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                output.WriteLine("\"" + n.Name + "\" [shape=" + (n.IsInner ? "box,style=bold" : "oval") + "];");
            }

            output.WriteLine();

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                foreach (var e in nodesAndEdges[n].Where(e => e.UsingNode.IsInner || e.UsedNode.IsInner)) {
                    output.WriteLine(e.GetDotRepresentation(stringLengthForIllegalEdges));
                }
            }

            output.WriteLine("}");
        }

        public void Render(IEnumerable<IEdge> dependencies, string argsAsString, [CanBeNull] string baseFileName) {
            int stringLengthForIllegalEdges = -1;
            Options.Parse(argsAsString,
                new OptionAction('e', (args, j) => {
                    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out stringLengthForIllegalEdges)) {
                        Options.Throw("No valid length after e", args);
                    }
                    return j;
                }));
            using (TextWriter sw = new StreamWriter(GetDotFileName(baseFileName))) {
                Render(dependencies, sw, stringLengthForIllegalEdges);
            }
        }

        private string GetDotFileName(string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".dot");
        }

        public void RenderToStreamForUnitTests(IEnumerable<IEdge> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                Render(dependencies, sw, null);
            }
        }

        public string GetHelp() {
            return
@"  Writes dependencies to file in .dot format (graphviz; see http://graphviz.org/).
  This is helpful for smaller dependency graphs without any programming.
  For larger graphs, it is better to use or define a renderer that creates a
  specific structure, e.g., a ModulesAndInterfacesRenderer.

  Options: [-e #] -o fileName | fileName
    -e #          cutoff length of text for wrong dependencies; default: no cutoff
    fileName      output fileName in .dot (graphviz) format";
        }

        public string GetMasterFileName(string argsAsString, string baseFileName) {
            return GetDotFileName(baseFileName);
        }
    }
}