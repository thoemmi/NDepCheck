using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Markers;
using NDepCheck.Matching;

namespace NDepCheck {
    public abstract class AbstractDependency<TItem> : IMarkerSet, IWithCt where TItem : AbstractItem<TItem> {
        protected AbstractDependency([NotNull] TItem usingItem, [NotNull] TItem usedItem, ISourceLocation source) {
            UsingItem = usingItem;
            UsedItem = usedItem;
            Source = source;
        }

        public const string DIP_ARROW = "=>";

        [NotNull]
        public TItem UsingItem { get; }
        [NotNull]
        public TItem UsedItem { get; }
        [NotNull]
        protected abstract IMarkerSet MarkerSet { get; }

        public abstract int Ct { get; }
        public abstract int QuestionableCt { get; }
        public abstract int BadCt { get; }

        [CanBeNull]
        public ISourceLocation Source { get; }
        /// <value>
        /// A guess where the use occurs in the
        /// original source file.
        /// </value>
        [CanBeNull]
        public abstract string ExampleInfo { get; }

        public IEnumerable<string> Markers => MarkerSet.Markers;

        public bool IsMatch(IEnumerable<IMatcher> present, IEnumerable<IMatcher> absent) {
            return MarkerSet.IsMatch(present, absent);
        }
        /// <summary>
        /// Coded name of using item.
        /// </summary>
        [NotNull]
        public string UsingItemAsString => UsingItem.AsString();

        /// <value>
        /// Coded name of used item.
        /// </value>
        [NotNull]
        public string UsedItemAsString => UsedItem.AsString();

        /// <summary>
        /// String representation of a Dependency.
        /// </summary>
        public override string ToString() {
            return UsingItem + " ---> " + UsedItem;
        }

        public int NotOkCt => QuestionableCt + BadCt;

        public int OkCt => Ct - QuestionableCt - BadCt;

        public bool IsMatch([CanBeNull] IEnumerable<DependencyMatch> matches, [CanBeNull] IEnumerable<DependencyMatch> excludes) {
            return (matches == null || !matches.Any() || matches.Any(m => m.IsMatch(this))) &&
                   (excludes == null || !excludes.Any() || !excludes.Any(m => m.IsMatch(this)));
        }

        public static ISet<Item> GetAllItems(IEnumerable<Dependency> dependencies, Func<Item, bool> selectItem) {
            var items = new HashSet<Item>(dependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
            return selectItem == null ? items : new HashSet<Item>(items.Where(i => selectItem(i)));
        }

        /// <summary>
        /// A message presented to the user if this Dependency has a <see cref="BadCt"/>or <see cref="QuestionableCt"/>.
        /// </summary>
        /// <returns></returns>
        public string NotOkMessage() {
            string nounTail = Ct > 1 ? "ependencies" : "ependency";
            string prefix = BadCt > 0
                ? QuestionableCt > 0 ? "Bad and questionable d" : "Bad d"
                : QuestionableCt > 0 ? "Questionable d" : "D";
            string ct = BadCt > 0
                ? QuestionableCt > 0 ? $";{QuestionableCt};{BadCt}" : $";;{BadCt}"
                : QuestionableCt > 0 ? $";{QuestionableCt};" : ";;";
            string markers = Markers.Any() ? "'" + string.Join("+", Markers) : "";
            return $"{prefix}{nounTail} {UsingItem} --{ct}{markers}-> {UsedItem}" + (Source != null ? (Ct > 1 ? " (e.g. at " : " (at ") + Source + ")" : "");
        }

        public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
            // TODO: ?? And there should be a flag (in Edge?) "hasNotOkInfo", depending on whether dependency checking was done or not.
            return "\"" + UsingItem.Name + "\" -> \"" + UsedItem.Name + "\" ["
                       + GetDotLabel(stringLengthForIllegalEdges)
                       + GetDotFontSize()
                       + GetDotEdgePenWidthAndWeight()
                       + "];";
        }

        private string GetDotFontSize() {
            return " fontsize=" + (10 + 5 * Math.Round(Math.Log10(Ct)));
        }

        private string GetDotEdgePenWidthAndWeight() {
            double v = 1 + Math.Round(3 * Math.Log10(Ct));
            return " penwidth=" + v.ToString(CultureInfo.InvariantCulture) + (v < 5 ? " constraint=false" : "");
            //return " penwidth=" + v;
            //return " penwidth=" + v + " weight=" + v;
        }

        private string GetDotLabel(int? stringLengthForIllegalEdges) {
            return "label=\"" + (stringLengthForIllegalEdges.HasValue && ExampleInfo != null
                                ? LimitWidth(ExampleInfo, stringLengthForIllegalEdges.Value) + "\\n"
                                : "") +
                            " (" + Ct + (QuestionableCt + BadCt > 0 ? "(" + QuestionableCt + "?," + BadCt + "!)" : "") + ")" +
                            "\"";
        }

        private static string LimitWidth(string s, int lg) {
            if (s.Length > lg) {
                s = "..." + s.Substring(s.Length - lg + 3);
            }
            return s;
        }

        public string AsDipStringWithTypes(bool withExampleInfo) {
            // Should that be in DipWriter?
            string exampleInfo = withExampleInfo ? ExampleInfo : null;
            string markers = string.Join("+", Markers.OrderBy(s => s));
            return $"{UsingItem.AsFullString()} {DIP_ARROW} "
                 + $"{markers};{Ct};{QuestionableCt};{BadCt};{Source?.AsDipString()};{exampleInfo} "
                 + $"{DIP_ARROW} {UsedItem.AsFullString()}";
        }
    }

    public class ReadOnlyDependency : AbstractDependency<ReadOnlyItem> {
        public ReadOnlyDependency(ReadOnlyItem usingItem, ReadOnlyItem usedItem, ISourceLocation source, IMarkerSet markerSet,
                                   int ct, int questionableCt, int badCt, string exampleInfo) : base(usingItem, usedItem, source) {
            MarkerSet = markerSet;
            Ct = ct;
            QuestionableCt = questionableCt;
            BadCt = badCt;
            ExampleInfo = exampleInfo;
        }

        protected override IMarkerSet MarkerSet { get; }
        public override int Ct { get; }
        public override int QuestionableCt { get; }
        public override int BadCt { get; }
        public override string ExampleInfo { get; }
    }

    public class Dependency : AbstractDependency<Item>, IMutableMarkerSet {
        [NotNull]
        private readonly MutableMarkerSet _markerSet;

        private int _ct;
        private int _questionableCt;
        private int _badCt;

        [CanBeNull]
        private string _exampleInfo;

        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem, [CanBeNull] ISourceLocation source,
            [NotNull] string markers, int ct, int questionableCt = 0, int badCt = 0,
            [CanBeNull] string exampleInfo = null) : this(
                usingItem, usedItem, source, markers: markers.Split('&', '+', ','), ct: ct,
                questionableCt: questionableCt, badCt: badCt, exampleInfo: exampleInfo) {
        }

        protected override IMarkerSet MarkerSet => _markerSet;

        /// <summary>
        /// Create a dependency.
        /// </summary>
        /// <param name="usingItem">The using item.</param>
        /// <param name="usedItem">The used item.</param>
        /// <param name="source">Name of the file.</param>
        /// <param name="markers"></param>
        /// <param name="ct"></param>
        /// <param name="questionableCt"></param>
        /// <param name="badCt"></param>
        /// <param name="exampleInfo"></param>
        /// <param name="ignoreCase"></param>
        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [CanBeNull] IEnumerable<string> markers,
            int ct, int questionableCt = 0, int badCt = 0, [CanBeNull] string exampleInfo = null,
            bool? ignoreCase = null) : base(usingItem, usedItem, source) {
            if (usingItem == null) {
                throw new ArgumentNullException(nameof(usingItem));
            }
            if (usedItem == null) {
                throw new ArgumentNullException(nameof(usedItem));
            }
            _ct = ct;
            _questionableCt = questionableCt;
            _badCt = badCt;
            _exampleInfo = exampleInfo;
            _markerSet = new MutableMarkerSet(ignoreCase ?? usingItem.Type.IgnoreCase | usedItem.Type.IgnoreCase, markers);
        }

        public override int Ct => _ct;

        public override int QuestionableCt => _questionableCt;

        public override int BadCt => _badCt;

        public override string ExampleInfo => _exampleInfo;

        public void MarkAsBad() {
            SetBadCount(_ct);
        }

        public void IncrementBad() {
            SetBadCount(_badCt + 1);
        }

        public void ResetBad() {
            _badCt = 0;
        }

        private void SetBadCount(int value) {
            // First bad example overrides any previous example
            if (_badCt == 0 || _exampleInfo == null) {
                _exampleInfo = UsingItemAsString + " ---! " + UsedItemAsString;
            }
            _badCt = value;
        }

        public void MarkAsQuestionable() {
            SetQuestionableCount(_ct);
        }

        public void IncrementQuestionable() {
            SetQuestionableCount(_questionableCt + 1);
        }

        public void ResetQuestionable() {
            _questionableCt = 0;
        }

        private void SetQuestionableCount(int value) {
            if (_badCt == 0 && _questionableCt == 0 || _exampleInfo == null) {
                _exampleInfo = UsingItemAsString + " ---? " + UsedItemAsString;
            }
            _questionableCt = value;
        }

        public void AggregateMarkersAndCounts(Dependency d) {
            _markerSet.UnionWithMarkers(d.Markers);
            _ct += d.Ct;
            _questionableCt += d.QuestionableCt;
            _badCt += d.BadCt;
            _exampleInfo = _exampleInfo ?? d.ExampleInfo;
        }

        private static Item GetOrCreateItem<T>(Dictionary<Item, Item> canonicalItems, Dictionary<Item, List<T>> itemsAndDependencies, Item item) where T : Dependency {
            Item result;
            if (!canonicalItems.TryGetValue(item, out result)) {
                canonicalItems.Add(item, result = item);
            }
            if (!itemsAndDependencies.ContainsKey(result)) {
                itemsAndDependencies.Add(result, new List<T>());
            }
            return result;
        }

        // TODO: Duplicate of Outgoing???
        internal static IDictionary<Item, IEnumerable<Dependency>> Dependencies2ItemsAndDependencies(IEnumerable<Dependency> edges) {
            Dictionary<Item, List<Dependency>> result = Dependencies2ItemsAndDependenciesList(edges);
            return result.ToDictionary<KeyValuePair<Item, List<Dependency>>, Item, IEnumerable<Dependency>>(kvp => kvp.Key, kvp => kvp.Value);
        }

        internal static Dictionary<Item, List<Dependency>> Dependencies2ItemsAndDependenciesList(IEnumerable<Dependency> edges) {
            var canonicalItems = new Dictionary<Item, Item>();
            var result = new Dictionary<Item, List<Dependency>>();
            foreach (var e in edges) {
                Item @using = GetOrCreateItem(canonicalItems, result, e.UsingItem);
                GetOrCreateItem(canonicalItems, result, e.UsedItem);

                result[@using].Add(e);
            }
            return result;
        }

        public bool AddMarker(string marker) {
            return _markerSet.AddMarker(marker);
        }

        public bool UnionWithMarkers(IEnumerable<string> markerPatterns) {
            return _markerSet.UnionWithMarkers(markerPatterns);
        }

        public bool RemoveMarkers(string markerPattern, bool ignoreCase) {
            return _markerSet.RemoveMarkers(markerPattern, ignoreCase);
        }

        public bool RemoveMarkers(IEnumerable<string> markerPatterns, bool ignoreCase) {
            return _markerSet.RemoveMarkers(markerPatterns, ignoreCase);
        }

        public bool ClearMarkers() {
            return _markerSet.ClearMarkers();
        }
    }
}