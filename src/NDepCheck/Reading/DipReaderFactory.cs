using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Reading {
    public class DipReaderFactory : AbstractReaderFactory {
        private readonly Dictionary<string, ItemType> _registeredItemTypes = new Dictionary<string, ItemType>();

        [CanBeNull]
        public ItemType GetDescriptor(string name) {
            ItemType result;
            _registeredItemTypes.TryGetValue(name, out result);
            return result;
        }

        public override AbstractDependencyReader CreateReader(string filename, GlobalContext options, bool needsOnlyItemTails) {
            return new DipReader(filename, this);
        }

        public void AddItemType([NotNull]ItemType itemType) {
            if (!_registeredItemTypes.ContainsKey(itemType.Name)) {
                _registeredItemTypes.Add(itemType.Name, itemType);
            }
        }
    }
}