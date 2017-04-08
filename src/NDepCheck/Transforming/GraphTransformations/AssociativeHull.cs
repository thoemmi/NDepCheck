using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.GraphTransformations {
    //public class AssociativeHull<T> : IGraphTransformation<T> where T : class, IEdge {
    //    private readonly Func<T, T, T> _concatEdge;
    //    //private readonly bool _outerOnly = false;

    //    public AssociativeHull(IEnumerable<string> options, Func<T,T,T> concatEdge) {
    //        _concatEdge = concatEdge;
    //        //foreach (var o in options) {
    //        //    if (o == "o") {
    //        //        _outerOnly = true;
    //        //    } else if (o == "a") {
    //        //        _outerOnly = false;
    //        //    } else {
    //        //        throw new ArgumentException("Unknown option for HideTransitiveEdges(HT): " + o);
    //        //    }
    //        //}
    //    }

    //    public IEnumerable<T> Run(IEnumerable<T> edges) {
    //        IDictionary<INode, IEnumerable<T>> nodesAndEdges = Dependency.Edges2NodesAndEdges(edges);

    //        IEnumerable<T> workingSet = edges;
    //        var result = new HashSet<T>();

    //        while (workingSet.Any()) {
    //            var newEdges = new HashSet<T>();
    //            foreach (var e1 in workingSet.Where(e => !e.UsingNode.Equals(e.UsedNode))) {
    //                foreach (var e2 in nodesAndEdges[e1.UsedNode]) {
    //                    T newEdge = _concatEdge(e1, e2);
    //                    if (result.Add(newEdge)) {
    //                        newEdges.Add(newEdge);
    //                    }
    //                }
    //            }
    //            workingSet = newEdges;
    //        }

    //        return result;
    //    }

    //    public string GetInfo() {
    //        return "Associative hull";
    //    }
    //}
}