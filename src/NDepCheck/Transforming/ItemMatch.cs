namespace NDepCheck.Transforming {
    public class ItemMatch {
        private readonly ItemPattern _itempattern;
        private readonly MarkerPattern _markerPattern;

        public ItemMatch(ItemType itemType, string pattern, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            _itempattern = new ItemPattern(itemType, patternParts[0], 0, ignoreCase);
            _markerPattern = new MarkerPattern(patternParts.Length > 1 ? patternParts[1] : "");
        }

        public string[] Match(Item item) {
            return _markerPattern.Match(item) ? _itempattern.Match(item) : null;
        }
    }
}