using System;
using System.Collections.Generic;
using System.IO;

namespace NDepCheck {
    /// <summary>
    /// Writer for dependencies ("Edges") in standard "DIP" format
    /// </summary>
    internal static class DipWriter {
        public static void Write(IEnumerable<IEdge> edges, string filename) {
            var writtenTypes = new HashSet<ItemType>();

            using (var sw = new StreamWriter(filename)) {
                sw.WriteLine("// Written " + DateTime.Now);
                sw.WriteLine();
                foreach (var e in edges) {
                    WriteItemType(writtenTypes, e.UsingNode.Type, sw);
                    WriteItemType(writtenTypes, e.UsedNode.Type, sw);

                    sw.WriteLine(e.AsStringWithTypes());
                }
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
    }
}
