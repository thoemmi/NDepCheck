using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public static class DictionaryExtensions {
        public static TValue Get<TKey, TValue>([CanBeNull] this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) {
            if (dictionary == null) {
                return default(TValue);
            } else {
                TValue result;
                dictionary.TryGetValue(key, out result);
                return result;
            }
        }

        public static void Set<TKey, TValue>([NotNull] this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) {
            dictionary[key] = value;
        }

        public static void Increment([NotNull] this Dictionary<string, int> dictionary, string key, int increment) {
            dictionary[key] = dictionary.Get(key) + increment;
        }

        public static void UnionWith([NotNull] this Dictionary<string, int> dictionary, IReadOnlyDictionary<string, int> other) {
            if (dictionary == other) {
                throw new ArgumentException("Passed dictionaries must not be the same object");
            }
            foreach (var kvp in other) {
                dictionary[kvp.Key] = dictionary.Get(kvp.Key) + kvp.Value;
            }
        }
    }
}