using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.PathMatching {
    public class Graphken {
        public readonly Node StartNode;
        public readonly Node EndNode;

        public Graphken(Node startNode, Node endNode) {
            StartNode = startNode;
            EndNode = endNode;
        }

        public Graphken Wrap() {
            Node newStart = StartNode.CreateNodeOfSameKind();
            newStart.AddEpsilonTo(StartNode);
            Node newEnd = EndNode.CreateNodeOfSameKind();
            EndNode.AddEpsilonTo(newEnd);
            return new Graphken(newStart, newEnd);
        }
    }

    public struct MatchAndTarget<TMatch, TTargetNode> where TTargetNode : Node {
        public bool Invert { get; }
        public bool IsCount { get; }

        [CanBeNull]
        public readonly IEnumerable<TMatch> MatchesOrNullForMatchAny;
        [CanBeNull]
        public readonly TTargetNode TargetNode;

        public MatchAndTarget(bool invert, bool isCount, [CanBeNull] IEnumerable<TMatch> matchesOrNullForMatchAny,
            TTargetNode targetNode) {
            Invert = invert;
            IsCount = isCount;
            MatchesOrNullForMatchAny = matchesOrNullForMatchAny;
            TargetNode = targetNode;
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{GetType().Name}:{TargetNode}{(IsCount ? "C" : "")}{(Invert ? "I" : "")}";
        }
    }

    public abstract class Node {
        private static int _nodeCt;
        public readonly int NodeId = ++_nodeCt;

        public abstract void AddEpsilonTo(Node t);
        public abstract Node CreateNodeOfSameKind();
        public bool IsEnd { get; set; }

        [ExcludeFromCodeCoverage]
        public string GetRepresentation() {
            var sb = new StringBuilder();
            GetRepresentation(sb, "", new HashSet<Node>());
            return sb.ToString();
        }

        protected internal abstract void GetRepresentation(StringBuilder result, string indent,
            HashSet<Node> alreadyVisisted);

        public abstract IEnumerable<Node> CollectAllReachableViaEpsilons();
    }

    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public abstract class NodeBefore<TMatch, TThis, TTargetNode> : Node
            where TThis : NodeBefore<TMatch, TThis, TTargetNode> where TTargetNode : Node {
        private readonly List<TThis> _epsilonTargets = new List<TThis>();

        private readonly List<MatchAndTarget<TMatch, TTargetNode>> _transitions =
            new List<MatchAndTarget<TMatch, TTargetNode>>();

        protected NodeBefore(string representation) {
            Representation = representation;
        }

        public string Representation { get; }

        [ExcludeFromCodeCoverage]
        protected internal override void GetRepresentation(StringBuilder result, string indent,
            HashSet<Node> alreadyVisisted) {
            if (alreadyVisisted.Add(this)) {
                result.AppendLine(indent + this);
                foreach (var e in _epsilonTargets) {
                    e.GetRepresentation(result, indent + " -", alreadyVisisted);
                }
                foreach (var t in _transitions.Select(t => t.TargetNode)) {
                    if (t == null) {
                        result.AppendLine(indent + " =null");
                    } else {
                        t.GetRepresentation(result, indent + " =", alreadyVisisted);
                    }
                }
            } else {
                result.AppendLine(indent + "^" + this);
            }
        }

        public IEnumerable<TThis> EpsilonTargets => _epsilonTargets;
        public IEnumerable<MatchAndTarget<TMatch, TTargetNode>> Transitions => _transitions;

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return
                $"{NodeId} {GetType().Name}({_transitions.Count}/{_epsilonTargets.Count}/{(IsEnd ? "E" : "")}) {Representation}";
        }

        public override void AddEpsilonTo(Node t) {
            TThis sameType = t as TThis;
            if (sameType == null) {
                throw new Exception("Inner exception - trying to add epsilon edge between different node types");
            }
            _epsilonTargets.Add(sameType);
        }

        public override IEnumerable<Node> CollectAllReachableViaEpsilons() => CollectReachableViaEpsilons();

        public IEnumerable<NodeBefore<TMatch, TThis, TTargetNode>> CollectReachableViaEpsilons() {
            yield return this;
            foreach (var e in EpsilonTargets.SelectMany(e => e.CollectReachableViaEpsilons())) {
                yield return e;
            }
        }

        public void Add(bool invert, bool isCount, IEnumerable<TMatch> matches, TTargetNode target) {
            _transitions.Add(new MatchAndTarget<TMatch, TTargetNode>(invert, isCount, matches, target));
        }
    }

    public abstract class PathRegex<TItem, TDependency, TItemMatch, TDependencyMatch> {
        public class NodeBeforeItemMatch : NodeBefore<TItemMatch, NodeBeforeItemMatch, NodeBeforeDependencyMatch> {
            public NodeBeforeItemMatch(string representation) : base(representation) {
            }

            public override Node CreateNodeOfSameKind() {
                return new NodeBeforeItemMatch("=" + NodeId + " " + Representation);
            }
        }

        public class NodeBeforeDependencyMatch :
            NodeBefore<TDependencyMatch, NodeBeforeDependencyMatch, NodeBeforeItemMatch> {
            public NodeBeforeDependencyMatch(string representation) : base(representation) {
            }

            public override Node CreateNodeOfSameKind() {
                return new NodeBeforeDependencyMatch("=" + NodeId + " " + Representation);
            }
        }

        public abstract class PathRegexElement {
            protected PathRegexElement(int textPos) {
                TextPos = textPos;
            }

            public int TextPos { get; }

            public abstract bool StartsWithItem { get; }
            public abstract bool EndsWithItem { get; }
            public abstract bool CanBeEmpty { get; }

            public abstract Graphken CreateGraphken();
        }

        public class ItemMatchAnyElement : PathRegexElement {
            private readonly bool _invert;
            private readonly bool _isCount;

            [CanBeNull, ItemNotNull]
            public IEnumerable<TItemMatch> ItemMatchesOrNullForMatchAny { get; }

            public ItemMatchAnyElement(bool invert, bool isCount, int textPos,
                [CanBeNull, ItemNotNull] IEnumerable<TItemMatch> itemMatchesOrNullForMatchAny) : base(textPos) {
                _invert = invert;
                _isCount = isCount;
                ItemMatchesOrNullForMatchAny = itemMatchesOrNullForMatchAny;
            }

            public override bool StartsWithItem => true;
            public override bool EndsWithItem => true;
            public override bool CanBeEmpty => false;

            public override Graphken CreateGraphken() {
                string representation = "{" +
                                        (ItemMatchesOrNullForMatchAny == null
                                            ? ":"
                                            : string.Join(",", ItemMatchesOrNullForMatchAny)) + "}";
                NodeBeforeItemMatch start = new NodeBeforeItemMatch("<" + representation);
                NodeBeforeDependencyMatch end = new NodeBeforeDependencyMatch(">" + representation);
                start.Add(_invert, _isCount, ItemMatchesOrNullForMatchAny, end);
                return new Graphken(start, end);
            }
        }

        public class DependencyMatchAnyElement : PathRegexElement {
            private readonly bool _invert;
            private readonly bool _isCount;

            [CanBeNull, ItemNotNull]
            public IEnumerable<TDependencyMatch> DependencyMatchesOrNullForMatchAny { get; }

            public DependencyMatchAnyElement(bool invert, bool isCount, int textPos,
                [CanBeNull, ItemNotNull] IEnumerable<TDependencyMatch> dependencyMatchesOrNullForMatchAny)
                : base(textPos) {
                _invert = invert;
                _isCount = isCount;
                DependencyMatchesOrNullForMatchAny = dependencyMatchesOrNullForMatchAny;
            }

            public override bool StartsWithItem => false;
            public override bool EndsWithItem => false;
            public override bool CanBeEmpty => false;

            public override Graphken CreateGraphken() {
                string representation = "{" +
                                        (DependencyMatchesOrNullForMatchAny == null
                                            ? "."
                                            : string.Join(",", DependencyMatchesOrNullForMatchAny)) + "}";
                NodeBeforeDependencyMatch start = new NodeBeforeDependencyMatch("<" + representation);
                NodeBeforeItemMatch end = new NodeBeforeItemMatch(representation + ">");
                start.Add(_invert, _isCount, DependencyMatchesOrNullForMatchAny, end);
                return new Graphken(start, end);
            }
        }

        public class Optional : PathRegexElement {
            public PathRegexElement Inner { get; }

            public Optional(int textPos, PathRegexElement inner) : base(textPos) {
                Inner = inner;
            }

            public override bool StartsWithItem => Inner.StartsWithItem;
            public override bool EndsWithItem => Inner.EndsWithItem;
            public override bool CanBeEmpty => true;

            public override Graphken CreateGraphken() {
                Graphken inner = Inner.CreateGraphken();
                Graphken wrap = inner.Wrap();
                wrap.StartNode.AddEpsilonTo(wrap.EndNode);
                return wrap;
            }
        }

        public class ZeroOrMore : PathRegexElement {
            public PathRegexElement Inner { get; }

            public ZeroOrMore(int textPos, PathRegexElement inner) : base(textPos) {
                Inner = inner;
            }

            public override bool StartsWithItem => Inner.StartsWithItem;
            public override bool EndsWithItem => Inner.EndsWithItem;
            public override bool CanBeEmpty => true;

            public override Graphken CreateGraphken() {
                Graphken inner = Inner.CreateGraphken();
                inner.EndNode.AddEpsilonTo(inner.StartNode);
                Graphken loop = new Graphken(inner.StartNode, inner.StartNode);
                Graphken wrap = loop.Wrap();
                return wrap;
            }
        }

        public class OneOrMore : PathRegexElement {
            public PathRegexElement Inner { get; }

            public OneOrMore(int textPos, PathRegexElement inner) : base(textPos) {
                Inner = inner;
            }

            public override bool StartsWithItem => Inner.StartsWithItem;
            public override bool EndsWithItem => Inner.EndsWithItem;
            public override bool CanBeEmpty => false;

            public override Graphken CreateGraphken() {
                Graphken inner = Inner.CreateGraphken();
                Graphken wrap = inner.Wrap();
                wrap.EndNode.AddEpsilonTo(wrap.StartNode);
                return wrap;
            }
        }

        public class Sequence : PathRegexElement {
            public override bool StartsWithItem { get; }
            public override bool EndsWithItem { get; }

            public Sequence(bool startsWithItem, bool endsWithItem, int textPos,
                IEnumerable<PathRegexElement> elements) : base(textPos) {
                StartsWithItem = startsWithItem;
                EndsWithItem = endsWithItem;
                Elements = elements.ToArray();
            }

            public PathRegexElement[] Elements { get; }

            public override bool CanBeEmpty => Elements.All(e => e.CanBeEmpty);

            public override Graphken CreateGraphken() {
                if (Elements.Any()) {
                    Graphken init = Elements.First().CreateGraphken();
                    Graphken previous = init;
                    foreach (var e in Elements.Skip(1)) {
                        Graphken g = e.CreateGraphken();
                        previous.EndNode.AddEpsilonTo(g.StartNode);
                        previous = g;
                    }
                    return new Graphken(init.StartNode, previous.EndNode);
                } else {
                    Node startNode = StartsWithItem
                        ? (Node)new NodeBeforeItemMatch("<>")
                        : new NodeBeforeDependencyMatch("<>");
                    Node endNode = startNode.CreateNodeOfSameKind();
                    startNode.AddEpsilonTo(endNode);
                    return new Graphken(startNode, endNode);
                }
            }
        }

        public class Alternatives : PathRegexElement {
            public Alternatives(bool startsWithItem, bool endsWithItem, int textPos,
                IEnumerable<PathRegexElement> elements) : base(textPos) {
                StartsWithItem = startsWithItem;
                EndsWithItem = endsWithItem;
                Elements = elements.ToArray();
            }

            public PathRegexElement[] Elements { get; }
            public override bool StartsWithItem { get; }
            public override bool EndsWithItem { get; }
            public override bool CanBeEmpty => Elements.Any(e => e.CanBeEmpty);

            public override Graphken CreateGraphken() {
                Graphken result = Elements.First().CreateGraphken().Wrap();
                foreach (var e in Elements.Skip(1)) {
                    Graphken w = e.CreateGraphken();
                    result.StartNode.AddEpsilonTo(w.StartNode);
                    w.EndNode.AddEpsilonTo(result.EndNode);
                }
                return result;
            }
        }

        private const string NAME_REGEX = "{[^}]*}|[a-zA-Z0-9]";
        private const string SYMBOL_REGEX = @"\G[\s_]*([*+?()|.:\[\]^#]|" + NAME_REGEX + ")";

        private readonly Dictionary<string, TItemMatch> _definedItemMatches;
        private readonly Dictionary<string, TDependencyMatch> _definedDependencyMatches;
        private readonly bool _ignoreCase;
        private readonly string _definition;
        private readonly Graphken _graph;
        private bool _isCountEncountered;
        private bool _containsLoops;

        protected PathRegex([NotNull] string definition, [NotNull] Dictionary<string, TItemMatch> definedItemMatches,
            [NotNull] Dictionary<string, TDependencyMatch> definedDependencyMatches, bool ignoreCase) {
            _definedItemMatches = definedItemMatches;
            _definedDependencyMatches = definedDependencyMatches;
            _ignoreCase = ignoreCase;

            int pos = 0;
            _definition = definition;
            PathRegexElement regex = ParseItemAlternatives(ref pos, startsWithItem: true);
            _graph = regex.CreateGraphken();
            _graph.EndNode.IsEnd = true;
        }

        public bool ContainsCountSymbol => _isCountEncountered;

        public bool ContainsLoops => _containsLoops;

        [ExcludeFromCodeCoverage]
        public string GetGraphkenRepresentation() {
            return _graph.StartNode.GetRepresentation();
        }

        public string RawPeekSymbol(int pos) {
            Regex _symbols = new Regex(SYMBOL_REGEX);
            if (pos >= _definition.Length) {
                return "";
            } else {
                Match m = _symbols.Match(_definition, pos);
                if (m.Success) {
                    return m.Value;
                } else {
                    throw new RegexSyntaxException(_definition, pos, "No valid symbol");
                }
            }
        }

        public string PeekSymbol(int pos, bool eofOk = false) {
            string sym = RawPeekSymbol(pos).Trim().Trim('_');
            if (!eofOk && sym == "") {
                throw new RegexSyntaxException(_definition, pos, "Unexpected end of input");
            }
            return sym;
        }

        private void AdvanceSymbolPos(ref int pos) {
            pos += RawPeekSymbol(pos).Length;
        }

        private bool Matches(string symbol, params string[] symbols) {
            var b = symbols.Contains(NAME_REGEX) && Regex.IsMatch(symbol, "^" + NAME_REGEX + "$");
            var c = symbols.Contains(symbol);
            return c || b;
        }

        private PathRegexElement ParseItemAlternatives(ref int pos, bool startsWithItem) {
            // Syntax: sequence { '|' sequence }
            int startPos = pos;
            PathRegexElement first = ParseItemSequence(ref pos, startsWithItem);
            var elements = new List<PathRegexElement> { first };

            while (Matches(PeekSymbol(pos, eofOk: true), "|")) {
                AdvanceSymbolPos(ref pos);
                elements.Add(ParseItemSequence(ref pos, startsWithItem));
            }

            bool endsWithItem = first.EndsWithItem;
            foreach (var e in elements) {
                if (e.EndsWithItem != endsWithItem) {
                    throw new RegexSyntaxException(_definition, e.TextPos,
                        "Element in Regex does not end with " + ItemOrDependency(endsWithItem) + " like others");
                }
            }

            return elements.Count > 1
                ? new Alternatives(startsWithItem: startsWithItem, endsWithItem: endsWithItem, textPos: startPos,
                    elements: elements)
                : first;
        }

        [ExcludeFromCodeCoverage]
        private string ItemOrDependency(bool endsWithItem) {
            return endsWithItem ? "item match" : "dependency match";
        }

        private PathRegexElement ParseItemSequence(ref int pos, bool startsWithItem) {
            // Syntax: { element }
            int startPos = pos;
            var list = new List<PathRegexElement>();
            for (;;) {
                string peekSym = PeekSymbol(pos, eofOk: true);
                if (Matches(peekSym, "", ")", "|")) {
                    break;
                } else {
                    PathRegexElement element = ParseItemElement(ref pos, startsWithItem);
                    list.Add(element);
                    startsWithItem = !element.EndsWithItem;
                }
            }
            switch (list.Count) {
                case 0:
                    return new Sequence(startsWithItem, !startsWithItem, startPos, list);
                case 1:
                    return list.Single();
                default:
                    return new Sequence(startsWithItem, list.Last().EndsWithItem, startPos, list);
            }
        }

        private PathRegexElement ParseItemElement(ref int pos, bool startsWithItem) {
            // Syntax: 
            //     /!startsWithItem/ . 
            //   | /startsWithItem/  : 
            //   | '(' alternative ')' ( ? | * | + )?
            //   | '[' ( '^' )? NAME { NAME } ']'
            //   | NAME
            int startPos = pos;
            string peekSym = PeekSymbol(startPos);
            if (Matches(peekSym, "(")) {
                AdvanceSymbolPos(ref pos);

                PathRegexElement inner = ParseItemAlternatives(ref pos, startsWithItem);
                peekSym = PeekSymbol(pos);
                if (!Matches(peekSym, ")")) {
                    throw new RegexSyntaxException(_definition, pos, ") expected");
                }
                AdvanceSymbolPos(ref pos);

                peekSym = PeekSymbol(pos, eofOk: true);
                if (Matches(peekSym, "?")) {
                    AdvanceSymbolPos(ref pos);
                    return new Optional(startPos, inner);
                } else if (Matches(peekSym, "*")) {
                    AdvanceSymbolPos(ref pos);
                    _containsLoops = true;
                    return new ZeroOrMore(startPos, inner);
                } else if (Matches(peekSym, "+")) {
                    AdvanceSymbolPos(ref pos);
                    _containsLoops = true;
                    return new OneOrMore(startPos, inner);
                } else {
                    return inner;
                }
            } else if (Matches(peekSym, "[")) {
                AdvanceSymbolPos(ref pos);

                bool invert = false;
                if (Matches(PeekSymbol(pos), "^")) {
                    invert = true;
                    AdvanceSymbolPos(ref pos);
                }

                var matches = new SortedDictionary<int, string> { { pos, PeekName(pos) } };
                AdvanceSymbolPos(ref pos);
                while (!Matches(PeekSymbol(pos), "]")) {
                    matches.Add(pos, PeekName(pos));
                    AdvanceSymbolPos(ref pos);
                }
                AdvanceSymbolPos(ref pos);
                bool isCount = ParseOptionalCount(ref pos);
                if (startsWithItem) {
                    return new ItemMatchAnyElement(invert, isCount, startPos,
                        matches.Select(kvp => CreateItemMatch(kvp.Key, kvp.Value)));
                } else {
                    return new DependencyMatchAnyElement(invert, isCount, startPos,
                        matches.Select(kvp => CreateDependencyMatch(kvp.Key, kvp.Value)));
                }
            } else if (Matches(peekSym, NAME_REGEX)) {
                AdvanceSymbolPos(ref pos);

                bool isCount = ParseOptionalCount(ref pos);
                if (startsWithItem) {
                    return new ItemMatchAnyElement(false, isCount, startPos,
                        new[] { CreateItemMatch(startPos, peekSym) });
                } else {
                    return new DependencyMatchAnyElement(false, isCount, startPos,
                        new[] { CreateDependencyMatch(startPos, peekSym) });
                }
            } else if (Matches(peekSym, ".")) {
                AdvanceSymbolPos(ref pos);
                if (startsWithItem) {
                    throw new RegexSyntaxException(_definition, startPos, ". cannot be used at item position");
                }
                bool isCount = ParseOptionalCount(ref pos);
                return new DependencyMatchAnyElement(false, isCount, startPos, null);
            } else if (Matches(peekSym, ":")) {
                AdvanceSymbolPos(ref pos);
                if (!startsWithItem) {
                    throw new RegexSyntaxException(_definition, startPos, ": cannot be used at dependency position");
                }
                bool isCount = ParseOptionalCount(ref pos);
                return new ItemMatchAnyElement(false, isCount, startPos, null);
            } else {
                throw new RegexSyntaxException(_definition, startPos,
                    "Unexpected element - [, ( or " + (startsWithItem ? ":" : ".") + " expected");
            }
        }

        private bool ParseOptionalCount(ref int pos) {
            bool isCount = false;
            if (Matches(PeekSymbol(pos, eofOk: true), "#")) {
                if (_isCountEncountered) {
                    throw new RegexSyntaxException(_definition, pos, "# can only be used once");
                }
                AdvanceSymbolPos(ref pos);
                isCount = true;
                _isCountEncountered = true;
            }
            return isCount;
        }

        [CanBeNull]
        private TItemMatch CreateItemMatch(int pos, [NotNull] string s) {
            string trimmed = s.TrimStart('{').TrimEnd('}');
            try {
                TItemMatch definedMatch;
                return _definedItemMatches.TryGetValue(trimmed, out definedMatch)
                    ? definedMatch
                    : CreateItemMatch(trimmed, _ignoreCase);
            } catch (Exception ex) {
                throw new RegexSyntaxException(_definition, pos,
                    $"Cannot create ItemMatch from '{trimmed}' - reason: {ex.Message}");
            }
        }

        protected abstract TItemMatch CreateItemMatch([NotNull] string pattern, bool ignoreCase);

        [CanBeNull]
        private TDependencyMatch CreateDependencyMatch(int pos, [NotNull] string s) {
            string trimmed = s.TrimStart('{').TrimEnd('}');
            try {
                TDependencyMatch definedMatch;
                return _definedDependencyMatches.TryGetValue(trimmed, out definedMatch)
                    ? definedMatch
                    : CreateDependencyMatch(trimmed, _ignoreCase);
            } catch (Exception ex) {
                throw new RegexSyntaxException(_definition, pos,
                    $"Cannot create dependency match from '{trimmed}' - reason: {ex.Message}");
            }
        }

        protected abstract TDependencyMatch CreateDependencyMatch([NotNull] string pattern, bool ignoreCase);

        private string PeekName(int pos) {
            string name = PeekSymbol(pos);
            if (!Matches(name, NAME_REGEX)) {
                throw new RegexSyntaxException(_definition, pos,
                    "Expected name or match in [...]; a name is either a single letter or {...}");
            }
            return name;
        }

        public IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> CreateState() {
            return new BeforeItemGraphkenState(new Dictionary<NodeBeforeItemMatch, object> {
                        { (NodeBeforeItemMatch)_graph.StartNode, null}
                    });
        }

        [DebuggerDisplay("{" + nameof(ToString) + "()}")]
        private abstract class AbstractGraphkenState<T, TMatch, TNode, TTargetNode>
            where TNode : NodeBefore<TMatch, TNode, TTargetNode> where TTargetNode : Node {

            private readonly IDictionary<TNode, object> _active;

            public IEnumerable<object> CountedObjects => _active.Select(se => se.Value).Where(obj => obj != null);

            protected AbstractGraphkenState([NotNull] IDictionary<TNode, object> active) {
                _active = active;
            }

            [ExcludeFromCodeCoverage]
            public override string ToString() {
                return GetType().Name + "(" + string.Join(",", _active.Select(kvp => kvp.Key)) + ")";
            }

            public override int GetHashCode() {
                return _active.Aggregate(0, (current, kvp) => current ^ kvp.Key.GetHashCode());
            }

            public override bool Equals(object obj) {
                var other = obj as AbstractGraphkenState<T, TMatch, TNode, TTargetNode>;
                if (other == null || other._active.Count != _active.Count) {
                    return false;
                } else {
                    foreach (var kvp in _active) {
                        object behind;
                        if (!other._active.TryGetValue(kvp.Key, out behind) || !Equals(kvp.Value, behind)) {
                            return false;
                        }
                    }
                    return true;
                }
            }

            public bool CanContinue => _active.Any();

            protected Dictionary<TTargetNode, object> AdvanceState(T obj, Func<TMatch, T, bool> match,
                out bool atEnd, out bool atCount) {
                if (!CanContinue) {
                    throw new InvalidOperationException("Advance is not possible on state with CanContinue=false");
                }

                var result = new Dictionary<TTargetNode, object>();
                bool c = false;
                foreach (var se in _active.Keys) {
                    MatchAndTarget<TMatch, TTargetNode>[] matchAndTargets =
                        se.CollectReachableViaEpsilons()
                          .SelectMany(n => n.Transitions)
                          .Where(t => (t.MatchesOrNullForMatchAny == null ||
                                         t.MatchesOrNullForMatchAny.Any(m => match(m, obj))) == !t.Invert)
                          .ToArray();
                    c |= matchAndTargets.Any(m => m.IsCount);

                    foreach (var t in matchAndTargets) {
                        if (t.TargetNode != null) {
                            object countedObj;
                            if (!result.TryGetValue(t.TargetNode, out countedObj) || countedObj == null) {
                                result[t.TargetNode] = t.IsCount ? obj : default(T);
                            }
                        }
                    }
                }
                atEnd = result.Keys.SelectMany(se => se.CollectAllReachableViaEpsilons()).Any(n => n.IsEnd);
                atCount = c;
                return result;
            }
        }

        private class BeforeItemGraphkenState :
            AbstractGraphkenState<TItem, TItemMatch, NodeBeforeItemMatch, NodeBeforeDependencyMatch>,
            IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> {

            public BeforeItemGraphkenState(Dictionary<NodeBeforeItemMatch, object> active) : base(active) {
            }

            public IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> Advance(
                TItem item, Func<TItemMatch, TItem, bool> itemMatch, out bool atEnd, out bool atCount) {
                return new BeforeDependencyGraphkenState(AdvanceState(item, itemMatch, out atEnd, out atCount));
            }
        }

        private class BeforeDependencyGraphkenState :
            AbstractGraphkenState<TDependency, TDependencyMatch, NodeBeforeDependencyMatch, NodeBeforeItemMatch>,
            IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> {
            public BeforeDependencyGraphkenState(Dictionary<NodeBeforeDependencyMatch, object> active) : base(active) {
            }

            public IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> Advance(
                TDependency dependency, Func<TDependencyMatch, TDependency, bool> dependencyMatch, out bool atCount) {
                bool ignoreAtEnd;
                return new BeforeItemGraphkenState(AdvanceState(dependency, dependencyMatch, out ignoreAtEnd, out atCount));
            }
        }
    }

    public interface IGraphkenState {
        bool CanContinue { get; }
        IEnumerable<object> CountedObjects { get; }
    }

    public interface IBeforeDependencyGraphkenState : IGraphkenState { }

    public interface IBeforeItemGraphkenState : IGraphkenState { }

    public interface IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> : IBeforeItemGraphkenState {
        IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> Advance(TItem item,
            Func<TItemMatch, TItem, bool> itemMatch, out bool atEnd, out bool atCount);
    }

    public interface IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> :
        IBeforeDependencyGraphkenState {
        IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> Advance(TDependency dependency,
            Func<TDependencyMatch, TDependency, bool> dependencyMatch, out bool atCount);
    }

    [ExcludeFromCodeCoverage]
    public class RegexSyntaxException : Exception {
        public RegexSyntaxException(string definition, int pos, string message)
            : base(CreateMessage(definition, pos, message)) {
            // empty
        }

        private static string CreateMessage(string definition, int pos, string message) {
            return message + " at '" + Substring(definition, pos - 4, 4) + ">>>" + Substring(definition, pos, 25) +
                   "'";
        }

        private static object Substring(string definition, int start, int length) {
            if (start < 0) {
                return definition.Substring(0, length + start);
            } else if (start > definition.Length) {
                return "";
            } else {
                return definition.Substring(start, Math.Min(definition.Length - start, length));
            }
        }
    }

    public static class PathRegex {
        public const string HELP = @"
Path regular expressions, or path regexes for short, are used to describe
patterns for paths. They are used by ""path finding transformers"" like
PathMarker or CycleFinder.

A path, in NDepCheck, is a sequence of dependencies. For the purpose of
path matching, it is considered to be an alternating sequence of items
and dependencies, e.g. 

    a:b'c  --'x-->  d:e'f  --'y->  g:h'i

This is an informal sketch of a path with two dependencies that
* starts at an item a:b which is marked with marker c,
* traverses a dependency marked with x that
* leads to an item d:e which is marked with marker f,
* and continues via a dependency makred with y
* to a final item g:h that is marked with i.
(For information on items, see '-? items').

A path regex is a general expression that can match such paths.
For this, it consists of an alternating sequence of item and dependency matches.
The simplest two matches are

* .  a single dot matches any dependency
* :  a single colon matches any item

The path example above would therefore be matched by the
path regex

    :.:.:

i.e., 

    an arbitrary item - arbitrary dependency - item - dependency - item

However, like real (string) regexes, path regexes also support looping and
alternating constructs. A group of matches can be put into parentheses and
suffixed with * (zero or more occurrences), + (one or more occurrences), or
? (zero or one occurrence). A simple path regex that would also match the
example path is

    :(.:)+

i.e., an item and then a non-empty sequence of alternating dependencies and
items. In fact, this path regex will match every possible path, as every
path consists of a starting item and then some dependencies.

Here is a more complex example: Match all paths from a node marked with 'C
to a node marked with 'I using only dependencies marked with 'i or 'm, and
count the number of 'C items leading to each 'I:

    {'C}# ( [{'i}{'m}] : )* [{'i}{'m}] {'I}

Explanation:

* We start with a path match (see '-? matches') that matches all items marked
  with C.
* Then we have a list of pairs of
** dependencies marked with 'i or 'm
** and a subsequent arbitrary item (the colon)
* Finally, a last dependency also marked with 'i or 'm leads to an 'I item.

This is a typical path that connects classes with the interfaces that they
implement in C# or Java.
From the example, one can see a few properties of path regexes:
* They can have embedded item and dependency matches for selective matching.
  This produces quite a lot of curly braces; but fortunately, when using
  them e.g. with PathMarker, this can often be abbreviated to
    C#([im]:)*[im]I
  which then looks very much like a standard string regex.
* They can contain embedded spaces which is ignored. Actually, also embedded
  underscores are ignored; this is useful when formatting a path regex used
  as an argument. The abbreviated expression above might then be written as
    C#_([im]_:)*_[im]_I
  for example.
* There are no ^ and $ markers; path regexes are always anchored at both ends.
* The # symbol indicates that the items (or dependencies) matching the preceding
  pattern should be counted. PathMarker uses this information to mark the items
  along the path with the number of ""reaching matches"".
* Finally - and not so easy to see -, * (and +) are neither ""eager"" nor 
  ""minimal"". Rather, they work like the original Kleene operator, which always 
  looks far enough ahead to decide whether the loop should be taken once more or
  exited. Internally, this is done by following all possibilities in parallel
  until one of the reaches the end.

It must be stressed that a path regex must describe an alternating sequence of 
items and dependencies. Hence, .* is not a valid path regex: It would describe 
a sequence of dependencies with no items in between. Also :* or [{a:b}]+ are
not allowed, as they would describe sequences of items without dependencies 
between them. As a final example, (:.|.) also makes no sense: It would be
a sequence of either an item and a dependency, or a dependency alone; but
such an expression could not follow anything: In x(:.|.), x could be neither
a dependency pattern (because then, with the second branch would, the two
dependency patterns x and . would ""collide"") nor an item pattern (as then,
item pattern x would immediately precede item pattern :.).

Here is the full syntax of path regexes. The meta symbols are:
* ::= is the symbol for each production
* | separates syntax alternatives
* {...} means zero or more repetitions,
* (...) groups syntax elements,
* ...? means zero or one occurrences
* '...' are literal strings
Spaces and _ are removed before the regex is parsed.

regex        ::= alternatives

alternatives ::= sequence { '|' sequence }

sequence     ::= { element }

element      ::= '(' alternatives ')' quantifier?
               | '[' '^'? pattern { pattern } ']' '#'?
               | pattern '#'?
               | '.' '#'?
               | ':' '#'?

pattern      ::= a single letter or digit
               | '{' { any char except closing brace } '}'

quantifier   ::= '*' | '+' | '?'

The left-hand sides of these syntax productions are called ""parts""
in the following.

For each part, it is uniquely defined whether it starts, and ends, with
an item or a dependency pattern. The definitions are as follows:
* . starts and ends with an dependency
* : starts and ends with an item
* a pattern starts and ends with the opposite of its preceeding pattern
* a non-empty sequence starts with whatever its first element starts with;
  and ends with whatever its last elements ends.
* an empty sequence starts with the opposite of what the part preceeding it
  ends; and ends with the same as what ends that preceeding part.
* alternatives start with what any of their subparts start; and end with
  what any of their subparts ends.
* a regex always starts with an item.
To be correct, a regex must also end with item; and any part inside must
end with the opposite that starts all poentially following parts.
The potentially following part of a part p are defined as follows:
* The textually following part;
* In addition, if p is a sequence with quantifier * or +, the first subpart
  of p is potentially following its last subpart.
* In addition, if a potentially following part q can match emptily, also
  the potentially following parts of q; where ""can match emptily"" is
  defined as follows:
** Every part with quantifier * or ? can match emptily;
** Every empty sequence can match emptily.
** An alternative can match emptily if at least one of its subparts
   (sequence) can match emptily.
** No other parts can match emptily.

Wew ... that should be it.
";

    }
}
