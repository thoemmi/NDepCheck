using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.GraphTransformations {
    public class HideTransitiveEdges <T> : IGraphTransformation<T> where T : class, IEdge  {
        // ReSharper disable once RedundantDefaultMemberInitializer - show default explicitly
        private readonly bool _outerOnly = false;

        public HideTransitiveEdges(IEnumerable<string> options) {
            foreach (var o in options) {
                if (o == "o") {
                    _outerOnly = true;
                } else if (o == "a") {
                    _outerOnly = false;
                } else {
                    throw new ArgumentException("Unknown option for HideTransitiveEdges(HT): " + o);
                }
            }
        }

        private static void ComputeMaximalDistanceFromN(INode n, IDictionary<INode, IEnumerable<T>> nodesAndEdges, int length, List<INode> path, Dictionary<INode, int> distanceFromRoot) {
            if (!path.Contains(n)) {
                int? distance = GetDistance(distanceFromRoot, n);

                if (distance.HasValue) {
                    if (length > distance) {
                        distanceFromRoot[n] = length;
                    }
                } else {
                    distanceFromRoot[n] = length;
                    path.Insert(0, n);
                    foreach (var e in nodesAndEdges[n].Where(e => !e.Hidden)) {
                        ComputeMaximalDistanceFromN(e.UsedNode, nodesAndEdges, length + 1, path, distanceFromRoot);
                    }
                    path.RemoveAt(0);
                }
            }
        }

        public IEnumerable<T> Run(IEnumerable<T> edges) {
            // TODO: Funktioniert vielleicht nicht richtig :-( - testen!!

            IDictionary<INode, IEnumerable<T>> nodesAndEdges = DependencyGrapher.Edges2NodesAndEdges(edges);

            foreach (var root in nodesAndEdges.Keys.Where(n => !_outerOnly || !n.IsInner)) {
                var distanceFromRoot = new Dictionary<INode, int>();

                foreach (var e in nodesAndEdges[root].Where(e => !e.Hidden)) {
                    // This will update all node.Value1 fields.
                    var path = new List<INode> { root };
                    ComputeMaximalDistanceFromN(e.UsedNode, nodesAndEdges, 1, path, distanceFromRoot);
                    if (path.Count != 1)
                        throw new InvalidProgramException("Internal error on traversal of " + e +
                                                          " - not all nodes removed from path");
                }

                foreach (var e in nodesAndEdges[root].Where(e => !e.Hidden)) {
                    int? distance = GetDistance(distanceFromRoot, e.UsedNode);
                    if (distance > 1) {
                        e.Hidden = true;
                    } else if (distance.HasValue) {
                        e.MarkCarrysTransitive();
                    }
                }
            }
            return edges;
        }

        private static int? GetDistance(Dictionary<INode, int> distanceFromRoot, INode node) {
            int result;
            return distanceFromRoot.TryGetValue(node, out result) ? result : (int?) null;
        }

        public string GetInfo() {
            return "Hiding transitive edges";
        }
    }
}