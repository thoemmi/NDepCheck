// (c) HMMüller 2006...2015

using System;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.ViolationChecking {
    /// <remarks>Class <c>DependencyRule_</c> knows enough 
    /// about an allowed or forbidden dependency so that it
    /// can find out whether a <c>Dependency_</c> matches it.
    /// Internally, the class stores (after an idea of
    /// Ralf Kretzschmar) the dependency as a single regular
    /// expression, which allows back-references
    /// (like \1) between the using and the used
    /// item.</remarks>
    public class DependencyRule {
        [NotNull]
        private readonly ItemType _usingItemType;
        [NotNull]
        private readonly ItemType _usedItemType;

        //[NotNull]
        //private readonly IMatcher[] _using;
        //[NotNull]
        //private readonly IMatcher[] _used;
        [NotNull]
        private readonly ItemPattern _using;
        [NotNull]
        private readonly ItemPattern _used;


        private int _hitCount;

        // Dependency_ rules are created from lines with
        // a specific extension algorithm (see CreateDependencyRules()
        // below. Hence, the constructor is private.
        public DependencyRule([NotNull] ItemType usingItemType, string usingItemPattern, [NotNull] ItemType usedItemType,
            string usedItemPattern, [NotNull] DependencyRuleRepresentation rep, bool ignoreCase)
            : this(usingItemType: usingItemType, @using: new ItemPattern(usingItemType, usingItemPattern, 0, ignoreCase), usedItemType: usedItemType, 
                  used: new ItemPattern(usedItemType, usedItemPattern, usingItemPattern.Count(c => c == '('), ignoreCase), rep: rep) {
        }

        public DependencyRule([NotNull] ItemType usingItemType, [NotNull] ItemPattern @using, 
                              [NotNull] ItemType usedItemType, [NotNull] ItemPattern used, [NotNull] DependencyRuleRepresentation rep) {
            if (usingItemType == null) {
                throw new ArgumentNullException(nameof(usingItemType));
            }
            if (usedItemType == null) {
                throw new ArgumentNullException(nameof(usedItemType));
            }
            if (rep == null) {
                throw new ArgumentNullException(nameof(rep));
            }
            _using = @using;
            _used = used;
            _usingItemType = usingItemType;
            _usedItemType = usedItemType;
            Representation = rep;
        }

        public int HitCount => _hitCount;

        [NotNull]
        public DependencyRuleRepresentation Representation { get; }

        [NotNull]
        public ItemPattern Using => _using;

        [NotNull]
        public ItemPattern Used => _used;

        public bool IsMatch(Dependency dependency) {
            // We check the types immediately, so that no unnecessary Match is run.
            if (!dependency.UsingItem.Type.Equals(_usingItemType)) {
                return false;
            }
            if (!Equals(dependency.UsedItem.Type, _usedItemType)) {
                return false;
            }

            if (Log.IsChattyEnabled) {
                Log.WriteInfo("Checking " + dependency + " against " + Representation);
            }

            string[] groupsInUsing = _using.Matches(dependency.UsingItem);

            if (groupsInUsing == null) {
                return false;
            } else {
                IMatcher[] usedMatchers = _used.Matchers;
                for (int i = 0; i < usedMatchers.Length; i++) {
                    string used = dependency.UsedItem.Values[i];
                    bool isMatch = usedMatchers[i].IsMatch(used, groupsInUsing);
                    if (!isMatch) {
                        return false;
                    }
                }

                _hitCount++;
                Representation.MarkHit();

                return true;
            }
        }

        public bool MatchesUsingPattern(ItemPattern other) {
            IMatcher[] usingMatchers = _using.Matchers;
            IMatcher[] otherMatchers = other.Matchers;
            for (int i = 0; i < Math.Min(usingMatchers.Length, otherMatchers.Length); i++) {
                if (!usingMatchers[i].MatchesAlike(otherMatchers[i])) {
                    return false;
                }
            }
            return true;
        }
    }
}