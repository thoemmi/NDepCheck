using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class AbstractDepthFirstPathTraverser<TDependency, TItem, TDownInfo, TUpInfo>
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

        protected abstract TDownInfo AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, IPathMatch<TDependency, TItem> dependencyMatchOrNull, IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, TDownInfo down);

        protected abstract TUpInfo OnFoundCycleToRoot(Stack<TDependency> currentPath);

        protected abstract TUpInfo BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath, int incidentIndex, int incidentCount, bool isEnd, TUpInfo par);

        protected abstract TUpInfo AggregateUpInfo(TUpInfo sum, TUpInfo next);

        protected void Traverse([NotNull] TItem root, bool ignoreCyclesInThisRecursion,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            int maxLength, [NotNull, ItemCanBeNull] IPathMatch<TDependency, TItem>[] expectedPathMatches) {
            Traverse(root, root, ignoreCyclesInThisRecursion, incidentDependencies, new Dictionary<VisitedKey, int>(), maxLength,
                expectedPathMatches, 1, default(TDownInfo));
        }

        protected void Traverse([NotNull] TDependency toRoot, bool ignoreCyclesInThisRecursion,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            int maxLength, [NotNull, ItemCanBeNull] IPathMatch<TDependency, TItem>[] expectedPathMatches) {
            _currentPath.Push(toRoot);
            Traverse(toRoot.UsingItem, toRoot.UsedItem, ignoreCyclesInThisRecursion, incidentDependencies,
                new Dictionary<VisitedKey, int> { { new VisitedKey(toRoot.UsingItem, 0), maxLength - 1 } },
                maxLength - 1, expectedPathMatches, 1, default(TDownInfo));
        }

        private TUpInfo Traverse([NotNull] TItem root, [NotNull] TItem tail, bool ignoreCyclesInThisRecursion,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            [NotNull] Dictionary<VisitedKey, int> allVisitedItems, int restMaxLength,
            [NotNull, ItemCanBeNull] IPathMatch<TDependency, TItem>[] expectedPathMatches, int expectedPathMatchIndex, TDownInfo par) {

            int lengthCheckedBehindTail;

            VisitedKey tailVisitedKey = new VisitedKey(tail, expectedPathMatchIndex);

            TUpInfo upSum = default(TUpInfo);

            bool tailAlreadyVisited = allVisitedItems.TryGetValue(tailVisitedKey, out lengthCheckedBehindTail);
            if (!tailAlreadyVisited || lengthCheckedBehindTail < restMaxLength) {
                if (restMaxLength > 0 && incidentDependencies.ContainsKey(tail)) {
                    allVisitedItems[tailVisitedKey] = restMaxLength;
                    // we are at this item for the first time - check whether we find a path to some defined end

                    TDependency[] dependencies = incidentDependencies[tail];
                    int n = dependencies.Length;

                    bool upSumIsAssigned = false;

                    for (int i = 0; i < n; i++) {
                        TDependency nextDep = dependencies[i];
                        TItem nextTail = nextDep.UsedItem;

                        int newExpectedPathMatchIndex = expectedPathMatchIndex;

                        bool mayContinue = true;

                        IPathMatch<TDependency, TItem> dependencyMatchOrNull = null;
                        if (newExpectedPathMatchIndex < expectedPathMatches.Length) {
                            IPathMatch<TDependency, TItem> m = expectedPathMatches[newExpectedPathMatchIndex];
                            if (m != null && m.Matches(nextDep)) {
                                newExpectedPathMatchIndex++;
                                mayContinue &= m.MayContinue;
                                dependencyMatchOrNull = m;
                            }
                        }

                        IPathMatch<TDependency, TItem> itemMatchOrNull = null;
                        if (newExpectedPathMatchIndex < expectedPathMatches.Length) {
                            var m = expectedPathMatches[newExpectedPathMatchIndex];
                            if (m != null && m.Matches(nextTail)) {
                                newExpectedPathMatchIndex++;
                                mayContinue &= m.MayContinue;
                                itemMatchOrNull = m;
                            }
                        }

                        if (newExpectedPathMatchIndex == expectedPathMatchIndex) {
                            // Check that no "used up" non-multiple-occurrence positive ("MayContinue") path 
                            // match matches - but only if none of the two previous tests matched explicitly.
                            // This, I hope & believe, captures what one expects to hold implicitly:
                            // "No loop backs" to previous positive single patterns
                            mayContinue &= !expectedPathMatches
                                .Take(expectedPathMatchIndex)
                                .Where(m => m != null && m.MayContinue && !m.MultipleOccurrencesAllowed)
                                .Any(m => m.Matches(nextDep) || m.Matches(nextTail));
                        }

                        if (mayContinue) {
                            _currentPath.Push(nextDep);
                            VisitedKey newTailVisitedKey = new VisitedKey(nextTail, expectedPathMatchIndex);
                            bool alreadyVisitedUsedItem = allVisitedItems.ContainsKey(newTailVisitedKey);

                            bool isEnd;
                            if (expectedPathMatches.Length >= 2 && expectedPathMatchIndex >= expectedPathMatches.Length - 1) {
                                // We are at or behind the path match end; if the current item or dependency match, we have a real path end!
                                IPathMatch<TDependency, TItem> lastMatch = expectedPathMatches[expectedPathMatches.Length - 1];
                                // Check whether we are end
                                isEnd = lastMatch.Matches(nextTail) || lastMatch.Matches(nextDep);
                            } else {
                                isEnd = false;
                            }

                            TDownInfo down = AfterPushDependency(_currentPath, alreadyVisitedUsedItem, i, n, dependencyMatchOrNull, itemMatchOrNull, isEnd, par);
                            TUpInfo up;
                            if (!ignoreCyclesInThisRecursion && Equals(nextTail, root)) {
                                // We found a cycle to the rootItem!
                                up = OnFoundCycleToRoot(_currentPath);
                            } else {
                                up = Traverse(root, nextTail, false, incidentDependencies, allVisitedItems, restMaxLength - 1, expectedPathMatches, newExpectedPathMatchIndex, down);
                            }
                            BeforePopDependency(_currentPath, alreadyVisitedUsedItem, i, n, isEnd, up);

                            upSum = upSumIsAssigned ? AggregateUpInfo(upSum, up) : up;
                            upSumIsAssigned = true;

                            _currentPath.Pop();
                        }
                    }
                    if (_retraverseItems) {
                        allVisitedItems.Remove(tailVisitedKey);
                    }
                }
            }

            return upSum;
        }
    }
}