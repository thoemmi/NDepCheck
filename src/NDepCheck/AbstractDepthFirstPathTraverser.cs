using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class AbstractDepthFirstPathTraverser<TDependency, TItem, TDownInfo, THereInfo, TUpInfo>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        private readonly Action _checkAbort;

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
                    ItemAndInt other = (ItemAndInt) obj;
                    return Equals(other._item, _item) && other._int == _int;
                } else {
                    return false;
                }
            }

            public override int GetHashCode() {
                return _item.GetHashCode();
            }
        }

        protected struct DownAndHere {
            public readonly TDownInfo Down;
            public readonly THereInfo Save;

            public DownAndHere(TDownInfo down, THereInfo save) {
                Down = down;
                Save = save;
            }
        }

        private static readonly TDependency[] NO_DEPENDENCIES = new TDependency[0];

        private readonly Stack<TDependency> _currentPath = new Stack<TDependency>();

        protected AbstractDepthFirstPathTraverser([NotNull] Action checkAbort) {
            _checkAbort = checkAbort;
        }

        protected abstract bool ShouldVisitSuccessors(TItem tail, Stack<TDependency> currentPath, int expectedPathMatchIndex,
                out TUpInfo initUpSum);

        protected abstract DownAndHere AfterPushDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                AbstractPathMatch<TDependency, TItem> pathMatchOrNull, bool isEnd, TDownInfo down);

        protected abstract TUpInfo BeforePopDependency(Stack<TDependency> currentPath, int expectedPathMatchIndex,
                AbstractPathMatch<TDependency, TItem> pathMatchOrNull, bool isEnd,
                THereInfo here, TUpInfo upSum, TUpInfo childUp);

        protected abstract TUpInfo AfterVisitingSuccessors(bool visitSuccessors, TItem tail, Stack<TDependency> currentPath,
                int expectedPathMatchIndex, TUpInfo upSum);

        protected TUpInfo Traverse([NotNull] TItem root,
                [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
                [NotNull] [ItemCanBeNull] AbstractPathMatch<TDependency, TItem>[] expectedInnerPathMatches,
                [CanBeNull] AbstractPathMatch<TDependency, TItem> endMatch, TDownInfo down) {

            return Traverse(root, incidentDependencies, expectedInnerPathMatches, 0, endMatch, false, down);
        }

        private TUpInfo Traverse([NotNull] TItem tail,
                [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
                [NotNull, ItemCanBeNull] AbstractPathMatch<TDependency, TItem>[] expectedInnerPathMatches,
                int expectedPathMatchIndex, AbstractPathMatch<TDependency, TItem> endMatch, bool parentIsEnd, TDownInfo rawDown) {
            _checkAbort();

            TUpInfo upSum;
            bool visitSuccessors = ShouldVisitSuccessors(tail, _currentPath, expectedPathMatchIndex, out upSum);
            if (parentIsEnd && endMatch != null && !endMatch.MultipleOccurrencesAllowed) {
                // If the end match matched, and the end match may occur only once, we do not visit the children
            } else {
                if (visitSuccessors) {
                    // We are at this item for the first time - check whether we find a path to some defined end

                    TDependency[] dependencies;
                    if (!incidentDependencies.TryGetValue(tail, out dependencies)) {
                        dependencies = NO_DEPENDENCIES;
                    }
                    foreach (var nextDep in dependencies) {
                        TItem nextTail = nextDep.UsedItem;

                        int newExpectedPathMatchIndex = expectedPathMatchIndex;

                        bool mayContinue = true;

                        AbstractPathMatch< TDependency,TItem> pathMatchOrNull = null;

                        if (newExpectedPathMatchIndex < expectedInnerPathMatches.Length) {
                            AbstractPathMatch<TDependency, TItem> m = expectedInnerPathMatches[newExpectedPathMatchIndex];
                            PathMatchResult pathResult = m.Match(nextDep, nextTail);
                            switch (pathResult) {
                                case PathMatchResult.Match:
                                    newExpectedPathMatchIndex++;
                                    pathMatchOrNull = m;
                                    break;
                                case PathMatchResult.Stop:
                                    mayContinue = false;
                                    break;
                                case PathMatchResult.Continue:
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }

                        bool isEnd;
                        if (newExpectedPathMatchIndex >= expectedInnerPathMatches.Length) {
                            // We are at or behind the path match end; if the current item or dependency matches, we have a real path end!
                            // Check whether we are really at an end.
                            // If no end match was provided (i.e., only a start pattern given), all items are accepted as end items.
                            // Otherwise, we check whether the last item or dependency matches the end match.
                            isEnd = endMatch == null || endMatch.IsMatch(nextDep, nextTail);
                        } else {
                            isEnd = false;
                        }

                        if (!isEnd && newExpectedPathMatchIndex == expectedPathMatchIndex) {
                            // Check that no "used up" non-multiple-occurrence positive path 
                            // match matches - but only if we did not find a path match exactly here,
                            // and we are not at the end (which is also "finding a match", namely
                            // the endMatch).
                            // This, I hope & believe, captures what one expects to hold implicitly:
                            // "No loop backs" to previous positive single patterns
                            mayContinue &= !expectedInnerPathMatches
                                            .Take(expectedPathMatchIndex)
                                            .Where(m => m != null && m.MayContinue && !m.MultipleOccurrencesAllowed)
                                            .Any(m => m.IsMatch(nextDep, nextTail));
                        }

                        if (mayContinue) {
                            _currentPath.Push(nextDep);

                            DownAndHere downAndHere = AfterPushDependency(_currentPath, expectedPathMatchIndex, 
                                                                          pathMatchOrNull, isEnd, rawDown);

                            TUpInfo childUp = Traverse(nextTail, incidentDependencies, expectedInnerPathMatches, 
                                                       newExpectedPathMatchIndex, endMatch, isEnd, downAndHere.Down);

                            upSum = BeforePopDependency(_currentPath, expectedPathMatchIndex, pathMatchOrNull, 
                                                        isEnd, downAndHere.Save, upSum, childUp);

                            _currentPath.Pop();
                        }
                    }
                }
            }
            upSum = AfterVisitingSuccessors(visitSuccessors, tail, _currentPath, expectedPathMatchIndex, upSum);

            return upSum;
        }
    }
}