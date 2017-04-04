using System;
using System.Collections.Generic;
using System.IO;

namespace NDepCheck.Rendering {
    /// <summary>
    /// Writer for dependencies ("Edges") in standard "DIP" format
    /// </summary>
    public class DipWriter : IDependencyRenderer {
        public static void Write(IEnumerable<IEdge> edges, TextWriter sw, bool withExampleInfo) {
            var writtenTypes = new HashSet<ItemType>();

            sw.WriteLine("// Written " + DateTime.Now);
            foreach (var e in edges) {
                WriteItemType(writtenTypes, e.UsingNode.Type, sw);
                WriteItemType(writtenTypes, e.UsedNode.Type, sw);

                sw.WriteLine(e.AsDipStringWithTypes(withExampleInfo));
            }
        }

        private static void WriteItemType(HashSet<ItemType> writtenTypes, ItemType itemType, TextWriter sw) {
            if (writtenTypes.Add(itemType)) {
                sw.WriteLine();
                sw.Write("$ ");
                sw.WriteLine(itemType);
                sw.WriteLine();
            }
        }

        public void Render(IEnumerable<Dependency> dependencies, string argsAsString, string baseFileName) {
            //int stringLengthForIllegalEdges = -1;
            bool withExampleInfo = false;
            Options.Parse(argsAsString, 
                //new OptionAction("e", (args, j) => {
                //    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out stringLengthForIllegalEdges)) {
                //        Options.Throw("No valid length after e", args);
                //    }
                //    return j;
                //}), 
                new OptionAction("n", (args, j) => {
                    withExampleInfo = true;
                    return j;
                }));
            using (var sw = GlobalContext.CreateTextWriter(GetMasterFileName(argsAsString, baseFileName))) { 
                Write(dependencies, sw.Writer, withExampleInfo);
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream output) {
            using (var sw = new StreamWriter(output)) {
                Write(dependencies, sw, true);
            }
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType amo = ItemType.New("AMO:Assembly:Module:Order");

            var bac = Item.New(amo, "BAC:BAC:0100".Split(':'));
            var kst = Item.New(amo, "KST:KST:0200".Split(':'));
            var kah = Item.New(amo, "KAH:KAH:0300".Split(':'));
            var kah_mi = Item.New(amo, "Kah.MI:KAH:0301".Split(':'));
            var vkf = Item.New(amo, "VKF:VKF:0400".Split(':'));

            items = new[] { bac, kst, kah, kah_mi, vkf };

            dependencies = new[] {
                    FromTo(kst, bac), FromTo(kst, kah_mi), FromTo(kah, bac), FromTo(vkf, bac), FromTo(vkf, kst), FromTo(vkf, kah, 3), FromTo(vkf, kah_mi, 2, 2)
                    // ... more to come
                };
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: ct, questionableCt: questionable);
        }

        public string GetHelp(bool detailedHelp) {
            return 
@"  Writes dependencies to .dip files, which can be read in by 
  NDepCheck's DipReader. This is very helpful for building pipelines 
  that process dependencies for different purposes.

  Options: [-n]
    -n       ... each edge contains an example of a dependency; default: do not write example";
        }

        public string GetMasterFileName(string argsAsString, string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, ".dip");
        }
    }
}
