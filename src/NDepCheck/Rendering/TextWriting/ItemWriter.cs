using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Rendering.TextWriting {
    /// <summary>
    /// Writer for items in standard format
    /// </summary>
    public class ItemWriter : RendererWithOptions<ItemWriter.Options> {
        public class Options {
            [NotNull, ItemNotNull]
            public List<ItemMatch> ItemMatches = new List<ItemMatch>();
            [NotNull, ItemNotNull]
            public List<ItemMatch> ItemExcludes = new List<ItemMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> IndegreeMatches = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> IndegreeExcludes = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> OutdegreeMatches = new List<DependencyMatch>();
            [NotNull, ItemNotNull]
            public List<DependencyMatch> OutdegreeExcludes = new List<DependencyMatch>();
            public bool WriteOnlyIfIndegreeNotZero;
            public bool WriteOnlyIfOutdegreeNotZero;
            public bool ShowMarkers;
        }

        public static readonly Option MatchOption = new Option("im", "item-match", "&", "Match to select items to write", @default: "all items are written", multiple: true);
        public static readonly Option NoMatchOption = new Option("nm", "no-match", "&", "Match to exclude items", @default: "no items are excluded", multiple: true);

        public static readonly Option IndegreeMatchOption = new Option("di", "indegree-match", "&", "Match to select dependencies for indegree counting", @default: "all incoming dependencies are counted", multiple: true);
        public static readonly Option IndegreeNoMatchOption = new Option("ni", "indegree-no-match", "&", "Match to exclude dependencies for indegree counting", @default: "no incoming dependencies are excluded", multiple: true);
        public static readonly Option OutdegreeMatchOption = new Option("do", "outdegree-match", "&", "Match to select dependencies for outdegree counting", @default: "all incoming dependencies are counted", multiple: true);
        public static readonly Option OutdegreeNoMatchOption = new Option("no", "outdegree-no-match", "&", "Match to exclude dependencies for outdegree counting", @default: "no incoming dependencies are excluded", multiple: true);
        public static readonly Option IndegreeNotZeroOption = new Option("ei", "exist-in", "", "Write item only if indegree is > 0", @default: false);
        public static readonly Option OutdegreeNotZeroOption = new Option("eo", "exist-out", "", "Write item only if outdegree is > 0", @default: false);

        public static readonly Option ShowMarkersOption = new Option("sm", "show-markers", "", "Show markers on written items", @default: false);
        public static readonly Option ProjectionOption = new Option("pi", "project-item", "&", "Project item for writing", @default: "all fields are shown", multiple: true);

        private static readonly Option[] _allOptions = {
            MatchOption, NoMatchOption,
            IndegreeMatchOption, IndegreeNoMatchOption, OutdegreeMatchOption, OutdegreeNoMatchOption,
            ShowMarkersOption, ProjectionOption
        };


        protected override Options CreateRenderOptions(GlobalContext globalContext, string options) {
            var result = new Options();
            Option.Parse(globalContext, options,
                MatchOption.Action((args, j) => {
                    result.ItemMatches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item match"), globalContext.IgnoreCase, anyWhereMatcherOk: true));
                    return j;
                }),
                NoMatchOption.Action((args, j) => {
                    result.ItemExcludes.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item match"), globalContext.IgnoreCase, anyWhereMatcherOk: true));
                    return j;
                }),
                IndegreeMatchOption.Action((args, j) => {
                    result.IndegreeMatches.Add(DependencyMatch.Create(Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency match"), globalContext.IgnoreCase));
                    return j;
                }),
                IndegreeNoMatchOption.Action((args, j) => {
                    result.IndegreeExcludes.Add(DependencyMatch.Create(Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency match"), globalContext.IgnoreCase));
                    return j;
                }),
                OutdegreeMatchOption.Action((args, j) => {
                    result.OutdegreeMatches.Add(DependencyMatch.Create(Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency match"), globalContext.IgnoreCase));
                    return j;
                }),
                OutdegreeNoMatchOption.Action((args, j) => {
                    result.OutdegreeExcludes.Add(DependencyMatch.Create(Option.ExtractRequiredOptionValue(args, ref j, "Missing dependency match"), globalContext.IgnoreCase));
                    return j;
                }),
                IndegreeNotZeroOption.Action((args, j) => {
                    result.WriteOnlyIfIndegreeNotZero = true;
                    return j;
                }),
                OutdegreeNotZeroOption.Action((args, j) => {
                    result.WriteOnlyIfOutdegreeNotZero = true;
                    return j;
                }),
                ShowMarkersOption.Action((args, j) => {
                    result.ShowMarkers = true;
                    return j;
                }));
            return result;
        }

        public override void Render([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Options options, [NotNull] WriteTarget target, bool ignoreCase) {

            WriteTarget masterFile = GetMasterFileName(globalContext, options, target);
            using (var sw = masterFile.CreateWriter()) {
                bool writeHeader = masterFile.IsConsoleOut;
                if (!writeHeader) {
                    sw.WriteLine($"// Written {DateTime.Now} by {typeof(ItemWriter).Name} in NDepCheck {Program.VERSION}");
                    sw.WriteLine();
                }

                Write(dependencies, sw, options.ItemMatches, options.ItemExcludes, options.IndegreeMatches, options.IndegreeExcludes,
                      options.OutdegreeMatches, options.OutdegreeExcludes, options.WriteOnlyIfIndegreeNotZero,
                      options.WriteOnlyIfOutdegreeNotZero, options.ShowMarkers, ignoreCase);
            }
        }

        public override void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext,
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Stream output, string option) {
            using (var sw = new TargetStreamWriter(output)) {
                Write(dependencies, sw, itemMatches: null, itemExcludes: null, indegreeMatches: null, indegreeExcludes: null,
                      outdegreeMatches: null, outdegreeExcludes: null,
                      writeOnlyIfIndegreeNotZero: false, writeOnlyIfOutdegreeNotZero: false, showMarkers: true, ignoreCase: false);
            }
        }

        private void Write([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, ITargetWriter sw, List<ItemMatch> itemMatches, List<ItemMatch> itemExcludes,
                            List<DependencyMatch> indegreeMatches, List<DependencyMatch> indegreeExcludes,
                            List<DependencyMatch> outdegreeMatches, List<DependencyMatch> outdegreeExcludes,
                            bool writeOnlyIfIndegreeNotZero, bool writeOnlyIfOutdegreeNotZero, bool showMarkers, bool ignoreCase) {
            ISet<Item> items = Dependency.GetAllItems(dependencies, i => i.IsMatch(itemMatches, itemExcludes));

            Dictionary<Item, Dependency[]> incoming = Item.CollectIncomingDependenciesMap(dependencies,
                i => items.Contains(i));
            Dictionary<Item, Dependency[]> outgoing = Item.CollectOutgoingDependenciesMap(dependencies,
                i => items.Contains(i));

            List<Item> itemsAsList = items.ToList();
            itemsAsList.Sort(
                (i1, i2) =>
                    string.Compare(i1.AsFullString(), i2.AsFullString(),
                        ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture));

            var writtenTypes = new HashSet<ItemType>();
            int n = 0;
            foreach (var i in itemsAsList) {
                ItemType itemType = i.Type;
                if (writtenTypes.Add(itemType)) {
                    sw.Write("$ ");
                    sw.WriteLine(itemType.ToString());
                }

                int ict = GetCount(incoming, i, indegreeMatches, indegreeExcludes);
                int oct = GetCount(outgoing, i, outdegreeMatches, outdegreeExcludes);

                if (ict == 0 && writeOnlyIfIndegreeNotZero) {
                    // dont write
                } else if (oct == 0 && writeOnlyIfOutdegreeNotZero) {
                    // dont write
                } else {
                    n++;
                    sw.WriteLine($"{"--" + ict,7}->*--{oct + "->",-7} {(showMarkers ? i.AsFullString() : i.AsString())}");
                }
            }

            Log.WriteInfo($"... written {n} items");
        }

        private int GetCount(Dictionary<Item, Dependency[]> adjacency, Item i, List<DependencyMatch> matches, List<DependencyMatch> excludes) {
            Dependency[] dependencies;
            return adjacency.TryGetValue(i, out dependencies) ? dependencies.Count(d => d.IsMarkerMatch(matches, excludes)) : 0;
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph renderingGraph) {
            ItemType amo = ItemType.New("AMO(Assembly:Module:Order)");

            var bac = renderingGraph.CreateItem(amo, "BAC:BAC:0100".Split(':'), "area".Split(','));
            var kst = renderingGraph.CreateItem(amo, "KST:KST:0200".Split(':'), "area".Split(','));
            var kah = renderingGraph.CreateItem(amo, "KAH:KAH:0300".Split(':'), "area".Split(','));
            var kah_mi = renderingGraph.CreateItem(amo, "Kah.MI:KAH:0301".Split(':'), "area,mi".Split(','));
            var vkf = renderingGraph.CreateItem(amo, "VKF:VKF:0400".Split(':'), "area".Split(','));


            return new[] {
                Dep(renderingGraph, kst, bac),
                Dep(renderingGraph, kst, kah_mi),
                Dep(renderingGraph, kah, bac),
                Dep(renderingGraph, vkf, bac),
                Dep(renderingGraph, vkf, kst),
                Dep(renderingGraph, vkf, kah, 3),
                Dep(renderingGraph, vkf, kah_mi, 2, 2)
                // ... more to come
            };
        }

        private Dependency Dep(WorkingGraph graph, Item from, Item to, int ct = 1, int questionable = 0) {
            return graph.CreateDependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: ct, questionableCt: questionable,
                notOkReason: questionable > 0 ? "testdata" : null);
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes items to .txt files.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public override WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, Options options, WriteTarget baseTarget) {
            return GlobalContext.CreateFullFileName(baseTarget, ".txt");
        }
    }
}
