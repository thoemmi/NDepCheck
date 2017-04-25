using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NDepCheck.Transforming.Modifying {
    public class DependencyAction {
        // Group indexes                             1           2           3           4
        private const string PATTERN_PATTERN = @"^\s*(.*)\s*--\s*(.*)\s*->\s*(.*)\s*=>\s*(.*)\s*$";

        private readonly ItemMatch _usingMatch;
        private readonly DependencyMatch _dependencyMatch;
        private readonly ItemMatch _usedMatch;
        private readonly IEnumerable<Action<Dependency>> _effects;

        public DependencyAction(string line, bool ignoreCase, string fullConfigFileName, int startLineNo) {
            Match match = Regex.Match(line ?? "", PATTERN_PATTERN);
            if (!match.Success) {
                throw new ArgumentException($"Unexpected dependency pattern '{line}' at {fullConfigFileName}/{startLineNo}");
            } else {
                GroupCollection groups = match.Groups;
                if (groups[1].Value != "") {
                    _usingMatch = ItemMatch.CreateItemMatchWithGenericType(groups[1].Value, ignoreCase);
                }
                if (groups[2].Value != "") {
                    _dependencyMatch = new DependencyMatch(groups[2].Value, ignoreCase);
                }
                if (groups[3].Value != "") {
                    _usedMatch = ItemMatch.CreateItemMatchWithGenericType(groups[3].Value, ignoreCase);
                }
                if (groups[4].Value != "-" && groups[4].Value != "delete") {
                    var effects = new List<Action<Dependency>>();
                    var effectOptions =
                        groups[4].Value.Split(' ', ',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var effect in effectOptions) {
                        if (effect == "-?" || effect == "reset-questionable") {
                            effects.Add(d => d.ResetQuestionable());
                        } else if (effect == "+?" || effect == "mark-questionable") {
                            effects.Add(d => d.MarkAsQuestionable());
                        } else if (effect == "?" || effect == "increment-questionable") {
                            effects.Add(d => d.IncrementQuestionable());
                        } else if (effect == "-!" || effect == "reset-bad") {
                            effects.Add(d => d.ResetBad());
                        } else if (effect == "+!" || effect == "mark-bad") {
                            effects.Add(d => d.MarkAsBad());
                        } else if (effect == "!" || effect == "increment-bad") {
                            effects.Add(d => d.IncrementBad());
                        } else if (effect == "" || effect == "ignore" || effect == "keep") {
                            effects.Add(d => { });
                        } else if (effect.StartsWith("+")) {
                            effects.Add(d => d.AddMarker(effect.Substring(1)));
                        } else if (effect.StartsWith("-")) {
                            effects.Add(d => d.RemoveMarker(effect.Substring(1)));
                        } else {
                            throw new ArgumentException($"Unexpected dependency directive '{effect}' at {fullConfigFileName}/{startLineNo}");
                        }
                    }
                    _effects = effects;
                } else {
                    _effects = null;
                }
            }
        }

        public bool Match(Dependency d) {
            return ItemMatch.Matches(_usingMatch, d.UsingItem)
                   && (_dependencyMatch == null || _dependencyMatch.Matches(d))
                   && ItemMatch.Matches(_usedMatch, d.UsedItem);
        }

        public bool Apply(Dependency d) {
            if (_effects == null) {
                return false;
            } else {
                foreach (var e in _effects) {
                    e(d);
                }
                return true;
            }
        }
    }
}