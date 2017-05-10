using NDepCheck.Matching;

namespace NDepCheck {
    public interface IPathMatch<in TDependency, in TItem>
        where TDependency : AbstractDependency<TItem>
        where TItem : AbstractItem<TItem> {
        bool IsItemMatch {
            get;
        }
        bool MayContinue{
            get;
        }
        bool MultipleOccurrencesAllowed { get; }
        bool Matches(TDependency d);
        bool Matches(TItem i);
    }

    public class DependencyPathMatch<TDependency, TItem> : IPathMatch<TDependency, TItem>
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

        public bool Matches(TDependency d) => _dependencyMatch.IsMatch(d);

        public bool Matches(TItem i) => false;
    }

    public class ItemPathMatch<TDependency, TItem> : IPathMatch<TDependency, TItem>
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

        public bool Matches(TDependency d) => false;

        public bool Matches(TItem i) => _itemMatch.Matches(i).Success;
    }

}