using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Markers {
    public static class DictionaryExtensions {
        public static Dictionary<string, int> ToDictionary(this IReadOnlyDictionary<string, int> source, bool ignoreCase) {
            return source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, AbstractMarkerSet.GetComparer(ignoreCase));
        }

        public static int Get([CanBeNull] this IReadOnlyDictionary<string, int> dictionary, string key) {
            if (dictionary == null) {
                return 0;
            } else {
                int result;
                dictionary.TryGetValue(key, out result);
                return result;
            }
        }

        public static void Set([NotNull] this Dictionary<string, int> dictionary, string key, int value) {
            dictionary[key] = value;
        }

        public static void Increment([NotNull] this Dictionary<string, int> dictionary, string key, int increment) {
            dictionary[key] = dictionary.Get(key) + increment;
        }

        public static void UnionWith([NotNull] this Dictionary<string, int> dictionary, IReadOnlyDictionary<string, int> other) {
            foreach (var kvp in other) {
                dictionary[kvp.Key] = dictionary.Get(kvp.Key) + kvp.Value;
            }
        }
    }
}