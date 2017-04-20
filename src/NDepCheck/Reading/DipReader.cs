using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Reading {
    internal class DipReader : AbstractDependencyReader {
        private class DipReaderException : Exception {
            public DipReaderException(string msg)
                : base(msg) {
            }
        }

        private readonly Dictionary<string, ItemType> _registeredItemTypes = new Dictionary<string, ItemType>();

        public DipReader([NotNull] string fileName) : base(fileName) {
        }

        protected override IEnumerable<Dependency> ReadDependencies([CanBeNull] InputContext inputContext, int depth) {
            Log.WriteInfo("Reading " + _fullFileName);
            Regex dipArrow = new Regex($@"\s*{Dependency.DIP_ARROW}\s*");

            var result = new List<Dependency>(10000);
            using (var sr = new StreamReader(_fullFileName)) {
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
                    if (line.StartsWith("$")) {
                        ItemType itemType = ItemType.New(line.TrimStart('$').Trim());
                        if (!_registeredItemTypes.ContainsKey(itemType.Name)) {
                            _registeredItemTypes.Add(itemType.Name, itemType);
                        }
                    } else {
                        string[] parts = dipArrow.Split(line);

                        if (parts.Length != 3) {
                            WriteError(_fullFileName, lineNo, $"Line is not ... {Dependency.DIP_ARROW} #;#;... {Dependency.DIP_ARROW} ..., but " + parts.Length, line);
                        }

                        try {
                            Item foundUsingItem = GetOrCreateItem(parts[0].Trim(), itemsDictionary);
                            Item foundUsedItem = GetOrCreateItem(parts[2].Trim(), itemsDictionary);

                            string[] properties = parts[1].Split(new[] { ';' }, 6);
                            int ct, questionableCt, badCt;
                            string dependencyMarkers = Get(properties, 0);
                            if (!int.TryParse(Get(properties, 1, "1"), out ct)) {
                                throw new DipReaderException($"Cannot parse count '{Get(properties, 1)}'");
                            }
                            if (!int.TryParse(Get(properties, 2, "0"), out questionableCt)) {
                                throw new DipReaderException($"Cannot parse questionableCt '{Get(properties, 2)}'");
                            }
                            if (!int.TryParse(Get(properties, 3, "0"), out badCt)) {
                                throw new DipReaderException($"Cannot parse badCt '{Get(properties, 3)}'");
                            }

                            string[] source = Get(properties, 4).Split('|');
                            int sourceLine = -1;
                            if (source.Length > 1) {
                                if (!int.TryParse(source[1], out sourceLine)) {
                                    sourceLine = -1;
                                }
                            }

                            string exampleInfo = Get(properties, 5);

                            var dependency = new Dependency(foundUsingItem, foundUsedItem,
                                string.IsNullOrWhiteSpace(source[0])
                                    ? new TextFileSource(_fullFileName, lineNo)
                                    : new TextFileSource(source[0], sourceLine < 0 ? null : (int?)sourceLine),
                                dependencyMarkers, ct, questionableCt, badCt, exampleInfo, inputContext);

                            result.Add(dependency);
                        } catch (DipReaderException ex) {
                            WriteError(FullFileName, lineNo, ex.Message + " - ignoring input line", line);
                        }
                    }
                }
                Log.WriteInfo($"... read {result.Count} dependencies from {_fullFileName}");
                return result;
            }
        }

        [NotNull]
        private string Get(string[] properties, int i, string @default = "") {
            return i < properties.Length ? properties[i] : @default;
        }

        [NotNull]
        private Item GetOrCreateItem([NotNull] string s, [NotNull] Dictionary<Item, Item> items) {
            Item item = CreateItem(s);
            Item foundItem;
            if (!items.TryGetValue(item, out foundItem)) {
                items.Add(item, foundItem = item);
            }
            return foundItem;
        }

        [NotNull]
        private Item CreateItem(string s) {
            string[] prefixAndValues = s.Split(new [] { ':' }, 2);
            string[] prefix = prefixAndValues[0].Split(';');

            string typeName = prefix[0];

            ItemType foundType;
            if (!_registeredItemTypes.TryGetValue(typeName, out foundType)) {
                throw new DipReaderException("ItemType '" + typeName + "' has not been defined in this file previously");
            } else {
                string[] values = prefixAndValues.Length > 1 ? prefixAndValues[1].Split(':', ';') : new string[0];
                Item result = Item.New(foundType, values);
                return prefix.Length > 1 ? result.SetOrder(prefix[1]) : result;
            }
        }

        private static void WriteError(string fileName, int lineNo, string msg, string line) {
            Log.WriteError(fileName + "/" + lineNo + ": " + msg + " - '" + line + "'");
        }
    }
}
