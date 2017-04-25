using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public class ItemMatch {
        [NotNull]
        private readonly ItemPattern _itemPattern;
        [NotNull]
        private readonly MarkerPattern _markerPattern;

        public ItemMatch([CanBeNull] ItemType itemTypeOrNull, [NotNull] string pattern, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            _itemPattern = new ItemPattern(itemTypeOrNull, patternParts[0], 0, ignoreCase);
            _markerPattern = new MarkerPattern(patternParts.Length > 1 ? patternParts[1] : "", ignoreCase);
        }

        public static ItemMatch CreateItemMatchWithGenericType([NotNull] string pattern, bool ignoreCase) {
            return new ItemMatch(null, pattern, ignoreCase);
        }

        public ItemPattern ItemPattern => _itemPattern;

        public string[] Matches(Item item) {
            return _markerPattern.Match(item) ? _itemPattern.Matches(item) : null;
        }

        public static bool Matches(ItemMatch matchOrNull, Item item) {
            return matchOrNull == null || matchOrNull.Matches(item) != null;
        }
    }
}