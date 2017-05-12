using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class AbstractDepthFirstPathTraverser<TDependency, TItem, TDownInfo, TSaveInfo, TUpInfo>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        public struct ItemAndInt {
            [NotNull]
            private readonly TItem _item;
            private readonly int _int;

            public ItemAndInt([NotNull] TItem item, int @int) {
                _item = item;
                _int = @int;
            }

            public override bool Equals(object obj) {
                if (obj is ItemAndInt) {
                    ItemAndInt other = (ItemAndInt)obj;
                    return Equals(other._item, _item) && other._int == _int;
                } else {
                    return false;
                }
            }

            public override int GetHashCode() {
                return _item.GetHashCode();
            }
        }

        protected struct DownAndSave {
            public readonly TDownInfo Down;
            public readonly TSaveInfo Save;

            public DownAndSave(TDownInfo down, TSaveInfo save) {
                Down = down;
                Save = save;
            }
        }

        private static readonly TDependency[] NO_DEPENDENCIES = new TDependency[0];

        private readonly Stack<TDependency> _currentPath = new Stack<TDependency>();

        protected abstract bool VisitSuccessors(TItem tail, Stack<TDependency> currentPath, int expectedPathMatchIndex, 
                                                out TUpInfo initUpSum);

        protected abstract DownAndSave AfterPushDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex, 
                                                IPathMatch<TDependency, TItem> dependencyMatchOrNull, 
                                                IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, TDownInfo down);

        protected abstract TUpInfo BeforePopDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                                                IPathMatch<TDependency, TItem> dependencyMatchOrNull, 
                                                IPathMatch<TDependency, TItem> itemMatchOrNull, bool isEnd, 
                                                TSaveInfo save, TUpInfo upSum, TUpInfo childUp);

        protected abstract TUpInfo AfterVisitingSuccessors(bool visitSuccessors, TItem tail, Stack<TDependency> currentPath, 
                                                int expectedPathMatchIndex, TUpInfo upSum);

        protected TUpInfo Traverse([NotNull] TItem root,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            [NotNull] [ItemCanBeNull] IPathMatch<TDependency, TItem>[] expectedInnerPathMatches,
            [CanBeNull] IPathMatch<TDependency, TItem> endMatch, TDownInfo down) {

            return Traverse(root, incidentDependencies,
                expectedInnerPathMatches, 0, endMatch, down);
        }

        private TUpInfo Traverse([NotNull] TItem tail,
            [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            [NotNull, ItemCanBeNull] IPathMatch<TDependency, TItem>[] expectedInnerPathMatches,
            int expectedPathMatchIndex, IPathMatch<TDependency, TItem> endMatch, TDownInfo rawDown) {

            TUpInfo upSum;

            bool visitSuccessors = VisitSuccessors(tail, _currentPath, expectedPathMatchIndex, out upSum);
            if (visitSuccessors) {
                // We are at this item for the first time - check whether we find a path to some defined end

                TDependency[] dependencies;
                if (!incidentDependencies.TryGetValue(tail, out dependencies)) {
                    dependencies = NO_DEPENDENCIES;
                }
                int n = dependencies.Length;

                for (int i = 0; i < n; i++) {
                    TDependency nextDep = dependencies[i];
                    TItem nextTail = nextDep.UsedItem;

                    int newExpectedPathMatchIndex = expectedPathMatchIndex;

                    bool mayContinue = true;

                    IPathMatch<TDependency, TItem> dependencyMatchOrNull = null;
                    IPathMatch<TDependency, TItem> itemMatchOrNull = null;

                    if (newExpectedPathMatchIndex < expectedInnerPathMatches.Length) {
                        IPathMatch<TDependency, TItem> m = expectedInnerPathMatches[newExpectedPathMatchIndex];
                        if (m != null && m.Matches(nextDep)) {
                            newExpectedPathMatchIndex++;
                            mayContinue &= m.MayContinue;
                            dependencyMatchOrNull = m;
                        }
                    }

                    if (newExpectedPathMatchIndex < expectedInnerPathMatches.Length) {
                        var m = expectedInnerPathMatches[newExpectedPathMatchIndex];
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
                        mayContinue &= !expectedInnerPathMatches
                            .Take(expectedPathMatchIndex)
                            .Where(m => m != null && m.MayContinue && !m.MultipleOccurrencesAllowed)
                            .Any(m => m.Matches(nextDep) || m.Matches(nextTail));
                    }

                    if (mayContinue) {
                        _currentPath.Push(nextDep);

                        bool isEnd;
                        if (expectedPathMatchIndex >= expectedInnerPathMatches.Length) {
                            // We are at or behind the path match end; if the current item or dependency match, we have a real path end!
                            // Check whether we are end
                            isEnd = endMatch == null || endMatch.Matches(nextTail) || endMatch.Matches(nextDep);
                        } else {
                            isEnd = false;
                        }

                        DownAndSave downAndSave = AfterPushDependency(_currentPath, expectedPathMatchIndex, dependencyMatchOrNull, itemMatchOrNull, isEnd, rawDown);

                        TUpInfo childUp = Traverse(nextTail, incidentDependencies, expectedInnerPathMatches, newExpectedPathMatchIndex, endMatch, downAndSave.Down);

                        upSum = BeforePopDependency(_currentPath, expectedPathMatchIndex, dependencyMatchOrNull, itemMatchOrNull, isEnd, downAndSave.Save, upSum, childUp);

                        _currentPath.Pop();
                    }
                }
            }
            upSum = AfterVisitingSuccessors(visitSuccessors, tail, _currentPath, expectedPathMatchIndex, upSum);

            return upSum;
        }
    }
}