using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.GraphTransformations {
    public class HidePureSinks <T> : IGraphTransformation<T> where T : class, IEdge  {
        private readonly bool _outerOnly = true;
        private readonly int _depth = 1;

        public HidePureSinks(IEnumerable<string> options) {
            foreach (var o in options) {
                if (o.StartsWith("d=")) {
                    _depth = int.Parse(o.Substring(2));
                } else if (o == "i") {
                    _outerOnly = true;
                } else if (o == "a") {
                    _outerOnly = false;
                } else {
                    throw new ArgumentException("Unknown option for HideTransitiveEdges(HT): " + o);
                }
            }
        }

        public IEnumerable<T> Run(IEnumerable<T> edges) {
            IDictionary<INode, IEnumerable<T>> nodesAndEdges = DependencyGrapher.Edges2NodesAndEdges(edges);

            IEnumerable<INode> consideredNodes = nodesAndEdges.Keys.Where(n => !_outerOnly || !n.IsInner);
            for (int d = 0; d < _depth; d++) {
                var sinks = new HashSet<INode>(consideredNodes.Where(n => nodesAndEdges[n].All(e => e.Hidden || e.UsedNode.Equals(n))));
                foreach (var n in consideredNodes) {
                    foreach (var e in nodesAndEdges[n].Where(e => sinks.Contains(e.UsedNode))) {
                        e.Hidden = true;
                    }
                }
            }
            return edges;
        }

        public string GetInfo() {
            return "Hiding pure sink nodes (depth=" + _depth + ")";
        }
    }
}