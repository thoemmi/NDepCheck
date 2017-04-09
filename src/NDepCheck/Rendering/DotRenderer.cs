using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Transforming;

namespace NDepCheck.Rendering {
    /// <summary>
    /// Class that creates AT&amp;T DOT (graphviz) output from dependencies - see <a href="http://graphviz.org/">http://graphviz.org/</a>.
    /// </summary>
    public class DotRenderer : IDependencyRenderer {
        private readonly GenericDotRenderer _delegate = new GenericDotRenderer();

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, string argsAsString, string baseFileName, bool ignoreCase) {
            _delegate.Render(globalContext, dependencies, argsAsString, baseFileName, ignoreCase);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            _delegate.RenderToStreamForUnitTests(dependencies, output);
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            SomeRendererTestData.CreateSomeTestItems(out items, out dependencies);
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return _delegate.GetHelp(detailedHelp, filter);
        }

        public string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            return _delegate.GetMasterFileName(globalContext, argsAsString, baseFileName);
        }
    }

    public class GenericDotRenderer : IRenderer<IEdge> {
        public static readonly Option MaxExampleLengthOption = new Option("ml", "max-example-length", "#", "Maximal length of example string", @default:"full example");
        public static readonly Option InnerMatchOption = new Option("im", "inner-item", "#", "Match to mark item as inner item", @default: "all items are inner");

        private static readonly Option[] _allOptions = { MaxExampleLengthOption, InnerMatchOption };

        private void Render(IEnumerable<IEdge> edges, [NotNull] TextWriter output, ItemMatch innerMatch, int? maxExampleLength) {
            IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges = Dependency.Edges2NodesAndEdges(edges);

            output.WriteLine("digraph D {");
            output.WriteLine("ranksep = 1.5;");

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                output.WriteLine("\"" + n.Name + "\" [shape=" + (ItemMatch.Matches(innerMatch, n) ? "box,style=bold" : "oval") + "];");
            }

            output.WriteLine();

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                foreach (var e in nodesAndEdges[n].Where(e => ItemMatch.Matches(innerMatch, e.UsingNode) || ItemMatch.Matches(innerMatch, e.UsedNode))) {
                    output.WriteLine(e.GetDotRepresentation(maxExampleLength));
                }
            }

            output.WriteLine("}");
        }

        public void Render(GlobalContext globalContext, IEnumerable<IEdge> dependencies, string argsAsString, [CanBeNull] string baseFileName, bool ignoreCase) {
            int? maxExampleLength = null;
            ItemMatch innerMatch = null;
            Option.Parse(globalContext, argsAsString,
                MaxExampleLengthOption.Action((args, j) => {
                    maxExampleLength = Option.ExtractIntOptionValue(args, ref j,
                        "No valid length after " + MaxExampleLengthOption.Name);
                    return j;
                }),
                AbstractMatrixRenderer.InnerMatchOption.Action((args, j) => {
                    innerMatch = new ItemMatch(null, Option.ExtractRequiredOptionValue(args, ref j, "Pattern for selecting inner items missing"), ignoreCase);
                    return j;
                }));
            using (TextWriter sw = new StreamWriter(GetDotFileName(baseFileName))) {
                Render(dependencies, sw, innerMatch, maxExampleLength);
            }
        }

        private string GetDotFileName(string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".dot");
        }

        public void RenderToStreamForUnitTests(IEnumerable<IEdge> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                Render(dependencies, sw, null, null);
            }
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes dependencies to file in .dot format (graphviz; see http://graphviz.org/).
  This is helpful for smaller dependency graphs without any programming.
  For larger graphs, it is better to use or define a renderer that creates a
  specific structure, e.g., a ModulesAndInterfacesRenderer.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            return GetDotFileName(baseFileName);
        }
    }
}