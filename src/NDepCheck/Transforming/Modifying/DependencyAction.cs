using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.Modifying {
    public class DependencyAction {
        // Group indexes                             1           2           3           4
        private const string PATTERN_PATTERN = @"^\s*(.*)\s*--\s*(.*)\s*->\s*(.*)\s*=>\s*(.*)\s*$";

        private readonly DependencyMatch _match;
        private readonly IEnumerable<Action<Dependency>> _effects;

        public DependencyAction(string line, bool ignoreCase, string fullConfigFileName, int startLineNo) {
            Match match = Regex.Match(line ?? "", PATTERN_PATTERN);
            if (!match.Success) {
                throw new ArgumentException($"Unexpected dependency pattern '{line}' at {fullConfigFileName}/{startLineNo}");
            } else {
                GroupCollection groups = match.Groups;
                _match = new DependencyMatch(null, groups[1].Value, groups[2].Value, null, groups[3].Value, ignoreCase);
                if (groups[4].Value != "-" && groups[4].Value != "delete") {
                    var effects = new List<Action<Dependency>>();
                    var effectOptions =
                        groups[4].Value.Split(' ', ',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
                    foreach (var effect in effectOptions) {
                        if (effect == "-?" || effect == "reset-questionable") {
                            effects.Add(d => d.ResetQuestionable());
                        } else if (effect == "+?" || effect == "mark-questionable") {
                            effects.Add(d => d.MarkAsQuestionable(effect));
                        } else if (effect == "?" || effect == "increment-questionable") {
                            effects.Add(d => d.IncrementQuestionable(effect));
                        } else if (effect == "-!" || effect == "reset-bad") {
                            effects.Add(d => d.ResetBad());
                        } else if (effect == "+!" || effect == "mark-bad") {
                            effects.Add(d => d.MarkAsBad(effect));
                        } else if (effect == "!" || effect == "increment-bad") {
                            effects.Add(d => d.IncrementBad(effect));
                        } else if (effect == "" || effect == "ignore" || effect == "keep") {
                            effects.Add(d => { });
                        } else if (effect.StartsWith("+")) {
                            effects.Add(d => d.IncrementMarker(effect.Substring(1)));
                        } else if (effect.StartsWith("-")) {
                            effects.Add(d => d.RemoveMarkers(effect.Substring(1), ignoreCase));
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

        public bool IsMatch(Dependency dependency) {
            return _match.IsMatch(dependency);
        }
    }
}