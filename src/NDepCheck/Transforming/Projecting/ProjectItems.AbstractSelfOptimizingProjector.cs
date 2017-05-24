using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems {
        public interface IResortableProjectorWithCost : IComparable<IResortableProjectorWithCost>, IProjector {
            double CostPerProjection { get; }

            void ReduceCostCountsInReorganizeToForgetHistory();
        }

        public abstract class AbstractSelfOptimizingProjector<TResortableProjectorWithCost> : AbstractProjector 
            where TResortableProjectorWithCost : IResortableProjectorWithCost {
            protected readonly IEqualityComparer<char> _equalityComparer;

            private readonly List<TResortableProjectorWithCost> _projectors;
            private readonly IProjector _fallBackProjector;

            protected readonly int _reorganizeIntervalIncrement;
            protected int _reorganizeInterval;
            private int _stepsToNextReorganize;

            protected AbstractSelfOptimizingProjector(Projection[] orderedProjections, bool ignoreCase, int reorganizeIntervalIncrement, string name) 
                : base(name) {
                _stepsToNextReorganize = _reorganizeInterval = _reorganizeIntervalIncrement = reorganizeIntervalIncrement;

                _equalityComparer = ignoreCase
                    ? (IEqualityComparer<char>) new CharIgnoreCaseEqualityComparer()
                    : EqualityComparer<char>.Default;

                // The following is ok if derived projectors do not initialize something in their
                // state which they use in CreateSelectingProjectors ...
                // ReSharper disable once VirtualMemberCallInConstructor 
                _projectors = CreateResortableProjectors(orderedProjections);

                _fallBackProjector = new SimpleProjector(orderedProjections, name: "fallback");
            }

            public IEnumerable<TResortableProjectorWithCost> ProjectorsForTesting => _projectors;

            protected abstract List<TResortableProjectorWithCost> CreateResortableProjectors(Projection[] orderedProjections);

            private void Reorganize() {
                _projectors.Sort();
                foreach (var p in _projectors) {
                    p.ReduceCostCountsInReorganizeToForgetHistory();
                }
            }

            public override Item Project(Environment cachingEnvironment, Item item, bool left) {
                if (_stepsToNextReorganize-- < 0) {
                    Reorganize();
                    _reorganizeInterval += _reorganizeIntervalIncrement;
                    _stepsToNextReorganize = _reorganizeInterval;
                }
                return ((IProjector) SelectProjector(_projectors, item, left, _stepsToNextReorganize) ?? _fallBackProjector).Project(cachingEnvironment, item, left);
            }

            [CanBeNull]
            protected abstract TResortableProjectorWithCost SelectProjector(IReadOnlyList<TResortableProjectorWithCost> projectors, 
                                                                            Item item, bool left, int stepsToNextReorganize);
        }
    }
}
