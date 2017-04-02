namespace NDepCheck.Transforming {
    public class ItemMatch : Pattern {
        private readonly ItemType _itemType;
        private readonly IMatcher[] _matchers;

        public ItemMatch(ItemType itemType, string pattern, bool ignoreCase) {
            _itemType = itemType;
            _matchers = CreateMatchers(_itemType, pattern, 0, ignoreCase);
        }

        public bool Match(Item item) {
            return Match(_itemType, _matchers, item) != null;
        }
    }
}