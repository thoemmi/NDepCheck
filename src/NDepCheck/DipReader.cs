using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck {
    internal class DipReader : AbstractDependencyReader {
        private class DipReaderException : Exception {
            public DipReaderException(string msg)
                : base(msg) {
            }
        }

        private readonly DipReaderFactory _factory;

        public DipReader([NotNull] string filename, [NotNull] DipReaderFactory factory) : base (filename) {
            _factory = factory;
        }

        protected override IEnumerable<Dependency> ReadDependencies(int depth) {
            Regex arrow = new Regex(@"\s*->\s*");

            var result = new List<Dependency>(10000);
            using (var sr = new StreamReader(_filename)) {
                var itemsDictionary = new Dictionary<Item, Item>();

                for (int lineNo = 1; ; lineNo++) {
                    string line = sr.ReadLine();
                    if (line == null) {
                        break;
                    }
                    // Remove comments
                    line = Regex.Replace(line, "//.*$", "").Trim();
                    if (line == "") {
                        continue;
                    }
                    if (!arrow.IsMatch(line)) {
                        string[] parts = line.Split(' ', '\t', ':');
                        RegisterType(parts[0], parts.Skip(1).Select(p => p.Split('.')));
                    } else {
                        string[] parts = arrow.Split(line);

                        if (parts.Length != 3) {
                            WriteError(_filename, lineNo, "Line is not ... -> #;#;... -> ..., but " + parts.Length, line);
                        }

                        try {
                            Item foundUsingItem = GetOrCreateItem(parts[0].Trim(), itemsDictionary);
                            Item foundUsedItem = GetOrCreateItem(parts[2].Trim(), itemsDictionary);

                            string[] properties = parts[1].Split(new[] { ';' }, 3);
                            int ct, notOkCt;
                            if (!int.TryParse(properties[0], out ct)) {
                                throw new DipReaderException("Cannot parse count: " + properties[0]);
                            }
                            if (!int.TryParse(properties[1], out notOkCt)) {
                                throw new DipReaderException("Cannot parse notOkCount: " + properties[1]);
                            }

                            var dependency = new Dependency(foundUsingItem, foundUsedItem, _filename, lineNo, 0, lineNo, line.Length, ct, notOkCt);

                            result.Add(dependency);
                        } catch (DipReaderException ex) {
                            WriteError(FileName, lineNo, ex.Message + " - ignoring input line", line);
                        }
                    }
                }
                return result;
            }
        }

        private void RegisterType([NotNull] string name, [NotNull] IEnumerable<string[]> keySubKeyPairs) {
            _factory.AddItemType(ItemType.New(name, keySubKeyPairs.Select(pair => pair[0]).ToArray(), keySubKeyPairs.Select(pair => pair.Length > 1 ? pair[1] : "").ToArray()));
        }

        [NotNull]
        private Item GetOrCreateItem([NotNull] string part, [NotNull] Dictionary<Item, Item> items) {
            Item item = CreateItem(part);
            Item foundItem;
            if (!items.TryGetValue(item, out foundItem)) {
                items.Add(item, foundItem = item);
            }
            return foundItem;
        }

        [NotNull]
        private Item CreateItem(string s) {
            string[] parts = s.Split(':', ';');

            string descriptorName = parts.First();
            ItemType foundType = _factory.GetDescriptor(descriptorName);

            if (foundType == null) {
                throw new DipReaderException("Descriptor '" + descriptorName + "' has not been defined in this file previously");
            } else {
                return Item.New(foundType, parts.Skip(1).ToArray());
            }
        }

        private static void WriteError(string filename, int lineNo, string msg, string line) {
            Log.WriteError(filename + "/" + lineNo + ": " + msg + " - '" + line + "'");
        }
    }

}
