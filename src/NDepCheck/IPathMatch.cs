using NDepCheck.Matching;

namespace NDepCheck {
    public interface IPathMatch {
        bool IsItemMatch {
            get;
        }
        bool MayContinue{
            get;
        }
        bool MultipleOccurrencesAllowed { get; }
        bool Matches(IMatchableObject d);
    }

    public class DependencyPathMatch<TDependency, TItem> : IPathMatch
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        public bool MayContinue {
            get;
        }
        public bool MultipleOccurrencesAllowed {
            get;
        }
        private readonly DependencyMatch _dependencyMatch;

        public DependencyPathMatch(string pattern, bool ignoreCase, bool multipleOccurrencesAllowed, bool mayContinue) {
            MultipleOccurrencesAllowed = multipleOccurrencesAllowed;
            MayContinue = mayContinue;
            _dependencyMatch = DependencyMatch.Create(pattern, ignoreCase);
        }

        public bool IsItemMatch => false;

        public bool Matches(IMatchableObject obj) => obj is TDependency && _dependencyMatch.IsMatch((TDependency) obj);
    }

    public class ItemPathMatch<TDependency, TItem> : IPathMatch
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        public bool MayContinue {
            get;
        }

        public bool MultipleOccurrencesAllowed {
            get;
        }
        private readonly ItemMatch _itemMatch;

        public ItemPathMatch(string pattern, bool ignoreCase, bool multipleOccurrencesAllowed, bool mayContinue) {
            MultipleOccurrencesAllowed = multipleOccurrencesAllowed;
            MayContinue = mayContinue;
            _itemMatch = new ItemMatch(pattern, ignoreCase);
        }

        public bool IsItemMatch => true;

        public bool Matches(IMatchableObject obj) => obj is TItem && _itemMatch.Matches((TItem) obj).Success;
    }

}