using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.ViolationChecking {
    // Tnis is now only a thin wrapper around a DependencyMatch - maybe the HitCount and Representation should
    // be moved there, and this class completely removed ...
    public class DependencyRule {
        [NotNull]
        private readonly DependencyMatch _match;
            
        private int _hitCount;

        public DependencyRule([NotNull] DependencyMatch match, [NotNull] DependencyRuleSource source) {
            _match = match;
            Source = source;
        }

        public int HitCount => _hitCount;

        [NotNull]
        public DependencyRuleSource Source { get; }

        [CanBeNull]
        public ItemMatch Using => _match.UsingMatch;

        [CanBeNull]
        public ItemMatch Used => _match.UsedMatch;

        public DependencyPattern DependencyPattern => _match.DependencyPattern;

        public bool IsMatch(Dependency dependency) {
            bool result = _match.IsMatch(dependency);
            if (result) {
                _hitCount++;
                Source.MarkHit();
            }
            return result;
        }

        public bool MatchesUsingPattern(string pattern) {
            return Source.TrimmedUsingPattern == pattern;
        }
    }
}