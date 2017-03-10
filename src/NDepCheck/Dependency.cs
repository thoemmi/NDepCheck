// (c) HMMüller 2006...2017

using System;
using JetBrains.Annotations;

namespace NDepCheck {
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
        private int _notOkCt;
        [CanBeNull]
        private Dependency _notOkExample;

        private bool _onCycle;
        private bool _carrysTransitive;

        /// <summary>
        /// Create a dependency.
        /// </summary>
        /// <param name="usingItem">The using item.</param>
        /// <param name="usedItem">The used item.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="startLine">The start line.</param>
        /// <param name="startColumn">The start column.</param>
        /// <param name="endLine">The end line.</param>
        /// <param name="endColumn">The end column.</param>
        /// <param name="ct"></param>
        /// <param name="notOkCt"></param>
        /// <param name="notOkExample"></param>
        public Dependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            string fileName, int startLine, int startColumn, int endLine, int endColumn,
            int ct = 1, int notOkCt = 0, [CanBeNull] Dependency notOkExample = null) {
            if (usingItem == null) {
                throw new ArgumentNullException(nameof(usingItem));
            }
            if (usedItem == null) {
                throw new ArgumentNullException(nameof(usedItem));
            }
            _usingItem = usingItem;
            _usedItem = usedItem;
            FileName = fileName; // != null ? string.Intern(fileName) : null;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;

            _ct = ct;
            _notOkCt = notOkCt;
            _notOkExample = notOkExample;
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
        public string FileName { get; }

        /// <summary>
        /// Gets a guess of the line number in the original
        /// source file.
        /// </summary>
        /// <value>The line number.</value>
        public int StartLine { get; }

        public int EndLine { get; }

        public int StartColumn { get; }

        public int EndColumn { get; }

        [NotNull]
        public Item UsingItem => _usingItem;

        [NotNull]
        public Item UsedItem => _usedItem;

        public int Ct => _ct;

        public int NotOkCt => _notOkCt;

        public Dependency NotOkExample => _notOkExample;

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

        public bool Hidden { get; set; }

        public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
            // TODO: ?? And there should be a flag (in Edge?) "hasNotOkInfo", depending on whether dependency checking was done or not.
            return "\"" + _usingItem.Name + "\" -> \"" + _usedItem.Name + "\" ["
                       + GetLabel(stringLengthForIllegalEdges)
                       + GetFontSize()
                       + GetStyle() + "];";
        }

        private string GetFontSize() {
            return " fontsize=" + (10 + 5 * Math.Round(Math.Log10(Ct)));
        }

        private string GetLabel(int? stringLengthForIllegalEdges) {
            return "label=\"" + (stringLengthForIllegalEdges.HasValue && NotOkExample != null
                                ? LimitWidth(NotOkExample.UsingItem.AsString()/*???*/, stringLengthForIllegalEdges.Value) + " --->\\n" +
                                  LimitWidth(NotOkExample.UsedItem.AsString()/*???*/, stringLengthForIllegalEdges.Value) + "\\n"
                                : "") +
                            " (" + CountsAsString() + ")" +
                            (_carrysTransitive ? "+" : "") +
                            "\"";
        }

        private static string LimitWidth(string s, int lg) {
            if (s.Length > lg) {
                s = "..." + s.Substring(s.Length - lg + 3);
            }
            return s;
        }

        private string CountsAsString() {
            return (NotOkCt > 0 ? NotOkCt + " bad of " : "") + Ct;
        }

        private string GetStyle() {
            return _onCycle ? " style=bold" : "";
        }

        public void MarkOnCycle() {
            _onCycle = true;
        }

        public void MarkCarrysTransitive() {
            _carrysTransitive = true;
        }

        public string AsStringWithTypes() {
            string notOk = _notOkExample == null ? null : $"({_notOkExample})";
            return $"{_usingItem.AsStringWithType()} -> {_ct};{_notOkCt};{notOk} -> {_usedItem.AsStringWithType()}";
        }

        public IEdge CreateEdgeFromUsingTo(INode usedNode) {
            // Brutal cast INode --> Item ... I'm not sure about this; but except for TestNode there is no other INode around ____
            return new Dependency(_usingItem, (Item) usedNode, "associative edge", 0, 0, 0, 0);
        }

        public void MarkOkOrNotOk(bool ok) {
            if (!ok) {
                _notOkCt = _ct;
                _notOkExample = _notOkExample ?? this;
            }
        }

        public void AggregateCounts(Dependency d) {
            _ct += d.Ct;
            _notOkCt += d.NotOkCt;
            _notOkExample = _notOkExample ?? d.NotOkExample;
        }
    }
}