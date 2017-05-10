using System.Collections.Generic;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck {
    public interface IPathMatch<in TDependency, in TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        bool IsItemMatch {
            get;
        }
        bool Matches(TDependency d);
        bool Matches(TItem i);
    }

    public class DependencyPathMatch<TDependency, TItem> : IPathMatch<TDependency, TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        private readonly DependencyMatch _dependencyMatch;

        public DependencyPathMatch(string pattern, bool ignoreCase) {
            _dependencyMatch = DependencyMatch.Create(pattern, ignoreCase);
        }

        public bool IsItemMatch => false;

        public bool Matches(TDependency d) => _dependencyMatch.IsMatch(d);

        public bool Matches(TItem i) => false;
    }

    public class ItemPathMatch<TDependency, TItem> : IPathMatch<TDependency, TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        private readonly ItemMatch _itemMatch;

        public ItemPathMatch(string pattern, bool ignoreCase) {
            _itemMatch = new ItemMatch(pattern, ignoreCase);
        }

        public bool IsItemMatch => true;

        public bool Matches(TDependency d) => false;

        public bool Matches(TItem i) => _itemMatch.Matches(i).Success;
    }

    public abstract class AbstractDepthFirstPathTraverser<TDependency, TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        private struct VisitedKey {
            [NotNull]
            private readonly TItem _item;
            private readonly int _expectedPathMatchIndex;

            public VisitedKey([NotNull] TItem item, int expectedPathMatchIndex) {
                _item = item;
                _expectedPathMatchIndex = expectedPathMatchIndex;
            }

            public override bool Equals(object obj) {
                if (obj is VisitedKey) {
                    VisitedKey other = (VisitedKey) obj;
                    return Equals(other._item, _item) && other._expectedPathMatchIndex == _expectedPathMatchIndex;
                } else {
                    return false;
                }
            }

            public override int GetHashCode() {
                return _item.GetHashCode();
            }
        }

        private readonly bool _retraverseItems;
        private readonly Stack<TDependency> _currentPath = new Stack<TDependency>();

        protected AbstractDepthFirstPathTraverser(bool retraverseItems) {
            _retraverseItems = retraverseItems;
        }

        protected abstract void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail);

        protected abstract void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd);

        protected abstract void OnFoundCycleToRoot(Stack<TDependency> currentPath);

        protected abstract void BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd);

        ////protected abstract void OnPathEnd(Stack<TDependency> currentPath);

        protected void Traverse([NotNull] TItem root, bool ignoreCyclesInThisRecursion,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            int maxLength, [NotNull] IPathMatch<TDependency, TItem>[] expectedPathMatches) {
            Traverse(root, root, ignoreCyclesInThisRecursion, incidentDependencies, new Dictionary<VisitedKey, int>(), maxLength,
                expectedPathMatches, 1);
        }
        protected void Traverse([NotNull] TDependency toRoot, bool ignoreCyclesInThisRecursion,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            int maxLength, [NotNull] IPathMatch<TDependency, TItem>[] expectedPathMatches) {
            _currentPath.Push(toRoot);
            Traverse(toRoot.UsingItem, toRoot.UsedItem, ignoreCyclesInThisRecursion, incidentDependencies,
                new Dictionary<VisitedKey, int> { { new VisitedKey(toRoot.UsingItem, 0), maxLength - 1 } },
                maxLength - 1, expectedPathMatches, 1);
        }

        private void Traverse(TItem root, [NotNull] TItem tail, bool ignoreCyclesInThisRecursion,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            [NotNull] Dictionary<VisitedKey, int> allVisitedItems, int restMaxLength,
            [NotNull] IPathMatch<TDependency, TItem>[] expectedPathMatches, int expectedPathMatchIndex) {

            int lengthCheckedBehindTail;

            VisitedKey tailVisitedKey = new VisitedKey(tail, expectedPathMatchIndex);

            bool tailAlreadyVisited = allVisitedItems.TryGetValue(tailVisitedKey, out lengthCheckedBehindTail);
            if (!tailAlreadyVisited || lengthCheckedBehindTail < restMaxLength) {
                if (restMaxLength > 0 && incidentDependencies.ContainsKey(tail)) {
                    allVisitedItems[tailVisitedKey] = restMaxLength;
                    // we are at this item for the first time - check whether we find a path to some defined end

                    TDependency[] dependencies = incidentDependencies[tail];
                    int n = dependencies.Length;
                    for (int i = 0; i < n; i++) {
                        TDependency nextDep = dependencies[i];

                        int newExpectedPathMatchIndex = expectedPathMatchIndex;

                        if (newExpectedPathMatchIndex < expectedPathMatches.Length && expectedPathMatches[newExpectedPathMatchIndex].Matches(nextDep)) {
                            newExpectedPathMatchIndex++;
                        }

                        TItem newTail = nextDep.UsedItem;

                        if (newExpectedPathMatchIndex < expectedPathMatches.Length && expectedPathMatches[newExpectedPathMatchIndex].Matches(tail)) {
                            newExpectedPathMatchIndex++;
                        }

                        _currentPath.Push(nextDep);
                        VisitedKey newTailVisitedKey = new VisitedKey(newTail, expectedPathMatchIndex);
                        bool alreadyVisitedUsedItem = allVisitedItems.ContainsKey(newTailVisitedKey);


                        bool isEnd;
                        if (expectedPathMatches.Length >= 2 && expectedPathMatchIndex >= expectedPathMatches.Length - 1) {
                            // We are at or behind the path end; if the current item or dependency match, we have a real path end!
                            IPathMatch<TDependency, TItem> lastMatch = expectedPathMatches[expectedPathMatches.Length - 1];
                            // Check whether we are end
                            isEnd = lastMatch.Matches(newTail) || lastMatch.Matches(nextDep);
                        } else {
                            isEnd = true;
                        }





                        AfterPushDependency(_currentPath, alreadyVisitedUsedItem, i, n, isEnd);
                        if (!ignoreCyclesInThisRecursion && Equals(newTail, root)) {
                            // We found a cycle to the rootItem!
                            OnFoundCycleToRoot(_currentPath);
                        } else {
                            Traverse(root, newTail, false, incidentDependencies, allVisitedItems, restMaxLength - 1, expectedPathMatches, newExpectedPathMatchIndex);
                        }
                        BeforePopDependency(_currentPath, alreadyVisitedUsedItem, i, n, isEnd);
                        _currentPath.Pop();
                    }
                    if (_retraverseItems) {
                        allVisitedItems.Remove(tailVisitedKey);
                    }
                }

                //if (isEnd) {
                //    OnPathEnd(_currentPath);
                //}
            }
        }
    }
}