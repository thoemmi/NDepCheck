using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public class ItemMatch {
        [NotNull]
        private readonly ItemPattern _itemPattern;
        [NotNull]
        private readonly MarkerMatch _markerPattern;

        public ItemMatch([CanBeNull] ItemType itemTypeOrNull, [NotNull] string pattern, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            _itemPattern = new ItemPattern(itemTypeOrNull, patternParts[0], 0, ignoreCase);
            _markerPattern = new MarkerMatch(patternParts.Length > 1 ? patternParts[1] : "", ignoreCase);
        }

        public ItemMatch([NotNull] string pattern, bool ignoreCase) : this(null, pattern, ignoreCase) {
        }

        public ItemPattern ItemPattern => _itemPattern;

        public string[] Matches(AbstractItem item) {
            return _markerPattern.IsMatch(item.MarkerSet) ? _itemPattern.Matches(item) : null;
        }

        public static bool IsMatch(ItemMatch matchOrNull, AbstractItem item) {
            return matchOrNull == null || matchOrNull.Matches(item) != null;
        }
    }
}