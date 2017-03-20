// (c) HMMüller 2006...2017

using System;
using System.Globalization;
using JetBrains.Annotations;

namespace NDepCheck {
    public enum DependencyCheckResult {
        Ok, Questionable, Bad
    }

    /// <remarks>Class <c>Dependency</c> stores
    /// knowledge about a concrete dependency
    /// (one "using item" uses one "used item").
    /// </remarks>
    public class Dependency : IEdge {
        [NotNull]
        private readonly Item _usingItem;
        [NotNull]
        private readonly Item _usedItem;

        private int _ct;
        private int _questionableCt;
        private int _badCt;
        [CanBeNull]
        private string _notOkExampleInfo;

        private bool _onCycle;
        private bool _carrysTransitive;

        /// <summary>
        /// Create a dependency.
        /// </summary>
        /// <param name="usingItem">The using item.</param>
        /// <param name="usedItem">The used item.</param>
        /// <param name="source">Name of the file.</param>
        /// <param name="startLine">The start line.</param>
        /// <param name="startColumn">The start column.</param>
        /// <param name="endLine">The end line.</param>
        /// <param name="endColumn">The end column.</param>
        /// <param name="usage"></param>
        /// <param name="ct"></param>
        /// <param name="questionableCt"></param>
        /// <param name="badCt"></param>
        /// <param name="notOkExampleInfo"></param>
        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            string source, int startLine, int startColumn, int endLine, int endColumn, string usage,
            int ct, int questionableCt = 0, int badCt = 0, [CanBeNull] string notOkExampleInfo = null) {
            if (usingItem == null) {
                throw new ArgumentNullException(nameof(usingItem));
            }
            if (usedItem == null) {
                throw new ArgumentNullException(nameof(usedItem));
            }
            _usingItem = usingItem;
            _usedItem = usedItem;
            Source = source; // != null ? string.Intern(fileName) : null;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
            Usage = usage;
            _ct = ct;
            _questionableCt = questionableCt;
            _badCt = badCt;
            _notOkExampleInfo = notOkExampleInfo;
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
        public string Source {
            get;
        }

        /// <summary>
        /// Gets a guess of the line number in the original
        /// source file.
        /// </summary>
        /// <value>The line number.</value>
        public int StartLine {
            get;
        }

        public int EndLine {
            get;
        }

        public int StartColumn {
            get;
        }

        public int EndColumn {
            get;
        }

        public string Usage {
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

        public string NotOkExampleInfo => _notOkExampleInfo;

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
        public string QuestionableMessage() {
            return "Questionable dependency " + UsingItem + " ---> " + UsedItem;
        }
        /// <summary>
        /// A message presented to the user of this Dependency is not allowed.
        /// </summary>
        /// <returns></returns>
        public string IllegalMessage() {
            return "Illegal dependency " + UsingItem + " ---> " + UsedItem;
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
                       + GetDotStyle() + "];";
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
            return "label=\"" + (stringLengthForIllegalEdges.HasValue && NotOkExampleInfo != null
                                ? LimitWidth(NotOkExampleInfo, stringLengthForIllegalEdges.Value) + "\\n"
                                : "") +
                            " (" + Ct + (QuestionableCt + BadCt > 0 ? "(" + QuestionableCt + "?," + BadCt + "!)" : "") + ")" +
                            (_carrysTransitive ? "+" : "") +
                            "\"";
        }

        private static string LimitWidth(string s, int lg) {
            if (s.Length > lg) {
                s = "..." + s.Substring(s.Length - lg + 3);
            }
            return s;
        }

        private string GetDotStyle() {
            return _onCycle ? " style=bold" : "";
        }

        public void MarkOnCycle() {
            _onCycle = true;
        }

        public void MarkCarrysTransitive() {
            _carrysTransitive = true;
        }

        public string AsDipStringWithTypes(bool withNotOkExampleInfo) {
            string notOkInfo = withNotOkExampleInfo && _notOkExampleInfo != null ? $"{_notOkExampleInfo}" : null;
            return $"{_usingItem.AsStringWithType()}"
                 + $" {EdgeConstants.DIP_ARROW} {Usage};{_ct};{_questionableCt};{_badCt};{notOkInfo} {EdgeConstants.DIP_ARROW} "
                 + $"{_usedItem.AsStringWithType()}";
        }

        ////public IEdge CreateEdgeFromUsingTo(INode usedNode) {
        ////    // Brutal cast INode --> Item ... I'm not sure about this; but except for TestNode there is no other INode around ____
        ////    return new Dependency(_usingItem, (Item) usedNode, "associative edge", 0, 0, 0, 0, 0, 1);
        ////}

        public void AddCheckResult(DependencyCheckResult result) {
            switch (result) {
                case DependencyCheckResult.Ok:
                    _ct++;
                    break;
                case DependencyCheckResult.Questionable:
                    _notOkExampleInfo = _notOkExampleInfo ?? UsingItemAsString + " ---? " + UsedItemAsString;
                    _questionableCt++;
                    break;
                case DependencyCheckResult.Bad:
                    if (_badCt == 0) {
                        // First bad example overrides any previous bad example
                        _notOkExampleInfo = UsingItemAsString + " ---! " + UsedItemAsString;
                    } else {
                        _notOkExampleInfo = _notOkExampleInfo ?? UsingItemAsString + " ---! " + UsedItemAsString;
                    }
                    _badCt++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        public void AggregateCounts(Dependency d) {
            if (string.IsNullOrWhiteSpace(Usage)) {
                Usage = d.Usage;
            } else if (!string.IsNullOrWhiteSpace(d.Usage) && !Usage.Contains(d.Usage)) {
                Usage += "+" + d.Usage;
            }
            _ct += d.Ct;
            _questionableCt += d.QuestionableCt;
            _badCt += d.BadCt;
            _notOkExampleInfo = _notOkExampleInfo ?? d.NotOkExampleInfo;
        }
    }
}