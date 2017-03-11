using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public class DotRenderer : IDependencyRenderer {
        public void RenderToFile(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string baseFilename, int? optionsStringLength) {
            new GenericDotRenderer().RenderToFile(items, dependencies, baseFilename, optionsStringLength);
        }

        public void RenderToStream(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, Stream output, int? optionsStringLength) {
            new GenericDotRenderer().RenderToStream(items, dependencies, output, optionsStringLength);
        }
    }

    public class GenericDotRenderer : IRenderer<INode, IEdge> {
        private void Render(/*IEnumerable<INode> nodes, */IEnumerable<IEdge> edges, [NotNull] StreamWriter output, int? stringLengthForIllegalEdges) {
            IEnumerable<IEdge> visibleEdges = edges.Where(e => !e.Hidden);

            IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges = DependencyGrapher.Edges2NodesAndEdges(visibleEdges);

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

        public void RenderToFile(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, [NotNull] string baseFilename, int? optionsStringLength) {
            string filename = Path.ChangeExtension(baseFilename, ".dot");
            using (var sw = new StreamWriter(filename)) {
                Render(/*items,*/ dependencies, sw, optionsStringLength);
            }
        }

        public void RenderToStream(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, Stream stream, int? optionsStringLength) {
            using (var sw = new StreamWriter(stream)) {
                Render(/*items,*/ dependencies, sw, optionsStringLength);
            }
        }
    }
}