using System.Collections.Generic;

namespace NDepCheck.Transforming.Projecting {
    public interface IProjectionSetElement {
        IEnumerable<Projection> AllProjections { get; }
    }
}