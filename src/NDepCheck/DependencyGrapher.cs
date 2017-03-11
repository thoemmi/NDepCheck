// (c) HMMüller 2006...2017

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    /// <remarks>
    /// Class that creates AT&amp;T DOT output from
    /// dependencies.
    /// </remarks>
    public static class DependencyGrapher {

        //#region Nested type: Edge

        //private class Edge : IEdge {
        //    private readonly Node _usingNode;
        //    private readonly Node _usedNode;

        //    public int Ct;
        //    public int NotOkCt;
        //    public Dependency NotOkExample;

        //    private bool _onCycle;
        //    private bool _carrysTransitive;

        //    public Edge(Node usingNode, Node usedNode) {
        //        _usingNode = usingNode;
        //        _usedNode = usedNode;
        //        Ct = 0;
        //        NotOkCt = 0;
        //        NotOkExample = null;
        //    }

        //    public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
        //        // TODO: ?? And there should be a flag (in Edge?) "hasNotOkInfo", depending on whether dependency checking was done or not.
        //        return _usingNode.Name + " -> " + _usedNode.Name + " ["
        //                   + GetLabel(stringLengthForIllegalEdges)
        //                   + GetFontSize()
        //                   + GetStyle() + "];";
        //    }


        //    public INode UsedNode {
        //        get { return _usedNode; }
        //    }

        //    public bool Hidden { get; set; }

        //    public void MarkOnCycle() {
        //        _onCycle = true;
        //    }

        //    public void MarkCarrysTransitive() {
        //        _carrysTransitive = true;
        //    }

        //    public string AsString() {
        //        // _usingItem.AsString() + " -> " + _ct + ";" + _notOkCt + ";" + _notOkExample + " -> " + _usedItem.AsString();
        //        return _usingNode.Name + " -> " + Ct + ";" + NotOkCt + ";" + NotOkExample + " -> " + _usedNode.Name;
        //    }

        //    private string CountsAsString() {
        //        return (NotOkCt > 0 ? NotOkCt + " of " : "") + Ct;
        //    }

        //    private string GetFontSize() {
        //        return " fontsize=" + (10 + 5 * Math.Round(Math.Log10(Ct)));
        //    }

        //    private string GetLabel(int? stringLengthForIllegalEdges) {
        //        return "label=\"" + (stringLengthForIllegalEdges.HasValue && NotOkExample != null
        //                            ? LimitWidth(NotOkExample.UsingItem.AsString()/*???*/, stringLengthForIllegalEdges.Value) + " --->\\n" +
        //                              LimitWidth(NotOkExample.UsedItem.AsString()/*???*/, stringLengthForIllegalEdges.Value) + "\\n"
        //                            : "") +
        //                        " (" + CountsAsString() + ")" +
        //                        (_carrysTransitive ? "+" : "") +
        //                        "\"";
        //    }

        //    private string GetStyle() {
        //        return _onCycle ? " style=bold" : "";
        //    }

        //    public void AggregateInto(Edge edge) {
        //        edge.Ct += Ct;
        //        edge.NotOkCt += NotOkCt;
        //        edge.NotOkExample = edge.NotOkExample ?? NotOkExample;
        //    }
        //}

        //#endregion

        //#region Nested type: Node

        //private class Node : INode {
        //    private Dictionary<Node, Edge> _edges = new Dictionary<Node, Edge>();
        //    private readonly string _name;
        //    private bool _isInner;

        //    public Node(string name, bool isInner = false) {
        //        _name = name;
        //        _isInner = isInner;
        //    }

        //    public Node(string name, IEnumerable<Edge> e1, IEnumerable<Edge> e2, bool isInner)
        //        : this(name, isInner) {
        //        foreach (var e in e1.Concat(e2)) {
        //            Edge f = FindOrAddEdgeTo((Node)e.UsedNode);
        //            f.Ct += e.Ct;
        //            f.NotOkCt += e.NotOkCt;
        //            if (f.NotOkExample == null) {
        //                f.NotOkExample = e.NotOkExample;
        //            }
        //        }
        //    }

        //    public void MarkIsInner() {
        //        _isInner = true;
        //    }

        //    public bool IsInner {
        //        get { return _isInner; }
        //    }

        //    public IEnumerable<IEdge> Edges {
        //        get { return _edges.Values; }
        //    }

        //    public string Name {
        //        get { return _name; }
        //    }

        //    public bool Hidden { get; set; }

        //    public Edge FindOrAddEdgeTo(Node usedNode) {
        //        if (!_edges.ContainsKey(usedNode)) {
        //            _edges.Add(usedNode, new Edge(this, usedNode));
        //        }
        //        return _edges[usedNode];
        //    }

        //    public override string ToString() {
        //        return _name;
        //    }

        //    public IEnumerable<Edge> GetEdges() {
        //        return _edges.Values;
        //    }

        //    public void RepairParallelEdges(IDictionary<string, Node> nodes) {
        //        IEnumerable<Edge> oldEdges = _edges.Values;
        //        _edges = new Dictionary<Node, Edge>();
        //        foreach (var e in oldEdges) {
        //            Node usedNode = nodes[e.UsedNode.Name];
        //            Edge found;
        //            if (_edges.TryGetValue(usedNode, out found)) {
        //                found.AggregateInto(e);
        //            } else {
        //                _edges.Add(usedNode, new Edge(this, usedNode) { Ct = e.Ct, NotOkCt = e.NotOkCt, NotOkExample = e.NotOkExample });
        //            }
        //        }

        //    }
        //}

        //#endregion

        ////private static string LimitWidth(string s, int lg) {
        ////    if (s.Length > lg) {
        ////        s = "..." + s.Substring(s.Length - lg + 3);
        ////    }
        ////    return s;
        ////}

        private static void ReduceEdge(IEnumerable<Projection> orderedProjections, Dependency d, 
            Dictionary<Item, Item> uniqueNodes, Dictionary<FromTo, Dependency> result
            /*,Dictionary<Tuple<string, int>, Projection> skipCache*/) {

            Item usingMatch = orderedProjections
                                    //.Skip(GuaranteedNonMatching(d.UsingItem))
                                    //.SkipWhile(ga => ga != FirstPossibleAbstractionInCache(d.UsingItem, skipCache))
                                    .Select(ga => ga.Match(d.UsingItem))
                                    .FirstOrDefault(m => m != null);
            Item usedMatch = orderedProjections
                                    //.Skip(GuaranteedNonMatching(d.UsedItem))
                                    //.SkipWhile(ga => ga != FirstPossibleAbstractionInCache(d.UsedItem, skipCache))
                                    .Select(ga => ga.Match(d.UsedItem))
                                    .FirstOrDefault(n => n != null);

            if (usingMatch == null) {
                Log.WriteInfo("No graph output pattern found for drawing " + d.UsingItem.AsString() + " - I ignore it");
            } else if (usedMatch == null) {
                Log.WriteInfo("No graph output pattern found for drawing " + d.UsedItem.AsString() + " - I ignore it");
            } else if (usingMatch.IsEmpty() || usedMatch.IsEmpty()) {
                // ignore this edge!
            } else {
                Item usingNode = GetOrCreateNode(uniqueNodes, usingMatch);
                Item usedNode = GetOrCreateNode(uniqueNodes, usedMatch);

                FromTo key = new FromTo(usingNode, usedNode);

                Dependency reducedEdge;
                if (!result.TryGetValue(key, out reducedEdge)) {
                    reducedEdge = new Dependency(usingNode, usedNode, d.FileName, d.StartLine, d.StartColumn,
                                                 d.EndLine, d.EndColumn, d.Ct, d.NotOkCt, d.NotOkExample);
                    result.Add(key, reducedEdge);
                } else {
                    reducedEdge.AggregateCounts(d);
                }
            }
        }

        private static Item GetOrCreateNode(Dictionary<Item, Item> uniqueNodes, Item item) {
            if (!uniqueNodes.ContainsKey(item)) {
                uniqueNodes[item] = item;
            }
            return uniqueNodes[item];
        }

        public class FromTo {
            [NotNull]
            public readonly Item From;
            public readonly Item To;
            public FromTo([NotNull] Item from, [NotNull] Item to) {
                From = from;
                To = to;
            }

            public override bool Equals(object obj) {
                var other = obj as FromTo;
                return other != null && other.From == From && other.To == To;
            }

            public override int GetHashCode() {
                return From.GetHashCode() ^ To.GetHashCode();
            }
        }

        public static IEnumerable<Dependency> ReduceGraph(GlobalContext checkerContext, Options options) {
            var uniqueNodes = new Dictionary<Item, Item>();
            var result = new Dictionary<FromTo, Dependency>();

            foreach (var i in checkerContext.InputContexts) {
                DependencyRuleSet ruleSet = i.GetOrCreateDependencyRuleSetMayBeCalledInParallel(checkerContext, options, "REDUCEGRAPH??");
                if (ruleSet != null) {
                    Log.WriteInfo("Reducing graph " + i.Filename);
                    ReduceGraph(options, ruleSet, i.Dependencies, uniqueNodes, result);
                } else {
                    Log.WriteWarning("No rule set found for reducing " + i.Filename);
                    foreach (var e in i.Dependencies) {
                        result[new FromTo(e.UsingItem, e.UsedItem)] = e;
                    }
                }
            }
            return result.Values;
        }

        public static void ReduceGraph(Options options, [NotNull] DependencyRuleSet ruleSet,
                [NotNull] IEnumerable<Dependency> dependencies, Dictionary<Item, Item> uniqueNodes,
                Dictionary<FromTo, Dependency> edges) {
            List<Projection> orderedGraphAbstractions = ruleSet.ExtractGraphAbstractions();

            // First pass: Compute all edges - i.e., 
            // select the abstraction pattern = first
            // group from each regexp match and put it
            // into edgeToLabel and n.odes
            foreach (var d in dependencies) {
                ReduceEdge(orderedGraphAbstractions, d, uniqueNodes, edges/*, skipCache*/);
            }
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

        public class ZeroEdge : IWithCt {
            public int Ct => 0;

            public int NotOkCt => 0;
        }
    }
}