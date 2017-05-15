using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public struct MatchResult {
        private static readonly string[] NO_GROUPS = new string[0];

        public readonly bool Success;
        private readonly string[] _groups;

        public string[] Groups => _groups ?? NO_GROUPS;

        public MatchResult(bool success, string[] groups) {
            Success = success;
            _groups = groups;
        }
    }

    public class ItemMatch {
        [NotNull]
        private readonly ItemPattern _itemPattern;
        [NotNull]
        private readonly MarkerMatch _markerPattern;

        private static readonly MatchResult FAIL = new MatchResult();
        private static readonly MatchResult SUCCESS = new MatchResult(true, null);

        private readonly bool _inverse;

        public ItemMatch([CanBeNull] ItemType itemTypeOrNull, [NotNull] string pattern, int upperBoundOfGroupCount, bool ignoreCase) {
            if (pattern.StartsWith("~")) {
                _inverse = true;
                pattern = pattern.Substring(1);
            } else {
                _inverse = false;
            }
            string[] patternParts = pattern.Split('\'');
            _itemPattern = new ItemPattern(itemTypeOrNull, patternParts[0], upperBoundOfGroupCount, ignoreCase);
            _markerPattern = new MarkerMatch(patternParts.Length > 1 ? patternParts[1] : "", ignoreCase);
        }

        public ItemMatch([NotNull] string pattern, bool ignoreCase) : this(null, pattern, 0, ignoreCase) {
        }

        public ItemPattern ItemPattern => _itemPattern;

        public MatchResult Matches<TItem>([NotNull] AbstractItem<TItem> item, [CanBeNull] string[] references = null) where TItem : AbstractItem<TItem> {
            return _markerPattern.IsMatch(item.MarkerSet) 
                ? _itemPattern.Matches(item, _inverse, references) 
                : _inverse ? SUCCESS : FAIL;
        }

        public static bool IsMatch<TItem>(ItemMatch matchOrNull, AbstractItem<TItem> item) where TItem : AbstractItem<TItem> {
            return matchOrNull == null || matchOrNull.Matches(item).Success;
        }
    }
}