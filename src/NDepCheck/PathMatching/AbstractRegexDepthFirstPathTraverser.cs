using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.PathMatching {
    public class PathRegex<TItem, TDependency> : PathRegex<TItem, TDependency, ItemMatch, DependencyMatch> {
        public PathRegex([NotNull] string definition, [NotNull] Dictionary<string, ItemMatch> definedItemMatches,
            [NotNull] Dictionary<string, DependencyMatch> definedDependencyMatches, bool ignoreCase) 
            : base(definition, definedItemMatches, definedDependencyMatches, ignoreCase) {
        }

        protected override ItemMatch CreateItemMatch([NotNull] string pattern, bool ignoreCase) {
            return new ItemMatch(pattern, ignoreCase, anyWhereMatcherOk: false);
        }

        protected override DependencyMatch CreateDependencyMatch([NotNull] string pattern, bool ignoreCase) {
            return DependencyMatch.Create(pattern, ignoreCase);
        }
    }

    public abstract class AbstractRegexDepthFirstPathTraverser<TDependency, TItem, TDownInfo, THereInfo, TUpInfo>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        private readonly PathRegex<TItem, TDependency> _regex;
        private readonly Action _checkAbort;

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

        protected AbstractRegexDepthFirstPathTraverser(PathRegex<TItem, TDependency> regex, [NotNull] Action checkAbort) {
            _regex = regex;
            _checkAbort = checkAbort;
        }

        protected abstract bool ShouldVisitSuccessors(TItem tail, Stack<TDependency> currentPath,
                out TUpInfo initUpSum);

        protected abstract DownAndHere AfterPushDependency(Stack<TDependency> currentPath, bool isEnd, TDownInfo down, object countedObject);

        protected abstract TUpInfo BeforePopDependency(Stack<TDependency> currentPath, bool isEnd, THereInfo here, TUpInfo upSum, TUpInfo childUp, object countedObject);

        protected abstract TUpInfo AfterVisitingSuccessors(bool visitSuccessors, TItem tail, Stack<TDependency> currentPath,
                TUpInfo upSum);

        protected TUpInfo Traverse([NotNull] TItem root,
                [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
                TDownInfo down) {

            IBeforeItemGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> initState = _regex.CreateState();
            bool atEnd, atCount;
            IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeDependencyState = initState.Advance(root, (m, i) => ItemMatch.IsMatch(m, i), out atEnd, out atCount);
            if (beforeDependencyState.CanContinue) {
                return Traverse(root, incidentDependencies, beforeDependencyState, down, 
                    new HashSet<IBeforeDependencyGraphkenState> { beforeDependencyState }, atCount ? root : null);
            } else {
                return default(TUpInfo); // ??????????
            }
        }

        private TUpInfo Traverse([NotNull] TItem tail, [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies, 
            IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeDependencyState, 
            TDownInfo rawDown, HashSet<IBeforeDependencyGraphkenState> statesOnPath, object countedObject) {
            if (!beforeDependencyState.CanContinue) {
                throw new ArgumentException("Traverse must be called with continueable state", nameof(beforeDependencyState));
            }
            _checkAbort();

            TUpInfo upSum;
            bool visitSuccessors = ShouldVisitSuccessors(tail, _currentPath, out upSum);
            if (visitSuccessors) {
                // We are at this item for the first time - check whether we find a path to some defined end

                TDependency[] dependencies;
                if (!incidentDependencies.TryGetValue(tail, out dependencies)) {
                    dependencies = NO_DEPENDENCIES;
                }
                foreach (var nextDep in dependencies) {
                    bool atCount;
                    IBeforeItemGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeItemState =
                        beforeDependencyState.Advance(nextDep, (m, d) => m.IsMatch(d), out atCount);
                    if (atCount) {
                        countedObject = nextDep;
                    }
                    if (beforeItemState.CanContinue) {
                        TItem nextTail = nextDep.UsedItem;
                        bool atEnd;
                        IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeNextDependencyState =
                            beforeItemState.Advance(nextTail, (m, i) => ItemMatch.IsMatch(m, i), out atEnd, out atCount);
                        if (atCount) {
                            countedObject = nextDep;
                        }

                        _currentPath.Push(nextDep);
                        DownAndHere downAndHere = AfterPushDependency(_currentPath, atEnd, rawDown, countedObject);

                        TUpInfo childUp;
                        if (beforeNextDependencyState.CanContinue && statesOnPath.Add(beforeNextDependencyState)) { 
                            childUp = Traverse(nextTail, incidentDependencies, beforeNextDependencyState,
                                downAndHere.Down, statesOnPath, countedObject);
                            statesOnPath.Remove(beforeNextDependencyState);
                        } else {
                            childUp = default(TUpInfo); // ??? as above ____
                        }
                        upSum = BeforePopDependency(_currentPath, atEnd, downAndHere.Save, upSum, childUp, countedObject);

                        _currentPath.Pop();
                    }
                }
            }
            upSum = AfterVisitingSuccessors(visitSuccessors, tail, _currentPath, upSum);

            return upSum;
        }
    }
}