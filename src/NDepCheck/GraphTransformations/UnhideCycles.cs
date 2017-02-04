using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.GraphTransformations {
    public class UnhideCycles<T> : IGraphTransformation<T> where T : class, IEdge {
        private readonly bool _innerOnly = true;

        public UnhideCycles(IEnumerable<string> options) {
            foreach (var o in options) {
                if (o == "i") {
                    _innerOnly = true;
                } else if (o == "a") {
                    _innerOnly = false;
                } else {
                    throw new ArgumentException("Unknown option for UnhideCycles(UC): " + o);
                }
            }
        }

        public IEnumerable<T> Run(IEnumerable<T> edges) {
            IDictionary<INode, IEnumerable<T>> nodesAndEdges = DependencyGrapher.Edges2NodesAndEdges(edges);

            // For each node: traverse graph, bounded by set of visited nodes. If root node reappears, all edges are on cycle.
            // This is a O(|N|*|E|) algorithm ... so what.
            foreach (var n in nodesAndEdges.Keys.Where(n => !_innerOnly || n.IsInner)) {
                //FindCycle(n, new Dictionary<INode, bool> { { n, true } });
                FindCycle(n, n, nodesAndEdges, new HashSet<INode>(), new HashSet<INode> { n });
            }
            return edges;
        }

        public string GetInfo() {
            return "Unhiding cycles";
        }

        //private bool FindCycle(INode current, IDictionary<INode, bool> visitedNodes) {
        //    var leadsToCycle = false;
        //    foreach (var e in current.Edges) {
        //        if (_innerOnly && !e.UsedNode.IsInner) {
        //            // ignore the node
        //        } else {
        //            bool onCycle;
        //            if (visitedNodes.TryGetValue(e.UsedNode, out onCycle)) {
        //                e.Hidden = !onCycle;
        //                e.MarkOnCycle();
        //                leadsToCycle = true;
        //            } else {
        //                visitedNodes.Add(e.UsedNode, false);
        //                leadsToCycle |= FindCycle(e.UsedNode, visitedNodes);
        //                visitedNodes[e.UsedNode] = leadsToCycle;
        //            }
        //        }
        //    }
        //    return leadsToCycle;
        //}

        private void FindCycle(INode current, INode root, IDictionary<INode, IEnumerable<T>> nodesAndEdges, ISet<INode> visitedNodes, ISet<INode> onCycleFromRoot) {
            foreach (var e in nodesAndEdges[current]) {
                if (_innerOnly && !e.UsedNode.IsInner) {
                    // ignore the node
                // ReSharper disable once PossibleUnintendedReferenceComparison - object comparison is intended in graph algorithm
                } else if (e.UsedNode == root) {
                    MarkOnCycle(current, onCycleFromRoot, e);
                } else if (visitedNodes.Contains(e.UsedNode)) {
                    // no additional visit necessary
                } else {
                    visitedNodes.Add(e.UsedNode);
                    FindCycle(e.UsedNode, root, nodesAndEdges, visitedNodes, onCycleFromRoot);
                    if (onCycleFromRoot.Contains(e.UsedNode)) {
                        MarkOnCycle(current, onCycleFromRoot, e);
                    }
                }
            }
        }

        private static void MarkOnCycle(INode current, ISet<INode> onCycleFromRoot, IEdge e) {
            e.Hidden = false;
            e.MarkOnCycle();
            onCycleFromRoot.Add(current);
        }
    }
}