using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public class ItemMatch {
        [NotNull]
        private readonly ItemPattern _itemPattern;
        [NotNull]
        private readonly MarkerMatch _markerPattern;

        public ItemMatch([CanBeNull] ItemType itemTypeOrNull, [NotNull] string pattern, int upperBoundOfGroupCount, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            _itemPattern = new ItemPattern(itemTypeOrNull, patternParts[0], upperBoundOfGroupCount, ignoreCase);
            _markerPattern = new MarkerMatch(patternParts.Length > 1 ? patternParts[1] : "", ignoreCase);
        }

        public ItemMatch([NotNull] string pattern, bool ignoreCase) : this(null, pattern, 0, ignoreCase) {
        }

        public ItemPattern ItemPattern => _itemPattern;

        public string[] Matches<TItem>(AbstractItem<TItem> item, [CanBeNull] string[] references = null) where TItem : AbstractItem<TItem> {
            return _markerPattern.IsMatch(item.MarkerSet) ? _itemPattern.Matches(item, references) : null;
        }

        public static bool IsMatch<TItem>(ItemMatch matchOrNull, AbstractItem<TItem> item) where TItem : AbstractItem<TItem> {
            return matchOrNull == null || matchOrNull.Matches(item) != null;
        }

        public bool MatchesAlike(ItemMatch other) {
            return _markerPattern.MatchesAlike(other._markerPattern) && _itemPattern.MatchesAlike(other._itemPattern);
        }
    }
}