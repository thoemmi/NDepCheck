using System;
using System.Text.RegularExpressions;

namespace NDepCheck.Transforming.Modifying {
    public class ItemDependencyItemMatch {
        // Group indexes                             1           2           3 
        private const string PATTERN_PATTERN = @"^\s*(.*)\s*--\s*(.*)\s*->\s*(.*)\s*$";

        private readonly ItemMatch _usingMatch;
        private readonly DependencyMatch _dependencyMatch;
        private readonly ItemMatch _usedMatch;

        public ItemDependencyItemMatch(string value1, string value2, string value3, bool ignoreCase) {
            _usingMatch = value1 != "" ? ItemMatch.CreateItemMatchWithGenericType(value1, ignoreCase) : null;
            _dependencyMatch = value2 != "" ? new DependencyMatch(value2, ignoreCase) : null;
            _usedMatch = value3 != "" ? ItemMatch.CreateItemMatchWithGenericType(value3, ignoreCase) : null;
        }

        public static ItemDependencyItemMatch Create(string pattern, bool ignoreCase) {
            Match match = Regex.Match(pattern ?? "", PATTERN_PATTERN);
            if (!match.Success) {
                throw new ArgumentException($"Unexpected dependency pattern '{pattern}'");
            } else {
                GroupCollection groups = match.Groups;
                return new ItemDependencyItemMatch(groups[1].Value, groups[2].Value, groups[3].Value, ignoreCase);
            }
        }

        public bool IsMatch(Dependency d) {
            return ItemMatch.IsMatch(_usingMatch, d.UsingItem)
                   && (_dependencyMatch == null || _dependencyMatch.IsMatch(d))
                   && ItemMatch.IsMatch(_usedMatch, d.UsedItem);
        }
    }
}