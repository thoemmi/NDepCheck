using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Reading {
    public class IgnoreReaderFactory : AbstractReaderFactory {
        public override AbstractDependencyReader CreateReader(string filename, GlobalContext options, bool needsOnlyItemTails) {
            return new IgnoreReader(filename);
        }
    }

    public class IgnoreReader : AbstractDependencyReader {
        public IgnoreReader(string filename) : base(filename) {
        }

        protected override IEnumerable<Dependency> ReadDependencies(InputContext inputContext, int depth) {
            return Enumerable.Empty<Dependency>();
        }
    }
}