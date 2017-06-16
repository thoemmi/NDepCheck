using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.TextWriting {
    /// <summary>
    /// Writer for dependencies in standard "DIP" format
    /// </summary>
    public class DipWriter : RendererWithOptions<DipWriter.Options> {
        public class Options {
            public bool NoExampleInfo;
            public List<DependencyMatch> Matches = new List<DependencyMatch>();
            public List<DependencyMatch> Excludes = new List<DependencyMatch>();
        }

        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("write");
        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);

        private static readonly Option[] _allOptions = DependencyMatchOptions.WithOptions(NoExampleInfoOption);

        public static int Write([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, ITargetWriter sw, bool withExampleInfo, IEnumerable<DependencyMatch> matches, IEnumerable<DependencyMatch> excludes) {
            var writtenTypes = new HashSet<ItemType>();

            int n = 0;
            foreach (var d in dependencies.Where(d => d.IsMarkerMatch(matches, excludes))) {
                WriteItemType(writtenTypes, d.UsingItem.Type, sw);
                WriteItemType(writtenTypes, d.UsedItem.Type, sw);

                sw.WriteLine(d.AsLimitableStringWithTypes(withExampleInfo, threeLines: false, maxLength: int.MaxValue));
                n++;
            }
            return n;
        }

        private static void WriteItemType(HashSet<ItemType> writtenTypes, ItemType itemType, ITargetWriter sw) {
            if (writtenTypes.Add(itemType)) {
                sw.WriteLine();
                sw.Write("$ ");
                sw.WriteLine(itemType.ToString());
                sw.WriteLine();
            }
        }

        protected override Options CreateRenderOptions(GlobalContext globalContext, string options) {
            var result = new Options();
            DependencyMatchOptions.Parse(globalContext, options, globalContext.IgnoreCase, result.Matches, result.Excludes,
                NoExampleInfoOption.Action((args, j) => {
                    result.NoExampleInfo = true;
                    return j;
                }));
            return result;
        }

        public override void Render([NotNull] GlobalContext globalContext,
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            Options options, [NotNull] WriteTarget target, bool ignoreCase) {
            using (var sw = GetMasterFileName(globalContext, options, target).CreateWriter(logToConsoleInfo: true)) {
                int n = Write(dependencies, sw, !options.NoExampleInfo, options.Matches, options.Excludes);
                Log.WriteInfo($"... written {n} dependencies");
            }
        }

        public override void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream output, string option) {
            using (var sw = new TargetStreamWriter(output)) {
                sw.WriteLine($"// Written {DateTime.Now} by {typeof(DipWriter).Name} in NDepCheck {Program.VERSION}");
                Write(dependencies, sw, withExampleInfo: true, matches: null, excludes: null);
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph renderingGraph) {
            ItemType amo = ItemType.New("AMO(Assembly:Module:Order)");

            var bac = renderingGraph.CreateItem(amo, "BAC:BAC:0100".Split(':'));
            var kst = renderingGraph.CreateItem(amo, "KST:KST:0200".Split(':'));
            var kah = renderingGraph.CreateItem(amo, "KAH:KAH:0300".Split(':'));
            var kah_mi = renderingGraph.CreateItem(amo, "Kah.MI:KAH:0301".Split(':'));
            var vkf = renderingGraph.CreateItem(amo, "VKF:VKF:0400".Split(':'));

            return new[] {
                    FromTo(renderingGraph, kst, bac), FromTo(renderingGraph, kst, kah_mi),
                FromTo(renderingGraph, kah, bac), FromTo(renderingGraph, vkf, bac),
                FromTo(renderingGraph, vkf, kst), FromTo(renderingGraph, vkf, kah, 3),
                FromTo(renderingGraph, vkf, kah_mi, 2, 2)
                    // ... more to come
                };
        }

        private Dependency FromTo(WorkingGraph graph, Item from, Item to, int ct = 1, int questionable = 0) {
            return graph.CreateDependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes dependencies to .dip files, which can be read in by 
  NDepCheck's DipReader. This is very helpful for building pipelines 
  that process dependencies for different purposes.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public override WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, Options options, WriteTarget baseTarget) {
            return GlobalContext.CreateFullFileName(baseTarget, ".dip");
        }
    }
}
