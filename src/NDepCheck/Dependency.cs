// (c) HMMüller 2006...2017

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    /// <remarks>Class <c>Dependency</c> stores
    /// knowledge about a concrete dependency
    /// (one "using item" uses one "used item").
    /// </remarks>
    public class Dependency : ObjectWithMarkers, IWithCt {
        public const string DIP_ARROW = "=>";

        [NotNull]
        private readonly Item _usingItem;
        [NotNull]
        private readonly Item _usedItem;

        [CanBeNull]
        public InputContext InputContext {
            get;
        }

        private int _ct;
        private int _questionableCt;
        private int _badCt;

        [CanBeNull]
        private string _exampleInfo;

        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem, [CanBeNull] ISourceLocation source,
            [NotNull] string markers, int ct, int questionableCt = 0, int badCt = 0,
            [CanBeNull] string exampleInfo = null, [CanBeNull] InputContext inputContext = null) : this(
                usingItem, usedItem, source, markers: markers.Split('&', '+', ','), ct: ct, questionableCt: questionableCt, badCt: badCt, exampleInfo: exampleInfo, inputContext: inputContext) {
        }

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
        /// <param name="inputContext"></param>
        /// <param name="ignoreCase"></param>
        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [CanBeNull] IEnumerable<string> markers,
            int ct, int questionableCt = 0, int badCt = 0, [CanBeNull] string exampleInfo = null,
            [CanBeNull] InputContext inputContext = null, bool? ignoreCase = null) : base(
                ignoreCase ?? usingItem.Type.IgnoreCase | usedItem.Type.IgnoreCase, markers) {
            if (usingItem == null) {
                throw new ArgumentNullException(nameof(usingItem));
            }
            if (usedItem == null) {
                throw new ArgumentNullException(nameof(usedItem));
            }
            _usingItem = usingItem;
            _usedItem = usedItem;
            InputContext = inputContext;
            inputContext?.AddDependency(this);
            Source = source; // != null ? string.Intern(fileName) : null;
            _ct = ct;
            _questionableCt = questionableCt;
            _badCt = badCt;
            _exampleInfo = exampleInfo;
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

        /// <value>
        /// A guess where the use occurs in the
        /// original source file.
        /// </value>
        [CanBeNull]
        public ISourceLocation Source {
            get;
        }

        [NotNull]
        public Item UsingItem => _usingItem;

        [NotNull]
        public Item UsedItem => _usedItem;

        public int Ct => _ct;

        public int NotOkCt => _questionableCt + _badCt;

        public int OkCt => _ct - _questionableCt - _badCt;

        public int QuestionableCt => _questionableCt;

        public int BadCt => _badCt;

        public string ExampleInfo => _exampleInfo;

        /// <summary>
        /// String representation of a Dependency.
        /// </summary>
        public override string ToString() {
            return UsingItem + " ---> " + UsedItem;
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
                ? QuestionableCt > 0 ? $"{BadCt};{QuestionableCt}" : $"{BadCt}"
                : QuestionableCt > 0 ? $";{QuestionableCt}" : "";
            return $"{prefix}{nounTail} {UsingItem} --{ct}-> {UsedItem}" + (Source != null ? (Ct > 1 ? " (e.g. at " : " (at") + Source + ")" : "");
        }

        public Item UsingNode => _usingItem;

        public Item UsedNode => _usedItem;

        public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
            // TODO: ?? And there should be a flag (in Edge?) "hasNotOkInfo", depending on whether dependency checking was done or not.
            return "\"" + _usingItem.Name + "\" -> \"" + _usedItem.Name + "\" ["
                       + GetDotLabel(stringLengthForIllegalEdges)
                       + GetDotFontSize()
                       + GetDotEdgePenWidthAndWeight()
                       + "];";
        }

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
            string exampleInfo = withExampleInfo ? _exampleInfo : null;
            string markers = string.Join("+", Markers.OrderBy(s => s));
            return $"{_usingItem.AsFullString()} {DIP_ARROW} "
                 + $"{markers};{_ct};{_questionableCt};{_badCt};{Source?.AsDipString()};{exampleInfo} "
                 + $"{DIP_ARROW} {_usedItem.AsFullString()}";
        }

        public void AggregateMarkersAndCounts(Dependency d) {
            UnionWithMarkers(d.Markers);
            _ct += d.Ct;
            _questionableCt += d.QuestionableCt;
            _badCt += d.BadCt;
            _exampleInfo = _exampleInfo ?? d.ExampleInfo;
        }

        private static Item GetOrCreateNode<T>(Dictionary<Item, Item> canonicalNodes, Dictionary<Item, List<T>> nodesAndEdges, Item node) where T : Dependency {
            Item result;
            if (!canonicalNodes.TryGetValue(node, out result)) {
                canonicalNodes.Add(node, result = node);
            }
            if (!nodesAndEdges.ContainsKey(result)) {
                nodesAndEdges.Add(result, new List<T>());
            }
            return result;
        }

        // TODO: Duplicate of Outgoing???
        internal static IDictionary<Item, IEnumerable<Dependency>> Dependencies2ItemsAndDependencies(IEnumerable<Dependency> edges) {
            Dictionary<Item, List<Dependency>> result = Edges2NodesAndEdgesList(edges);
            return result.ToDictionary<KeyValuePair<Item, List<Dependency>>, Item, IEnumerable<Dependency>>(kvp => kvp.Key, kvp => kvp.Value);
        }

        internal static Dictionary<Item, List<Dependency>> Edges2NodesAndEdgesList(IEnumerable<Dependency> edges) {
            var canonicalNodes = new Dictionary<Item, Item>();
            var result = new Dictionary<Item, List<Dependency>>();
            foreach (var e in edges) {
                Item @using = GetOrCreateNode(canonicalNodes, result, e.UsingNode);
                GetOrCreateNode(canonicalNodes, result, e.UsedNode);

                result[@using].Add(e);
            }
            return result;
        }

        protected override void MarkersHaveChanged() {
            // empty
        }
    }
}