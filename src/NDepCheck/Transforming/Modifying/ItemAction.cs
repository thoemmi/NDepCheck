using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.Modifying {
    public class ItemAction {
        // Group indexes                             1               2               3               4
        private const string PATTERN_PATTERN = @"^\s*([^\s]*)\s*->\s*([^\s]*)\s*--\s*([^\s]*)\s*=>\s*(.*)\s*$";

        private readonly DependencyPattern _atLeastOneIncomingDependencyPattern;
        private readonly ItemMatch _itemMatch;
        private readonly DependencyPattern _atLeastOneOutgoingDependencyPattern;
        private readonly IEnumerable<Action<Item>> _effects;

        public ItemAction(string line, bool ignoreCase, string fullConfigFileName, int startLineNo) {
            Match match = Regex.Match(line ?? "", PATTERN_PATTERN);
            if (!match.Success) {
                throw new ArgumentException(
                    $"Invalid item-action '{line}' at {fullConfigFileName}/{startLineNo}");
            } else {
                GroupCollection groups = match.Groups;
                if (groups[1].Value != "") {
                    _atLeastOneIncomingDependencyPattern = new DependencyPattern(groups[1].Value, ignoreCase);
                }
                if (groups[2].Value != "") {
                    _itemMatch = new ItemMatch(groups[2].Value, ignoreCase);
                }
                if (groups[3].Value != "") {
                    _atLeastOneOutgoingDependencyPattern = new DependencyPattern(groups[3].Value, ignoreCase);
                }
                if (groups[4].Value != "-" && groups[4].Value != "delete") {
                    var effects = new List<Action<Item>>();
                    var effectOptions =
                        groups[4].Value.Split(' ', ',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var effect in effectOptions) {
                        if (effect == "" || effect == "ignore" || effect == "keep") {
                            effects.Add(d => { });
                        } else if (effect.StartsWith("+")) {
                            effects.Add(i => i.IncrementMarker(effect.Substring(1)));
                        } else if (effect.StartsWith("-")) {
                            effects.Add(i => i.RemoveMarkers(effect.Substring(1), ignoreCase));
                        } else {
                            throw new ArgumentException(
                                $"Unexpected item directive '{effect}' at {fullConfigFileName}/{startLineNo}");
                        }
                    }
                    _effects = effects;
                } else {
                    _effects = null;
                }
            }
        }

        public bool Matches([CanBeNull] IEnumerable<Dependency> incoming, Item i, [CanBeNull] IEnumerable<Dependency> outgoing) {
            return (_atLeastOneIncomingDependencyPattern == null
                    || incoming != null && incoming.Any(d => _atLeastOneIncomingDependencyPattern.IsMatch(d)))
                && ItemMatch.IsMatch(_itemMatch, i)
                && (_atLeastOneOutgoingDependencyPattern == null
                    || outgoing != null && outgoing.Any(d => _atLeastOneOutgoingDependencyPattern.IsMatch(d)));
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