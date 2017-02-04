using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck {
    internal class DipReaderFactory : AbstractReaderFactory {
        private readonly Dictionary<string, ItemType> _registeredItemTypes = new Dictionary<string, ItemType>();

        public override IEnumerable<ItemType> GetDescriptors() {
            return _registeredItemTypes.Values;
        }

        [CanBeNull]
        public ItemType GetDescriptor(string name) {
            ItemType result;
            _registeredItemTypes.TryGetValue(name, out result);
            return result;
        }

        public override bool Accepts(string extension) {
            return extension == "dip";
        }

        public override AbstractDependencyReader CreateReader(string filename, Options options, bool needsOnlyItemTails) {
            return new DipReader(filename, this);
        }

        public void AddItemType([NotNull]ItemType itemType) {
            _registeredItemTypes.Add(itemType.Name, itemType);
        }
    }
}