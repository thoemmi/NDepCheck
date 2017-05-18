using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Reading.DipReading {
    internal class DipReader : AbstractDependencyReader {
        private class DipReaderException : Exception {
            public DipReaderException(string msg)
                : base(msg) {
            }
        }

        private class ItemProxy : Item {
            public ItemProxy([NotNull] ItemType type, string[] values, string[] markers) : base(type, values) {
                if (markers.Any()) {
                    throw new ArgumentException($"ItemProxy with markers not allowed: {type.Name}:{AsString()} defined with markers {string.Join("+", markers)}");
                }
            }

            public bool ProxyMatches([NotNull] Item item) {
                if (!Type.Equals(item.Type)) {
                    return false;
                }
                if (Values.Length != item.Values.Length) {
                    return false;
                }
                for (int i = 0; i < Values.Length; i++) {
                    if (Values[i] != "?" && Values[i] != item.Values[i]) {
                        return false;
                    }
                }
                return true;
            }
        }

        private static readonly string[] NO_MARKERS = new string[0];

        private readonly Dictionary<string, ItemType> _registeredItemTypes = new Dictionary<string, ItemType>();

        public DipReader([NotNull] string fileName) : base(Path.GetFullPath(fileName), fileName) {
        }

        public override IEnumerable<Dependency> ReadDependencies(int depth, bool ignoreCase) {
            Log.WriteInfo("Reading " + FullFileName);
            Regex dipArrow = new Regex($@"\s*{Dependency.DIP_ARROW}\s*");

            var result = new List<Dependency>(10000);
            bool thereAreProxies = false;
            using (var sr = new StreamReader(FullFileName)) {
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
                            WriteError(FullFileName, lineNo, $"Line is not ... {Dependency.DIP_ARROW} #;#;... {Dependency.DIP_ARROW} ..., but " + parts.Length, line);
                        }

                        try {
                            Item foundUsingItem = GetOrCreateItem(parts[0].Trim(), itemsDictionary);
                            Item foundUsedItem = GetOrCreateItem(parts[2].Trim(), itemsDictionary);

                            bool pointsToProxy = foundUsingItem is ItemProxy || foundUsedItem is ItemProxy;
                            thereAreProxies |= pointsToProxy;

                            string[] properties = parts[1].Split(new[] { ';' }, 6);
                            int ct, questionableCt, badCt;
                            string dependencyMarkers = Get(properties, 0).TrimStart('\''); // TODO: Remove the Trim ... this seems like a format uncertainty!

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
                            ISourceLocation location = AbstractSourceLocation.Create(source);

                            string exampleInfo = Get(properties, 5);

                            var dependency = new Dependency(foundUsingItem, foundUsedItem, location,
                                dependencyMarkers, ct, questionableCt, badCt, exampleInfo);

                            result.Add(dependency);
                        } catch (DipReaderException ex) {
                            WriteError(FullFileName, lineNo, ex.Message + " - ignoring input line", line);
                        }
                    }
                }

                Log.WriteInfo($"... read {result.Count} dependencies from {FullFileName}");
                if (thereAreProxies) {
                    var proxies = new HashSet<ItemProxy>(itemsDictionary.Keys.OfType<ItemProxy>());
                    Item[] items = itemsDictionary.Keys.Where(i => !(i is ItemProxy)).ToArray();
                    foreach (var item in items) {
                        foreach (var matchingProxy in proxies.Where(p => p.ProxyMatches(item)).ToArray()) {
                            itemsDictionary[matchingProxy] = item;
                            proxies.Remove(matchingProxy);
                        }
                    }

                    return result.Select(d => ResolveItemProxies(d, itemsDictionary)).ToArray();
                } else {
                    return result;
                }
            }
        }

        private Dependency ResolveItemProxies(Dependency d, Dictionary<Item, Item> itemsDictionary) {
            return d.UsingItem is ItemProxy || d.UsedItem is ItemProxy
                ? new Dependency(itemsDictionary[d.UsingItem], itemsDictionary[d.UsedItem],
                    d.Source, d.MarkerSet, d.Ct, d.QuestionableCt, d.BadCt, d.ExampleInfo)
                : d;
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
            string[] valuesAndMarkers = s.Split(new[] { '\'' }, 2);
            string[] prefixAndValues = valuesAndMarkers[0].Split(new[] { ':' }, 2);
            string[] prefix = prefixAndValues[0].Split(';');
            string[] markers = valuesAndMarkers.Length > 1 ? valuesAndMarkers[1].Split('+') : NO_MARKERS;

            string typeName = prefix[0];

            ItemType foundType;
            if (!_registeredItemTypes.TryGetValue(typeName, out foundType)) {
                throw new DipReaderException("ItemType '" + typeName + "' has not been defined in this file previously");
            } else {
                string[] values = prefixAndValues.Length > 1 ? prefixAndValues[1].Split(':', ';') : new string[0];

                return values.Contains("?") ? new ItemProxy(foundType, values, markers) : Item.New(foundType, values, markers);
            }
        }

        private static void WriteError(string fileName, int lineNo, string msg, string line) {
            Log.WriteError(fileName + "/" + lineNo + ": " + msg + " - '" + line + "'");
        }

        public override void SetReadersInSameReadFilesBeforeReadDependencies(IDependencyReader[] readerGang) {
            // empty - we do not need knowledge about neighboring readers
        }
    }
}
