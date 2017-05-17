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
    public class DipWriter : IRenderer {
        public static readonly DependencyMatchOptions DependencyMatchOptions = new DependencyMatchOptions("write");
        public static readonly Option NoExampleInfoOption = new Option("ne", "no-example", "", "Does not write example info", @default: false);

        private static readonly Option[] _allOptions = DependencyMatchOptions.WithOptions(NoExampleInfoOption);

        public static int Write(IEnumerable<Dependency> dependencies, TextWriter sw, bool withExampleInfo, IEnumerable<DependencyMatch> matches, IEnumerable<DependencyMatch> excludes) {
            var writtenTypes = new HashSet<ItemType>();

            int n = 0;
            foreach (var d in dependencies.Where(d => d.IsMatch(matches, excludes))) {
                WriteItemType(writtenTypes, d.UsingItem.Type, sw);
                WriteItemType(writtenTypes, d.UsedItem.Type, sw);

                sw.WriteLine(d.AsDipStringWithTypes(withExampleInfo));
                n++;
            }
            return n;
        }

        private static void WriteItemType(HashSet<ItemType> writtenTypes, ItemType itemType, TextWriter sw) {
            if (writtenTypes.Add(itemType)) {
                sw.WriteLine();
                sw.Write("$ ");
                sw.WriteLine(itemType);
                sw.WriteLine();
            }
        }

        public void Render(GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount,
                           string argsAsString, string baseFileName, bool ignoreCase) {
            bool noExampleInfo = false;
            var matches = new List<DependencyMatch>();
            var excludes = new List<DependencyMatch>();
            DependencyMatchOptions.Parse(globalContext, argsAsString, globalContext.IgnoreCase, matches, excludes,
                NoExampleInfoOption.Action((args, j) => {
                    noExampleInfo = true;
                    return j;
                }));
            using (var sw = GlobalContext.CreateTextWriter(GetMasterFileName(globalContext, argsAsString, baseFileName))) {
                int n = Write(dependencies, sw.Writer, !noExampleInfo, matches, excludes);
                Log.WriteInfo($"... written {n} dependencies");
            }
        }

        public void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, IEnumerable<Dependency> dependencies, Stream output, string option) {
            using (var sw = new StreamWriter(output)) {
                sw.WriteLine($"// Written {DateTime.Now} by {typeof(DipWriter).Name} in NDepCheck {Program.VERSION}");
                Write(dependencies, sw, withExampleInfo: true, matches: null, excludes: null);
            }
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies() {
            ItemType amo = ItemType.New("AMO(Assembly:Module:Order)");

            var bac = Item.New(amo, "BAC:BAC:0100".Split(':'));
            var kst = Item.New(amo, "KST:KST:0200".Split(':'));
            var kah = Item.New(amo, "KAH:KAH:0300".Split(':'));
            var kah_mi = Item.New(amo, "Kah.MI:KAH:0301".Split(':'));
            var vkf = Item.New(amo, "VKF:VKF:0400".Split(':'));

            return new[] {
                    FromTo(kst, bac), FromTo(kst, kah_mi), FromTo(kah, bac), FromTo(vkf, bac), FromTo(vkf, kst), FromTo(vkf, kah, 3), FromTo(vkf, kah_mi, 2, 2)
                    // ... more to come
                };
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes dependencies to .dip files, which can be read in by 
  NDepCheck's DipReader. This is very helpful for building pipelines 
  that process dependencies for different purposes.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".dip");
        }
    }
}
