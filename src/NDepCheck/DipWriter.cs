using System;
using System.Collections.Generic;
using System.IO;
using NDepCheck.Rendering;

namespace NDepCheck {
    /// <summary>
    /// Writer for dependencies ("Edges") in standard "DIP" format
    /// </summary>
    public class DipWriter : IDependencyRenderer {
        public static void Write(IEnumerable<IEdge> edges, StreamWriter sw, bool withNotOkExampleInfo) {

                var writtenTypes = new HashSet<ItemType>();

                sw.WriteLine("// Written " + DateTime.Now);
                sw.WriteLine();
                foreach (var e in edges) {
                    WriteItemType(writtenTypes, e.UsingNode.Type, sw);
                    WriteItemType(writtenTypes, e.UsedNode.Type, sw);

                    sw.WriteLine(e.AsStringWithTypes(withNotOkExampleInfo));
                }            
        }

        private static void WriteItemType(HashSet<ItemType> writtenTypes, ItemType itemType, StreamWriter sw) {
            if (writtenTypes.Add(itemType)) {
                sw.Write("// ITEMTYPE ");
                sw.WriteLine(itemType.Name);
                sw.Write(itemType.Name);
                for (int i = 0; i < itemType.Keys.Length; i++) {
                    sw.Write(' ');
                    sw.Write(itemType.Keys[i]);
                    sw.Write(itemType.SubKeys[i]);
                }
                sw.WriteLine();
                sw.WriteLine();
            }
        }

        public void Render(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string argsAsString) {
            //int stringLengthForIllegalEdges = -1;
            string baseFilename = null;
            bool withNotOkExampleInfo = false;
            Options.Parse(argsAsString, arg => baseFilename = arg,
                //new OptionAction('e', (args, j) => {
                //    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out stringLengthForIllegalEdges)) {
                //        Options.Throw("No valid length after e", args);
                //    }
                //    return j;
                //}), 
                new OptionAction('n', (args, j) => {
                    withNotOkExampleInfo = true;
                    return j;
                }), new OptionAction('o', (args, j) => {
                    baseFilename = Options.ExtractOptionValue(args, ref j);
                    return j;
                }));
            if (baseFilename == null) {
                Options.Throw("No filename set with option o", argsAsString);
            }
            string filename = Path.ChangeExtension(baseFilename, ".dip");

            using (var sw = new StreamWriter(filename)) {
                Write(dependencies, sw, withNotOkExampleInfo);
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, Stream output) {
            using (var sw = new StreamWriter(output)) {
                Write(dependencies, sw, true);
            }
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            throw new NotImplementedException();
        }
    }
}
