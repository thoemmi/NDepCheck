// (c) HMMüller 2006...2015

using System;
using System.Linq;

namespace NDepCheck {
    /// <remarks>Class <c>DependencyRule_</c> knows enough 
    /// about an allowed or forbidden dependency so that it
    /// can find out whether a <c>Dependency_</c> matches it.
    /// Internally, the class stores (after an idea of
    /// Ralf Kretzschmar) the dependency as a single regular
    /// expression, which allows back-references
    /// (like \1) between the using and the used
    /// item.</remarks>
    public class DependencyRule : Pattern {
        private readonly ItemType _usingItemType;
        private readonly ItemType _usedItemType;
        private readonly DependencyRuleRepresentation _rep;
        private readonly IMatcher[] _using;
        private readonly IMatcher[] _used;
        private int _hitCount;

        // Dependency_ rules are created from lines with
        // a specific extension algorithm (see CreateDependencyRules()
        // below. Hence, the constructor is private.
        public DependencyRule(ItemType usingItemType, string usingItemPattern, ItemType usedItemType, string usedItemPattern,
                              DependencyRuleRepresentation rep, bool ignoreCase) {
            if (usingItemType == null) {
                throw new ArgumentNullException(nameof(usingItemType));
            }
            if (usedItemType == null) {
                throw new ArgumentNullException(nameof(usedItemType));
            }
            if (rep == null) {
                throw new ArgumentNullException(nameof(rep));
            }
            _using = CreateMatchers(usingItemType, usingItemPattern, 0, ignoreCase);
            _used = CreateMatchers(usedItemType, usedItemPattern, usingItemPattern.Count(c => c == '('), ignoreCase);
            _usingItemType = usingItemType;
            _usedItemType = usedItemType;
            _rep = rep;
        }

        public int HitCount => _hitCount;

        public DependencyRuleRepresentation Representation => _rep;

        public bool IsMatch(Dependency dependency) {
            // We check the types immediately, so that no unnecessary Match is run.
            if (dependency.UsingItem.Type != _usingItemType) {
                return false;
            }
            if (dependency.UsedItem.Type != _usedItemType) {
                return false;
            }

            string[] groupsInUsing = Match(dependency.UsingItem.Type, _using, dependency.UsingItem);

            if (groupsInUsing == null) {
                return false;
            } else {
                for (int i = 0; i < _used.Length; i++) {
                    string used = dependency.UsedItem.Values[i];
                    bool isMatch = _used[i].IsMatch(used, groupsInUsing);
                    if (!isMatch) {
                        return false;
                    }
                }

                _hitCount++;
                Representation.MarkHit();

                return true;
            }
        }
    }
}