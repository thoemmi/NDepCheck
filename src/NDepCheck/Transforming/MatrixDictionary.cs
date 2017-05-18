using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public class MatrixDictionary {
        [NotNull]
        public static MatrixDictionary<Item, int> CreateCounts([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            [NotNull] Func<Dependency, int> getCount) {
            var aggregated = new Dictionary<FromTo, Dependency>();
            foreach (var d in dependencies) {
                new FromTo(d.UsingItem, d.UsedItem).AggregateDependency(d, aggregated);
            }

            var aggregatedCounts = new MatrixDictionary<Item, int>((s, i) => s + i, (s, i) => s - i);
            foreach (var kvp in aggregated) {
                aggregatedCounts.Add(kvp.Key.From, kvp.Key.To, getCount(kvp.Value));
            }
            return aggregatedCounts;
        }

    }

    public class MatrixDictionary<TRowKey, TColumnKey, TValue> : MatrixDictionary where TValue : new() {
        private readonly Dictionary<TRowKey, Dictionary<TColumnKey, TValue>> _rows =
            new Dictionary<TRowKey, Dictionary<TColumnKey, TValue>>();

        private readonly Dictionary<TColumnKey, Dictionary<TRowKey, TValue>> _columns =
            new Dictionary<TColumnKey, Dictionary<TRowKey, TValue>>();

        private readonly Dictionary<TRowKey, TValue> _rowSums = new Dictionary<TRowKey, TValue>();
        private readonly Dictionary<TColumnKey, TValue> _columnSums = new Dictionary<TColumnKey, TValue>();

        private readonly Func<TValue, TValue, TValue> _sum;
        private readonly Func<TValue, TValue, TValue> _diff;

        public override string ToString() {
            var sb = new StringBuilder();
            foreach (var rowKey in RowKeys) {
                sb.Append($"{rowKey,6}");
                sb.Append(" #");
                if (_rows.ContainsKey(rowKey)) {
                    foreach (var columnKey in _rows[rowKey].Keys) {
                        sb.Append($"{columnKey,6}={_rows[rowKey][columnKey],6} ");
                    }
                } else {
                    sb.Append("---   ");
                }
                sb.AppendLine(_rowSums.ContainsKey(rowKey) ? $"SUM={_rowSums[rowKey],6}" : "SUM=?");
                sb.Append(' ');
            }
            foreach (var columnKey in _columns.Keys) {
                sb.Append($"{columnKey,6}");
                sb.Append(" |");
                if (_columns.ContainsKey(columnKey)) {
                    foreach (var rowKey in _columns[columnKey].Keys) {
                        sb.Append($"{rowKey,6}={_columns[columnKey][rowKey],6} ");
                    }
                } else {
                    sb.Append("---   ");
                }
                sb.AppendLine(_columnSums.ContainsKey(columnKey) ? $"SUM={_columnSums[columnKey],6}" : "SUM=?");
                sb.Append(' ');
            }
            return sb.ToString();
        }

        public MatrixDictionary(Func<TValue, TValue, TValue> sum, Func<TValue, TValue, TValue> diff) {
            _sum = sum;
            _diff = diff;
        }

        public void Add(TRowKey rowKey, TColumnKey columnKey, TValue value) {
            Get(_rows, rowKey).Add(columnKey, value);
            Get(_columns, columnKey).Add(rowKey, value);
            _rowSums[rowKey] = _sum(Get(_rowSums, rowKey), value);
            _columnSums[columnKey] = _sum(Get(_columnSums, columnKey), value);
        }

        public bool RemoveRow(TRowKey rowKey) {
            return Remove(rowKey, _rows, _rowSums, _columns, _columnSums);
        }

        public bool RemoveColumn(TColumnKey solumnKey) {
            return Remove(solumnKey, _columns, _columnSums, _rows, _rowSums);
        }

        private bool Remove<TKeyR, TKeyC>(TKeyR rk, Dictionary<TKeyR, Dictionary<TKeyC, TValue>> rs,
            Dictionary<TKeyR, TValue> rSum, Dictionary<TKeyC, Dictionary<TKeyR, TValue>> cs,
            Dictionary<TKeyC, TValue> cSum) {
            Dictionary<TKeyC, TValue> r = Get(rs, rk);
            foreach (var ck in cs.Keys) {
                Get(cs, ck).Remove(rk);
                cSum[ck] = _diff(Get(cSum, ck), Get(r, ck));
            }
            rs.Remove(rk);
            return rSum.Remove(rk);
        }

        public IEnumerable<TRowKey> RowKeys => _rowSums.Keys;

        public IEnumerable<TColumnKey> ColumnKeys => _columnSums.Keys;

        public Dictionary<TRowKey, TValue> RowSums => _rowSums;

        public Dictionary<TColumnKey, TValue> ColumnSums => _columnSums;

        public TValue Get(TRowKey from, TColumnKey to) {
            return Get(Get(_rows, from), to);
        }

        public Dictionary<TColumnKey, TValue> GetRow(TRowKey from) {
            return Get(_rows, from);
        }

        public TValue GetRowSum(TRowKey from) {
            return Get(_rowSums, from);
        }

        public TValue GetColumnSum(TColumnKey to) {
            return Get(_columnSums, to);
        }

        public static TResult Get<TKey, TResult>(Dictionary<TKey, TResult> dict, TKey key) where TResult : new() {
            TResult result;
            if (!dict.TryGetValue(key, out result)) {
                dict.Add(key, result = new TResult());
            }
            return result;
        }
    }

    public class MatrixDictionary<TKey, TValue> : MatrixDictionary<TKey, TKey, TValue> where TValue : new() {
        public MatrixDictionary(Func<TValue, TValue, TValue> sum, Func<TValue, TValue, TValue> diff) : base(sum, diff) {
        }
    }
}