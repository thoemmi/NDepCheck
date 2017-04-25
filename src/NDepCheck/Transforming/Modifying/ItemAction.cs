using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Modifying {
    public class ItemAction {
        // Group indexes                             1               2               3               4
        private const string PATTERN_PATTERN = @"^\s*([^\s]*)\s*->\s*([^\s]*)\s*--\s*([^\s]*)\s*=>\s*(.*)\s*$";

        private readonly DependencyMatch _atLeastOneIncomingDependencyMatch;
        private readonly ItemMatch _itemMatch;
        private readonly DependencyMatch _atLeastOneOutgoingDependencyMatch;
        private readonly IEnumerable<Action<Item>> _effects;

        public ItemAction(string line, bool ignoreCase, string fullConfigFileName, int startLineNo) {
            Match match = Regex.Match(line ?? "", PATTERN_PATTERN);
            if (!match.Success) {
                throw new ArgumentException(
                    $"Invalid item-action '{line}' at {fullConfigFileName}/{startLineNo}");
            } else {
                GroupCollection groups = match.Groups;
                if (groups[1].Value != "") {
                    _atLeastOneIncomingDependencyMatch = new DependencyMatch(groups[1].Value, ignoreCase);
                }
                if (groups[2].Value != "") {
                    _itemMatch = ItemMatch.CreateItemMatchWithGenericType(groups[2].Value, ignoreCase);
                }
                if (groups[3].Value != "") {
                    _atLeastOneOutgoingDependencyMatch = new DependencyMatch(groups[3].Value, ignoreCase);
                }
                if (groups[4].Value != "-" && groups[4].Value != "delete") {
                    var effects = new List<Action<Item>>();
                    var effectOptions =
                        groups[4].Value.Split(' ', ',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var effect in effectOptions) {
                        if (effect == "" || effect == "ignore") {
                            effects.Add(d => { });
                        } else if (effect.StartsWith("+")) {
                            effects.Add(i => i.AddMarker(effect.Substring(1)));
                        } else if (effect.StartsWith("-")) {
                            effects.Add(i => i.RemoveMarker(effect.Substring(1)));
                        } else {
                            throw new ArgumentException(
                                $"Unexpected edge directive '{effect}' at {fullConfigFileName}/{startLineNo}");
                        }
                    }
                    _effects = effects;
                } else {
                    _effects = null;
                }
            }
        }

        public bool Matches([CanBeNull] IEnumerable<Dependency> incoming, Item i, [CanBeNull] IEnumerable<Dependency> outgoing) {
            return (_atLeastOneIncomingDependencyMatch == null
                    || incoming != null && incoming.Any(d => _atLeastOneIncomingDependencyMatch.Matches(d)))
                && ItemMatch.Matches(_itemMatch, i)
                && (_atLeastOneOutgoingDependencyMatch == null
                    || outgoing != null && outgoing.Any(d => _atLeastOneOutgoingDependencyMatch.Matches(d)));
        }

        public bool Apply(Item i) {
            if (_effects == null) {
                return false;
            } else {
                foreach (var e in _effects) {
                    e(i);
                }
                return true;
            }
        }
    }
}