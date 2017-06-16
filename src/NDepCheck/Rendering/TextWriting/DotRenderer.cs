using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;
using NDepCheck.Rendering.GraphicsRendering;

namespace NDepCheck.Rendering.TextWriting {
    /// <summary>
    /// Class that creates AT&amp;T DOT (graphviz) output from dependencies - see <a href="http://graphviz.org/">http://graphviz.org/</a>.
    /// </summary>
    public class DotRenderer : RendererWithOptions<DotRenderer.Options> {
        public class Options {
            public ItemMatch InnerMatch;
            public int? MaxExampleLength;
        }

        public static readonly Option MaxExampleLengthOption = new Option("ml", "max-example-length", "#", "Maximal length of example string", @default:"full example");
        public static readonly Option InnerMatchOption = new Option("im", "inner-item", "#", "Match to mark item as inner item", @default: "all items are inner");

        private static readonly Option[] _allOptions = { MaxExampleLengthOption, InnerMatchOption };

        private void Render([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, [NotNull] ITargetWriter output, ItemMatch innerMatch, int? maxExampleLength) {
            IDictionary<Item, IEnumerable<Dependency>> itemsAndDependencies = Dependency.Dependencies2ItemsAndDependencies(dependencies);

            output.WriteLine("digraph D {");
            output.WriteLine("ranksep = 1.5;");

            foreach (var n in itemsAndDependencies.Keys.OrderBy(n => n.Name)) {
                output.WriteLine("\"" + n.Name + "\" [shape=" + (ItemMatch.IsMatch(innerMatch, n) ? "box,style=bold" : "oval") + "];");
            }

            output.WriteLine();

            foreach (var n in itemsAndDependencies.Keys.OrderBy(n => n.Name)) {
                foreach (var e in itemsAndDependencies[n].Where(e => ItemMatch.IsMatch(innerMatch, e.UsingItem) || ItemMatch.IsMatch(innerMatch, e.UsedItem))) {
                    output.WriteLine(e.GetDotRepresentation(maxExampleLength));
                }
            }

            output.WriteLine("}");
        }

        protected override Options CreateRenderOptions(GlobalContext globalContext, string options) {
            var result = new Options();
            Option.Parse(globalContext, options,
                MaxExampleLengthOption.Action((args, j) => {
                    result.MaxExampleLength = Option.ExtractIntOptionValue(args, ref j,
                        "No valid length after " + MaxExampleLengthOption.Name);
                    return j;
                }),
                AbstractMatrixRenderer.InnerMatchOption.Action((args, j) => {
                    result.InnerMatch = new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Pattern for selecting inner items missing"), 
                        globalContext.IgnoreCase, anyWhereMatcherOk: true);
                    return j;
                }));
            return result;
        }

        public override void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, 
                    Options options, [NotNull] WriteTarget target, bool ignoreCase) {
            using (var sw = GetDotFileName(target).CreateWriter()) {
                Render(dependencies, sw, options.InnerMatch, options.MaxExampleLength);
            }
        }

        private WriteTarget GetDotFileName(WriteTarget target) {
            return target.ChangeExtension(".dot");
        }

        public override void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream stream, string testOption) {
            using (var sw = new TargetStreamWriter(stream)) {
                Render(dependencies, sw, null, null);
            }
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes dependencies to file in .dot format (graphviz; see http://graphviz.org/).
  This is helpful for smaller dependency graphs without any programming.
  For larger graphs, it is better to use or define a renderer that creates a
  specific structure, e.g., a ModulesAndInterfacesRenderer.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public override WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, Options options, WriteTarget baseTarget) {
            return GetDotFileName(baseTarget);
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph renderingGraph) {
            return RendererSupport.CreateSomeTestItems(renderingGraph);
        }
    }
}