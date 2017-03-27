using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Projecting {
    public class ProjectionSet : IProjectionSetElement {
        private readonly IEnumerable<IProjectionSetElement> _orderedAbstractions;

        public ProjectionSet(IEnumerable<IProjectionSetElement> orderedAbstractions) {
            _orderedAbstractions = orderedAbstractions;
        }

        public IEnumerable<Projection> AllProjections => _orderedAbstractions.SelectMany(e => e.AllProjections);
    }
}