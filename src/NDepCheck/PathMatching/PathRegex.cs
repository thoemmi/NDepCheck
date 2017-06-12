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
        private const string SYMBOL_REGEX = @"\G\s*([*+?()|.:\[\]^$]|" + NAME_REGEX + ")";

        private readonly Dictionary<string, TItemMatch> _definedItemMatches;
        private readonly Dictionary<string, TDependencyMatch> _definedDependencyMatches;
        private readonly bool _ignoreCase;
        private readonly string _definition;
        private readonly Graphken _graph;
        private bool _isCountEncountered;

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
                    return m.Groups[1].Value;
                } else {
                    throw new RegexSyntaxException(_definition, pos, "No valid symbol");
                }
            }
        }

        public string PeekSymbol(int pos, bool eofOk = false) {
            string sym = RawPeekSymbol(pos).TrimStart('{').TrimEnd('}').Trim();
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
                    return new ZeroOrMore(startPos, inner);
                } else if (Matches(peekSym, "+")) {
                    AdvanceSymbolPos(ref pos);
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
            if (Matches(PeekSymbol(pos, eofOk: true), "$")) {
                if (_isCountEncountered) {
                    throw new RegexSyntaxException(_definition, pos, "$ can only be used once");
                }
                AdvanceSymbolPos(ref pos);
                isCount = true;
                _isCountEncountered = true;
            }
            return isCount;
        }

        [CanBeNull]
        private TItemMatch CreateItemMatch(int pos, [NotNull] string s) {
            try {
                TItemMatch definedMatch;
                return _definedItemMatches.TryGetValue(s, out definedMatch)
                    ? definedMatch
                    : CreateItemMatch(s, _ignoreCase);
            } catch (Exception ex) {
                throw new RegexSyntaxException(_definition, pos,
                    $"Cannot create ItemMatch from '{s}' - reason: {ex.Message}");
            }
        }

        protected abstract TItemMatch CreateItemMatch([NotNull] string pattern, bool ignoreCase);

        [CanBeNull]
        private TDependencyMatch CreateDependencyMatch(int pos, [NotNull] string s) {
            try {
                TDependencyMatch definedMatch;
                return _definedDependencyMatches.TryGetValue(s, out definedMatch)
                    ? definedMatch
                    : CreateDependencyMatch(s, _ignoreCase);
            } catch (Exception ex) {
                throw new RegexSyntaxException(_definition, pos,
                    $"Cannot create dependency match from '{s}' - reason: {ex.Message}");
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
            return
                new BeforeItemGraphkenState(new[]
                    {new StateElement<NodeBeforeItemMatch>((NodeBeforeItemMatch) _graph.StartNode, null /*????*/)});
        }

        private struct StateElement<TNode> where TNode : Node {
            public readonly TNode Node;
            public readonly object BehindCount;

            public StateElement([NotNull] TNode node, [CanBeNull] object behindCount) {
                Node = node;
                BehindCount = behindCount;
            }
        }

        private abstract class AbstractGraphkenState<T, TMatch, TNode, TTargetNode>
            where TNode : NodeBefore<TMatch, TNode, TTargetNode> where TTargetNode : Node {

            private readonly StateElement<TNode>[] _active;

            public IEnumerable<object> CountedObjects
                => _active.Select(se => se.BehindCount).Where(obj => !Equals(obj, default(T)));

            protected AbstractGraphkenState(IEnumerable<StateElement<TNode>> active) {
                _active = active.ToArray();
            }

            public bool CanContinue => _active.Any();

            protected IEnumerable<StateElement<TTargetNode>> AdvanceState(T obj, Func<TMatch, T, bool> match,
                out bool atEnd, out bool atCount) {
                if (!CanContinue) {
                    throw new InvalidOperationException("Advance is not possible on state with CanContinue=false");
                }

                var result = new List<StateElement<TTargetNode>>();
                bool c = false;
                foreach (var se in _active) {
                    MatchAndTarget<TMatch, TTargetNode>[] matchAndTargets =
                        se.Node
                          .CollectReachableViaEpsilons()
                          .SelectMany(n => n.Transitions)
                          .Where(t => (t.MatchesOrNullForMatchAny == null ||
                                         t.MatchesOrNullForMatchAny.Any(m => match(m, obj))) == !t.Invert)
                          .ToArray();
                    c |= matchAndTargets.Any(m => m.IsCount);

                    result.AddRange(matchAndTargets
                        .Where(t => t.TargetNode != null)
                        .Select(t => new StateElement<TTargetNode>(t.TargetNode, se.BehindCount ?? (t.IsCount ? obj : default(T)))));
                }
                atEnd = result.SelectMany(se => se.Node.CollectAllReachableViaEpsilons()).Any(n => n.IsEnd);
                atCount = c;
                return result;
            }
        }

        private class BeforeItemGraphkenState :
            AbstractGraphkenState<TItem, TItemMatch, NodeBeforeItemMatch, NodeBeforeDependencyMatch>,
            IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> {

            public BeforeItemGraphkenState(IEnumerable<StateElement<NodeBeforeItemMatch>> active) : base(active) {
            }

            public IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> Advance(
                TItem item, Func<TItemMatch, TItem, bool> itemMatch, out bool atEnd, out bool atCount) {
                return new BeforeDependencyGraphkenState(AdvanceState(item, itemMatch, out atEnd, out atCount));
            }
        }

        private class BeforeDependencyGraphkenState :
            AbstractGraphkenState<TDependency, TDependencyMatch, NodeBeforeDependencyMatch, NodeBeforeItemMatch>,
            IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> {
            public BeforeDependencyGraphkenState(IEnumerable<StateElement<NodeBeforeDependencyMatch>> active)
                : base(active) {
            }

            public IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> Advance(
                TDependency dependency, Func<TDependencyMatch, TDependency, bool> dependencyMatch, out bool atCount) {
                bool ignoreAtEnd;
                return
                    new BeforeItemGraphkenState(AdvanceState(dependency, dependencyMatch, out ignoreAtEnd,
                        out atCount));
            }
        }
    }

    public interface IGraphkenState {
        bool CanContinue { get; }
        IEnumerable<object> CountedObjects { get; }
    }

    public interface IBeforeItemGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> : IGraphkenState {
        IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> Advance(TItem item,
            Func<TItemMatch, TItem, bool> itemMatch, out bool atEnd, out bool atCount);
    }

    public interface IBeforeDependencyGraphkenState<TItem, TDependency, TItemMatch, TDependencyMatch> :
        IGraphkenState {
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
}
