using System.Collections.Generic;
using System.Linq;

namespace NDepCheck {
    public class IgnoreReaderFactory : AbstractReaderFactory {

        public override IEnumerable<ItemType> GetDescriptors() {
            return new ItemType[0];
        }

        public override bool Accepts(string extension) {
            return extension == "pdb";
        }

        public override AbstractDependencyReader CreateReader(string filename, Options options, bool needsOnlyItemTails) {
            return new IgnoreReader(filename);
        }
    }

    public class IgnoreReader : AbstractDependencyReader {
        public IgnoreReader(string filename) : base(filename) {
        }

        protected override IEnumerable<Dependency> ReadDependencies() {
            return Enumerable.Empty<Dependency>();
        }
    }
}