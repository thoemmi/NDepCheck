using System.Collections.Generic;

namespace NDepCheck.Transforming.GraphTransformations {
    public interface IGraphTransformation<T> where T : class, IEdge {
        IEnumerable<T> Run(IEnumerable<T> edges);
        string GetInfo();
    }
}