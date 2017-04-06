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
    public class Dependency : ObjectWithMarkers, IEdge {
        [NotNull] private readonly Item _usingItem;
        [NotNull] private readonly Item _usedItem;

        [CanBeNull]
        public InputContext InputContext { get; }

        private int _ct;
        private int _questionableCt;
        private int _badCt;

        [CanBeNull] private string _exampleInfo;

        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem, [CanBeNull] ISourceLocation source,
            [CanBeNull] string usage, int ct, int questionableCt = 0, int badCt = 0,
            [CanBeNull] string exampleInfo = null, [CanBeNull] InputContext inputContext = null) : this(
                usingItem, usedItem, source, UsageToSet(usage), ct, questionableCt, badCt, exampleInfo, inputContext) { }



        /// <summary>
        /// Create a dependency.
        /// </summary>
        /// <param name="usingItem">The using item.</param>
        /// <param name="usedItem">The used item.</param>
        /// <param name="source">Name of the file.</param>
        /// <param name="usage"></param>
        /// <param name="ct"></param>
        /// <param name="questionableCt"></param>
        /// <param name="badCt"></param>
        /// <param name="exampleInfo"></param>
        /// <param name="inputContext"></param>
        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [NotNull] IEnumerable<string> usage,
            int ct, int questionableCt = 0, int badCt = 0, [CanBeNull] string exampleInfo = null,
            [CanBeNull] InputContext inputContext = null) {
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
            Usage = new HashSet<string>(usage);
            _ct = ct;
            _questionableCt = questionableCt;
            _badCt = badCt;
            _exampleInfo = exampleInfo;
        }

        public void SetUsage(string usage) {
            Usage = UsageToSet(usage);
        }

        public void AddUsage(string usage) {
            Usage.UnionWith(UsageToSet(usage));
        }

        private static HashSet<string> UsageToSet(string usage) {
            return new HashSet<string>(usage?.Split('+').Where(s => !string.IsNullOrWhiteSpace(s)) ?? Enumerable.Empty<string>());
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
        public HashSet<string> Usage {
            get; private set;
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
        /// A message presented to the user of this Dependency is questionable.
        /// </summary>
        /// <returns></returns>
        public string QuestionableDependencyMessage() {
            return "Questionable dependency " + UsingItem + " ---> " + UsedItem;
        }
        /// <summary>
        /// A message presented to the user of this Dependency is not allowed.
        /// </summary>
        /// <returns></returns>
        public string BadDependencyMessage() {
            return "Bad dependency " + UsingItem + " ---> " + UsedItem;
        }

        public INode UsingNode => _usingItem;
        public INode UsedNode => _usedItem;

        public bool Hidden {
            get; set;
        }

        public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
            // TODO: ?? And there should be a flag (in Edge?) "hasNotOkInfo", depending on whether dependency checking was done or not.
            return "\"" + _usingItem.Name + "\" -> \"" + _usedItem.Name + "\" ["
                       + GetDotLabel(stringLengthForIllegalEdges)
                       + GetDotFontSize()
                       + GetDotEdgePenWidthAndWeight()
                       + "];";
        }

        public void MarkAsBad() {
            // First bad example overrides any previous example
            if (_badCt == 0 || _exampleInfo == null) {
                _exampleInfo = UsingItemAsString + " ---! " + UsedItemAsString;
            }
            _badCt = _ct;
        }

        public void MarkAsQuestionable() {
            if (_badCt == 0 && _questionableCt == 0 || _exampleInfo == null) {
                _exampleInfo = UsingItemAsString + " ---? " + UsedItemAsString;
            }
            _questionableCt = _ct;
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
            string exampleInfo = withExampleInfo ? _exampleInfo : null;
            string usage = string.Join("+", Usage.OrderBy(s => s));
            return $"{_usingItem.AsStringWithOrderAndType()} {EdgeConstants.DIP_ARROW} "
                 + $"{usage};{_ct};{_questionableCt};{_badCt};{Source?.AsDipString()};{exampleInfo} "
                 + $"{EdgeConstants.DIP_ARROW} {_usedItem.AsStringWithOrderAndType()}";
        }

        public void AggregateCounts(Dependency d) {
            Usage.UnionWith(d.Usage);
            _ct += d.Ct;
            _questionableCt += d.QuestionableCt;
            _badCt += d.BadCt;
            _exampleInfo = _exampleInfo ?? d.ExampleInfo;
        }

        private static INode GetOrCreateNode<T>(Dictionary<INode, INode> canonicalNodes, Dictionary<INode, List<T>> nodesAndEdges, INode node) where T : IEdge {
            INode result;
            if (!canonicalNodes.TryGetValue(node, out result)) {
                canonicalNodes.Add(node, result = node);
            }
            if (!nodesAndEdges.ContainsKey(result)) {
                nodesAndEdges.Add(result, new List<T>());
            }
            return result;
        }

        internal static IDictionary<INode, IEnumerable<T>> Edges2NodesAndEdges<T>(IEnumerable<T> edges) where T : class, IEdge {
            Dictionary<INode, List<T>> result = Edges2NodesAndEdgesList(edges);
            return result.ToDictionary<KeyValuePair<INode, List<T>>, INode, IEnumerable<T>>(kvp => kvp.Key, kvp => kvp.Value);
        }

        internal static Dictionary<INode, List<T>> Edges2NodesAndEdgesList<T>(IEnumerable<T> edges) where T : IEdge {
            var canonicalNodes = new Dictionary<INode, INode>();
            var result = new Dictionary<INode, List<T>>();
            foreach (var e in edges) {
                INode @using = GetOrCreateNode(canonicalNodes, result, e.UsingNode);
                GetOrCreateNode(canonicalNodes, result, e.UsedNode);

                result[@using].Add(e);
            }
            return result;
        }

        public void ResetQuestionableCt() {
            _questionableCt = 0;
        }

        public void ResetBadCt() {
            _badCt = 0;
        }
    }
}