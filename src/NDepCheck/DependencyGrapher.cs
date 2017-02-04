// (c) HMMüller 2006...2015

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        private static void ReduceEdge(IEnumerable<GraphAbstraction> orderedGraphAbstractions, 
                 Dependency d, Dictionary<string, Item> nodes, Dictionary<Tuple<Item, Item>, Dependency> result /*,Dictionary<Tuple<string, int>, GraphAbstraction> skipCache*/) {
            bool usingIsInner = false;
            bool usedIsInner = false;

            string usingMatch = orderedGraphAbstractions
                //.Skip(GuaranteedNonMatching(d.UsingItem))
                //.SkipWhile(ga => ga != FirstPossibleAbstractionInCache(d.UsingItem, skipCache))
                                    .Select(ga => ga.Match(d.UsingItem, out usingIsInner/*, skipCache*/))
                                    .FirstOrDefault(m => m != null);
            string usedMatch = orderedGraphAbstractions
                //.Skip(GuaranteedNonMatching(d.UsedItem))
                //.SkipWhile(ga => ga != FirstPossibleAbstractionInCache(d.UsedItem, skipCache))
                                    .Select(ga => ga.Match(d.UsedItem, out usedIsInner/*, skipCache*/))
                                    .FirstOrDefault(n => n != null);

            if (usingMatch == null) {
                Log.WriteInfo("No graph output pattern found for drawing " + d.UsingItem.AsString() + " - I ignore it");
            } else if (usedMatch == null) {
                Log.WriteInfo("No graph output pattern found for drawing " + d.UsedItem.AsString() + " - I ignore it");
            } else if (usingMatch == "" || usedMatch == "") {
                // ignore this edge!
            } else {

                Item usingNode = GetOrCreateNode(nodes, usingMatch, usingIsInner);
                Item usedNode = GetOrCreateNode(nodes, usedMatch, usedIsInner);

                Tuple<Item, Item> key = Tuple.Create(usingNode, usedNode);

                Dependency reducedEdge;
                if (!result.TryGetValue(key, out reducedEdge)) {
                    reducedEdge = new Dependency(usingNode, usedNode, d.FileName, d.StartLine, d.StartColumn, d.EndLine, d.EndColumn, d.Ct, d.NotOkCt, d.NotOkExample);
                    result.Add(key, reducedEdge);
                } else {
                    reducedEdge.AggregateCounts(d);
                }
            }
        }

        private static readonly ItemType REDUCED = new ItemType("REDUCED", new[] { "Name" }, new string[] { null });

        private static Item GetOrCreateNode(Dictionary<string, Item> nodes, string name, bool isInner) {
            if (!nodes.ContainsKey(name)) {
                nodes[name] = new Item(REDUCED, name, isInner);
            }
            Item result = nodes[name];
            return result;
        }

        public static IEnumerable<Dependency> ReduceGraph(GlobalContext checkerContext, Options options) {
            var nodes = new Dictionary<string, Item>();
            var result = new Dictionary<Tuple<Item, Item>, Dependency>();

            foreach (var i in checkerContext.InputContexts) {
                DependencyRuleSet ruleSet = i.GetOrCreateDependencyRuleSetMayBeCalledInParallel(checkerContext, options);
                Log.WriteInfo("Reducing graph " + i.Filename);
                ReduceGraph(options, ruleSet, i.Dependencies, nodes, result);
            }
            return result.Values;
        }

        public static void ReduceGraph(Options options, DependencyRuleSet ruleSet, IEnumerable<Dependency> dependencies,
                                       Dictionary<string, Item> nodes, Dictionary<Tuple<Item, Item>, Dependency> edges) {
            List<GraphAbstraction> orderedGraphAbstractions = ruleSet.ExtractGraphAbstractions();

            // First pass: Compute all edges - i.e., 
            // select the abstraction pattern = first
            // group from each regexp match and put it
            // into edgeToLabel and n.odes
            foreach (var d in dependencies) {
                ReduceEdge(orderedGraphAbstractions, d, nodes, edges /*, skipCache*/);
            }
        }

        public static void WriteDotFile(IEnumerable<IEdge> edges, TextWriter output, int? stringLengthForIllegalEdges) {
            IEnumerable<IEdge> visibleEdges = edges.Where(e => !e.Hidden);

            IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges = Edges2NodesAndEdges(visibleEdges);

            output.WriteLine("digraph D {");
            output.WriteLine("ranksep = 1.5;");

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                output.WriteLine("\"" + n.Name + "\" [shape=" + (n.IsInner ? "box,style=bold" : "oval") + "];");
            }

            output.WriteLine();

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                foreach (var e in nodesAndEdges[n].Where(e => e.UsingNode.IsInner || e.UsedNode.IsInner)) {
                    output.WriteLine(e.GetDotRepresentation(stringLengthForIllegalEdges));
                }
            }

            output.WriteLine("}");
        }

        private static INode GetOrCreateNode<T>(Dictionary<INode, INode> canonicalNodes, Dictionary<INode, List<T>> nodesAndEdges, INode node) where T: IEdge {
            INode result;
            if (!canonicalNodes.TryGetValue(node, out result)) {
                canonicalNodes.Add(node, result = node);
            }
            if (!nodesAndEdges.ContainsKey(result)) {
                nodesAndEdges.Add(result, new List<T>());
            }
            return result;
        }

        public static IDictionary<INode, IEnumerable<T>> Edges2NodesAndEdges<T>(IEnumerable<T> edges) where T : class, IEdge {
            Dictionary<INode, List<T>> result = Edges2NodesAndEdgesList(edges);
            return result.ToDictionary<KeyValuePair<INode, List<T>>, INode, IEnumerable<T>>(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static Dictionary<INode, List<T>> Edges2NodesAndEdgesList<T>(IEnumerable<T> edges) where T: IEdge{
            var canonicalNodes = new Dictionary<INode, INode>();
            var result = new Dictionary<INode, List<T>>();
            foreach (var e in edges) {
                INode @using = GetOrCreateNode(canonicalNodes, result, e.UsingNode);
                GetOrCreateNode(canonicalNodes, result, e.UsedNode);

                result[@using].Add(e);
            }
            return result;
        }

        public static void WriteMatrixFile(IEnumerable<IEdge> edges, TextWriter output, char format, int? stringLength, bool withNotOkCt) {
            IEnumerable<IEdge> visibleEdges = edges.Where(e => !e.Hidden);
            IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges = Edges2NodesAndEdges(visibleEdges);

            var innerAndReachableOuterNodes =
                new HashSet<INode>(nodesAndEdges.Where(n => n.Key.IsInner).SelectMany(kvp => new[] { kvp.Key }.Concat(kvp.Value.Select(e => e.UsedNode))));

            IEnumerable<INode> sortedNodes = MoreOrLessTopologicalSort(visibleEdges).Where(n => innerAndReachableOuterNodes.Contains(n));

            if (sortedNodes.Any()) {

                int m = 0;
                Dictionary<INode, int> node2Index = sortedNodes.ToDictionary(n => n, n => ++m);

                IEnumerable<INode> topNodes = sortedNodes.Where(n => n.IsInner);

                int labelWidth = stringLength ?? Math.Max(Math.Min(sortedNodes.Max(n => n.Name.Length), 30), 4);
                int colWidth = Math.Max(1 + ("" + visibleEdges.Max(e => e.Ct)).Length, // 1+ because of loop prefix
                    1 + ("" + sortedNodes.Count()).Length); // 1+ because of ! or % marker
                string nodeFormat = "{0," + (colWidth - 1) + ":" + Repeat('0', colWidth - 1) + "}";
                string ctFormat = "{0}{1," + (colWidth - 1) + ":" + Repeat('#', colWidth) + "}";

                switch (format) {
                    case '1': {
                        WriteFormat1Line(output, Limit("Id", colWidth), Limit("Name", labelWidth),
                            topNodes.Select(n => NodeId(n, nodeFormat, node2Index) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "")));

                        IWithCt ZERO_EDGE = new ZeroEdge();
                        foreach (var used in sortedNodes) {
                            INode used1 = used;
                            WriteFormat1Line(output, NodeId(used, nodeFormat, node2Index), Limit(used.Name, labelWidth),
                                topNodes.Select(@using => FormatCt(withNotOkCt, ctFormat,
                                    node2Index[@using] > node2Index[used1],
                                    nodesAndEdges[@using].FirstOrDefault(e => !e.Hidden && e.UsedNode.Equals(used1)) ?? ZERO_EDGE)));
                        }
                        break;
                    }
                    case '2': {
                        var emptyCtCols = Repeat(' ', colWidth) + (withNotOkCt ? ";" + Repeat(' ', colWidth) : "");
                        WriteFormat2Line(output, Limit("Id", colWidth), Limit("Name", labelWidth), Limit("Id", colWidth), Limit("Name", labelWidth), emptyCtCols);
                        foreach (var @using in topNodes) {
                            WriteFormat2Line(output, NodeId(@using, nodeFormat, node2Index), Limit(@using.Name, labelWidth), Limit("", colWidth), Limit("", labelWidth), emptyCtCols);
                            foreach (var used in sortedNodes) {
                                var edge = nodesAndEdges[@using].FirstOrDefault(e => !e.Hidden && e.UsedNode.Equals(used));
                                if (edge != null) {
                                    WriteFormat2Line(output, NodeId(@using, nodeFormat, node2Index), Limit(@using.Name, labelWidth), NodeId(used, nodeFormat, node2Index),
                                        Limit(used.Name, labelWidth),
                                        FormatCt(withNotOkCt, ctFormat, node2Index[@using] > node2Index[used], edge));
                                }
                            }
                        }
                        break;
                    }
                    default: {
                        Log.WriteError("Matrix format option /m" + format + " not supported");
                        break;
                    }
                }
            } else {
                Log.WriteError("No visible nodes and edges found for output");
            }
        }

        private static void WriteFormat2Line(TextWriter output, string id1, string name1, string id2, string name2, string cts) {
            output.Write(id1);
            output.Write(';');
            output.Write(name1);
            output.Write(';');
            output.Write(id2);
            output.Write(';');
            output.Write(name2);
            output.Write(';');
            output.WriteLine(cts);
        }

        private static string FormatCt(bool withNotOkCt, string ctFormat, bool rightUpper, IWithCt e) {
            return (e.Ct > 0 ? string.Format(ctFormat, rightUpper ? '#' : ' ', e.Ct)
                             : string.Format(ctFormat, ' ', 0))
                + (withNotOkCt ? ";" + (e.NotOkCt > 0 ? string.Format(ctFormat, rightUpper ? '*' : '~', e.NotOkCt)
                                                      : string.Format(ctFormat, ' ', 0))
                               : "");
        }

        private static string NodeId(INode n, string nodeFormat, Dictionary<INode, int> node2Index) {
            return (n.IsInner ? '!' : '%') + string.Format(nodeFormat, node2Index[n]);
        }

        public class ZeroEdge : IWithCt {
            public int Ct => 0;

            public int NotOkCt => 0;
        }

        private static void WriteFormat1Line(TextWriter output, string index, string label, IEnumerable<string> columns) {
            char sep = ';';
            output.Write(index);
            output.Write(sep);
            output.Write(label);
            foreach (var col in columns) {
                output.Write(sep);
                output.Write(col);
            }
            output.WriteLine();
        }

        private static string Limit(string s, int lg) {
            return (s + Repeat(' ', lg)).Substring(0, lg);
        }

        private static string Repeat(char c, int lg) {
            return string.Join("", Enumerable.Repeat(c, lg));
        }

        private static List<INode> MoreOrLessTopologicalSort(IEnumerable<IEdge> edges) {
            Dictionary<INode, List<IEdge>> nodesAndEdges = Edges2NodesAndEdgesList(edges);

            var result = new List<INode>();
            var resultAsHashSet = new HashSet<INode>();
            var candidates = new List<INode>(nodesAndEdges.Keys.OrderBy(n => n.Name));

            while (candidates.Any()) {
                //INode best =
                //    // First try real sinks, i.e., nodes where the number of outgoing edges is 0
                //    candidates.FirstOrDefault(n => WeightOfAllLeavingEdges(nodesAndEdges, n, e => true, e => 1) == 0)
                //    // Then, try "not ok sinks", i.e., nodes where there are as many not-ok edges to nodes below.
                //          ?? SinkCandidate(candidates, nodesAndEdges, e => !resultAsHashSet.Contains(e.UsedNode), e => 1000 * e.NotOkCt / (e.Ct + 1))
                //          ;

                // Keine ausgehenden Kanten; und keine reinkommenden mit notOk-Ct von candidates

                INode best = FindBest(candidates, true, nodesAndEdges) ?? FindBest(candidates, false, nodesAndEdges);

                if (best == null) {
                    throw new Exception("Error in algorithm - no best candidate found");
                }
                result.Add(best);
                resultAsHashSet.Add(best);

                candidates.Remove(best);
                foreach (var es in nodesAndEdges.Values) {
                    es.RemoveAll(e => e.UsedNode.Equals(best));
                }
            }
            result.Reverse();
            return result;
        }

        private static INode FindBest(List<INode> candidates, bool skipOutgoing, Dictionary<INode, List<IEdge>> nodesAndEdges) {
            var minimalIncomingNotOkWeight = int.MaxValue;
            var minimalLeavingWeight = int.MaxValue;
            INode result = null;
            foreach (var n in candidates) {
                int leavingWeight = nodesAndEdges[n].Where(e => !e.UsedNode.Equals(n)).Sum(e => e.Ct);

                if (skipOutgoing) {
                    if (leavingWeight > 0) {
                        continue;
                    }
                }

                int incomingNotOkWeight = candidates.Where(c => !c.Equals(n)).Sum(c => nodesAndEdges[c].Where(e => e.UsedNode.Equals(n)).Sum(e => e.NotOkCt));

                if (leavingWeight < minimalLeavingWeight
                    || leavingWeight == minimalLeavingWeight && incomingNotOkWeight < minimalIncomingNotOkWeight) {
                    minimalIncomingNotOkWeight = incomingNotOkWeight;
                    minimalLeavingWeight = leavingWeight;
                    result = n;
                }
            }
            return result;
        }
    }
}