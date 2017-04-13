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

        public ItemMatch([CanBeNull] Dependency typeHintOrNull, [NotNull] string pattern, bool ignoreCase)
            : this(typeHintOrNull?.UsingItem.Type, pattern, ignoreCase) {
        }

        public ItemPattern ItemPattern => _itemPattern;

        public string[] Matches(Item item) {
            return _markerPattern.Match(item) ? _itemPattern.Matches(item) : null;
        }

        public static bool Matches(ItemMatch matchOrNull, INode node) {
            if (matchOrNull == null) {
                return true;
            } else if (!(node is Item)) {
                return true;
                    // TODO: INodes do not work with ItemMatch ... either I upgrade ItemMatch; oder I kill INode ...
            } else {
                return matchOrNull.Matches((Item) node) != null;
            }
        }
    }
}