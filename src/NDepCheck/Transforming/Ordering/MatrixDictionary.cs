using System;
using System.Collections.Generic;
using System.Text;

namespace NDepCheck.Transforming.Ordering {
    public class MatrixDictionary<TKey, TValue> where TValue : new() {
        private readonly Dictionary<TKey, Dictionary<TKey, TValue>> _fromTo = new Dictionary<TKey, Dictionary<TKey, TValue>>();
        private readonly Dictionary<TKey, Dictionary<TKey, TValue>> _toFrom = new Dictionary<TKey, Dictionary<TKey, TValue>>();
        private readonly Dictionary<TKey, TValue> _fromSum = new Dictionary<TKey, TValue>();
        private readonly Dictionary<TKey, TValue> _toSum = new Dictionary<TKey, TValue>();

        private readonly Func<TValue, TValue, TValue> _sum;
        private readonly Func<TValue, TValue, TValue> _diff;

        public override string ToString() {
            var sb = new StringBuilder();
            foreach (var from in FromKeys) {
                sb.Append($"{from,6}");
                sb.Append(" #");
                foreach (var to in _fromTo[from].Keys) {
                    sb.Append($"{to,6}={_fromTo[from][to],6} ");
                }
                sb.AppendLine(_fromSum.ContainsKey(from) ? $"SUM={_fromSum[from],6}" : "SUM=?");
                sb.Append(' ');
            }
            foreach (var to in ToKeys) {
                sb.Append($"{to,6}");
                sb.Append(" |");
                foreach (var from in _toFrom[to].Keys) {
                    sb.Append($"{from,6}={_toFrom[to][from],6} ");
                }
                sb.AppendLine(_toSum.ContainsKey(to) ? $"SUM={_toSum[to],6}" : "SUM=?");
                sb.Append(' ');
            }
            return sb.ToString();
        }

        public MatrixDictionary(Func<TValue, TValue, TValue> sum, Func<TValue, TValue, TValue> diff) {
            _sum = sum;
            _diff = diff;
        }

        public void Add(TKey from, TKey to, TValue value) {
            Get(_fromTo, from).Add(to, value);
            Get(_toFrom, to).Add(from, value);
            _fromSum[from] = _sum(Get(_fromSum, from), value);
            _toSum[to] = _sum(Get(_toSum, to), value);
        }

        public bool RemoveFrom(TKey from) {
            return Remove(from, _fromTo, _fromSum, _toFrom, _toSum);
        }

        public bool RemoveTo(TKey to) {
            return Remove(to, _toFrom, _toSum, _fromTo, _fromSum);
        }

        private bool Remove(TKey key, Dictionary<TKey, Dictionary<TKey, TValue>> fromTo, Dictionary<TKey, TValue> fromSum, Dictionary<TKey, Dictionary<TKey, TValue>> toFrom, Dictionary<TKey, TValue> toSum) {
            Dictionary<TKey, TValue> fromRow = Get(fromTo, key);
            foreach (var kvp in fromRow) {
                var k = kvp.Key;
                Get(toFrom, k).Remove(key);
                toSum[k] = _diff(Get(toSum, k), kvp.Value);
            }
            fromTo.Remove(key);
            return fromSum.Remove(key);
        }

        public IEnumerable<TKey> FromKeys => _fromTo.Keys;

        public IEnumerable<TKey> ToKeys => _toFrom.Keys;

        public Dictionary<TKey, TValue> FromSum => _fromSum;

        public Dictionary<TKey, TValue> ToSum => _toSum;

        public TValue Get(TKey from, TKey to) {
            return Get(Get(_fromTo, from), to);
        }

        public Dictionary<TKey, TValue> GetFrom(TKey from) {
            return Get(_fromTo, from);
        }

        public TValue GetFromSum(TKey from) {
            return Get(_fromSum, from);
        }

        public TValue GetToSum(TKey to) {
            return Get(_toSum, to);
        }

        public static TResult Get<TResult>(Dictionary<TKey, TResult> dict, TKey key) where TResult : new() {
            TResult result;
            if (!dict.TryGetValue(key, out result)) {
                dict.Add(key, result = new TResult());
            }
            return result;
        }
    }
}