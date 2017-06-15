using System;
using System.Collections.Generic;
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

        public static TValue GetOrAdd<TKey, TValue>([NotNull] this IDictionary<TKey, TValue> dictionary,
                                       TKey key, Func<TValue> createNewValue) {
            TValue result;
            if (!dictionary.TryGetValue(key, out result)) {
                dictionary.Add(key, result = createNewValue());
            }
            return result;
        }

        public static void Set<TKey, TValue>([NotNull] this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) {
            dictionary[key] = value;
        }

        public static void Increment<TKey>([NotNull] this Dictionary<TKey, int> dictionary, TKey key, int increment = 1) {
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