using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.ViolationChecking {
    // Tnis is now only a thin wrapper around a DependencyMatch - maybe the HitCount and Representation should
    // be moved there, and this class completely removed ...
    public class DependencyRule {
        [NotNull]
        private readonly DependencyMatch _match;
            
        private int _hitCount;

        public DependencyRule([NotNull] DependencyMatch match, [NotNull] DependencyRuleRepresentation rep) {
            _match = match;
            Representation = rep;
        }

        public int HitCount => _hitCount;

        [NotNull]
        public DependencyRuleRepresentation Representation { get; }

        [CanBeNull]
        public ItemMatch Using => _match.UsingMatch;

        [CanBeNull]
        public ItemMatch Used => _match.UsedMatch;

        public DependencyPattern DependencyPattern => _match.DependencyPattern;

        public bool IsMatch(Dependency dependency) {
            bool result = _match.IsMatch(dependency);
            if (result) {
                _hitCount++;
                Representation.MarkHit();
            }
            return result;
        }

        public bool MatchesUsingPattern(ItemMatch otherMatch) {
            return _match.UsingMatch?.MatchesAlike(otherMatch) ?? otherMatch == null;
        }
    }
}