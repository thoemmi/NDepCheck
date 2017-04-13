using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Projecting {
    public class ProjectionSet : IProjectionSetElement {
        private readonly IEnumerable<IProjectionSetElement> _orderedAbstractions;
        private Projection[] _allOrderedAbstractionsCached;

        public ProjectionSet(IEnumerable<IProjectionSetElement> orderedAbstractions) {
            _orderedAbstractions = orderedAbstractions;
        }

        public Projection[] AllProjections {
            get {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression - caching pattern
                if (_allOrderedAbstractionsCached == null) {
                    _allOrderedAbstractionsCached = _orderedAbstractions.SelectMany(e => e.AllProjections).ToArray();
                }
                return _allOrderedAbstractionsCached;
            }
        }
    }
}