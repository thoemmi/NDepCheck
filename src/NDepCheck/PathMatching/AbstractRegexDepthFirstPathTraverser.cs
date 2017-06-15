using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.PathMatching {
    public enum CountedEnum { NotCounted, DependencyCounted, UsedItemCounted }

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

    public class PathStateElement<TDependency> {
        [NotNull]
        private readonly IBeforeDependencyGraphkenState _graphkenState;
        [NotNull]
        private readonly TDependency _dependency;

        public PathStateElement([NotNull] IBeforeDependencyGraphkenState graphkenState, [NotNull] TDependency dependency) {
            _graphkenState = graphkenState;
            _dependency = dependency;
        }

        public override int GetHashCode() {
            return _graphkenState.GetHashCode() ^ _dependency.GetHashCode();
        }

        public override bool Equals(object obj) {
            var other = obj as PathStateElement<TDependency>;
            return other != null && other._graphkenState.Equals(_graphkenState) && other._dependency.Equals(_dependency);
        }
    }

    public abstract class AbstractRegexDepthFirstPathTraverser<TDependency, TItem, TDownInfo, THereInfo, TUpInfo, TPathState>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {

        protected struct DownAndHere {
            public readonly TDownInfo Down;
            public readonly THereInfo Save;

            public DownAndHere(TDownInfo down, THereInfo save) {
                Down = down;
                Save = save;
            }
        }

        private static readonly TDependency[] NO_DEPENDENCIES = new TDependency[0];

        [NotNull]
        private readonly PathRegex<TItem, TDependency> _regex;
        [NotNull]
        private readonly Action _checkAbort;
        [NotNull, ItemNotNull]
        private readonly Stack<TDependency> _currentPath = new Stack<TDependency>();
        private readonly int _maxRecursionDepth;

        protected AbstractRegexDepthFirstPathTraverser([NotNull] PathRegex<TItem, TDependency> regex,
                [NotNull] Action checkAbort, int maxRecursionDepth = 1000) {
            _regex = regex;
            _checkAbort = checkAbort;
            _maxRecursionDepth = maxRecursionDepth;
        }

        protected abstract bool ShouldVisitSuccessors(TItem tail, Stack<TDependency> currentPath, out TUpInfo initUpSum);

        protected abstract TPathState CreateFirstStateElement(IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeNextDependencyState, TItem nextDep);
        protected abstract TPathState CreateStateElement(
            IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeNextDependencyState,
            TDependency nextDep);

        protected abstract DownAndHere AfterPushDependency(Stack<TDependency> currentPath, bool isEnd, bool isLoopBack,
            CountedEnum counted, TDownInfo down);

        protected abstract TUpInfo BeforePopDependency(Stack<TDependency> currentPath, bool isEnd, bool isLoopBack, 
            CountedEnum counted, TDownInfo down, THereInfo here, TUpInfo upSum, TUpInfo childUp);

        protected abstract TUpInfo AfterVisitingSuccessors(bool visitSuccessors, TItem tail, Stack<TDependency> currentPath,
                TUpInfo upSum);

        protected TUpInfo Traverse([NotNull] TItem root, [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
                Func<TItem,bool,TDownInfo> down) {
            IBeforeItemGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> initState = _regex.CreateState();
            bool atEnd, atCount;
            IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeDependencyState = initState.Advance(root, (m, i) => ItemMatch.IsMatch(m, i), out atEnd, out atCount);
            if (beforeDependencyState.CanContinue) {
                return Traverse(root, incidentDependencies, beforeDependencyState, down(root, atCount),
                    new HashSet<TPathState> { CreateFirstStateElement(beforeDependencyState, root) });
            } else {
                return default(TUpInfo); // ??????????
            }
        }

        private TUpInfo Traverse([NotNull] TItem tail, [NotNull] Dictionary<TItem, TDependency[]> incidentDependencies,
            IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeDependencyState,
            TDownInfo rawDown, HashSet<TPathState> statesOnPath) {
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
                    bool dependencyIsCounted;
                    IBeforeItemGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeItemState =
                        beforeDependencyState.Advance(nextDep, (m, d) => m.IsMatch(d), out dependencyIsCounted);
                    if (beforeItemState.CanContinue) {
                        TItem nextTail = nextDep.UsedItem;
                        bool atEnd;
                        bool itemIsCounted;
                        IBeforeDependencyGraphkenState<TItem, TDependency, ItemMatch, DependencyMatch> beforeNextDependencyState =
                            beforeItemState.Advance(nextTail, (m, i) => ItemMatch.IsMatch(m, i), out atEnd, out itemIsCounted);

                        _currentPath.Push(nextDep);

                        CountedEnum counted = 
                            dependencyIsCounted ? CountedEnum.DependencyCounted :
                            itemIsCounted ? CountedEnum.UsedItemCounted : 
                            CountedEnum.NotCounted;

                        var stateElement = CreateStateElement(beforeNextDependencyState, nextDep);
                        bool newOnPath = statesOnPath.Add(stateElement);

                        DownAndHere downAndHere = AfterPushDependency(_currentPath, atEnd, !newOnPath, counted, rawDown);

                        TUpInfo childUp;
                        if (_currentPath.Count < _maxRecursionDepth 
                            && beforeNextDependencyState.CanContinue
                            && newOnPath) {
                            childUp = Traverse(nextTail, incidentDependencies, beforeNextDependencyState,
                                downAndHere.Down, statesOnPath);
                            statesOnPath.Remove(stateElement);
                        } else {
                            childUp = default(TUpInfo); // ??? as above ____
                        }
                        upSum = BeforePopDependency(_currentPath, atEnd, !newOnPath, counted, rawDown, downAndHere.Save, upSum, childUp);

                        _currentPath.Pop();
                    }
                }
            }
            upSum = AfterVisitingSuccessors(visitSuccessors, tail, _currentPath, upSum);

            return upSum;
        }
    }
}