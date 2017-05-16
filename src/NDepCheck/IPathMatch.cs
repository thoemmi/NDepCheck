using System.Collections.Generic;
using System.Linq;
using NDepCheck.Matching;

namespace NDepCheck {
    public enum PathMatchResult {
        Match,
        Stop,
        Continue
    }

    public abstract class AbstractPathMatch<TDependency, TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        public bool MayContinue {
            get;
        }
        public bool MultipleOccurrencesAllowed {
            get;
        }
        private readonly AbstractPathMatch<TDependency, TItem>[] _dontMatches;

        protected AbstractPathMatch(bool multipleOccurrencesAllowed, bool mayContinue, IEnumerable<AbstractPathMatch<TDependency, TItem>> dontMatches) {
            MayContinue = mayContinue;
            MultipleOccurrencesAllowed = multipleOccurrencesAllowed;
            _dontMatches = dontMatches.ToArray();
        }

        public PathMatchResult Match(TDependency nextDep, TItem newTail) {
            if (_dontMatches.Any(m => m.IsMatch(nextDep, newTail))) {
                return PathMatchResult.Stop;
            } else if (IsMatch(nextDep, newTail)) {
                return PathMatchResult.Match;
            } else {
                return PathMatchResult.Continue;
            }
        }

        public abstract bool IsMatch(TDependency nextDep, TItem newTail);
    }

    public class DependencyPathMatch<TDependency, TItem> : AbstractPathMatch<TDependency, TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        private readonly DependencyMatch _dependencyMatch;

        public DependencyPathMatch(string pattern, bool ignoreCase, bool multipleOccurrencesAllowed, bool mayContinue, IEnumerable<AbstractPathMatch<TDependency, TItem>> dontMatches) : base(multipleOccurrencesAllowed, mayContinue, dontMatches) {
            _dependencyMatch = DependencyMatch.Create(pattern, ignoreCase);
        }

        public override bool IsMatch(TDependency nextDep, TItem newTail) {
            return _dependencyMatch.IsMatch(nextDep);
        }
    }

    public class ItemPathMatch<TDependency, TItem> : AbstractPathMatch<TDependency, TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {

        private readonly ItemMatch _itemMatch;

        public ItemPathMatch(string pattern, bool ignoreCase, bool multipleOccurrencesAllowed, bool mayContinue, IEnumerable<AbstractPathMatch<TDependency, TItem>> dontMatches) : base(multipleOccurrencesAllowed, mayContinue, dontMatches) {
            _itemMatch = new ItemMatch(pattern, ignoreCase);
        }

        public override bool IsMatch(TDependency nextDep, TItem newTail) {
            return _itemMatch.Matches(newTail).Success;
        }
    }

}