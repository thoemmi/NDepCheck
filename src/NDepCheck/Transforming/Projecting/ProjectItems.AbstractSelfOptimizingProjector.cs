using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NDepCheck.Reading;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems {
        public abstract class AbstractResortableProjectorWithCost : AbstractProjectorWithProjectionList, IComparable<AbstractResortableProjectorWithCost> {
            private readonly string _name;

            protected AbstractResortableProjectorWithCost([NotNull, ItemNotNull] Projection[] orderedProjections, [CanBeNull] string name)
                : base(orderedProjections) {
                _name = name;
            }

            public override string ToString() {
                return ":" + _name;
            }

            protected abstract int Cost { get; }            

            public int CompareTo([NotNull] AbstractResortableProjectorWithCost other) {
                return Cost - other.Cost;
            }

            public static int CostOfOrderProjectionList(Projection[] orderedProjections) {
                // Average steps for each hit:

                //     1/hitCt[0] + 2/hitCt[1] + 3/hitCt[2] usw.
                // This means that (a) rarely hit and (b) "lately hit" 
                // (i.e. where the hit is towards the end of the projections
                // list) matchers are punished.
                // To keep to integer numbers, I multiply the values above with 100000;
                // and I add 1 to the hit counts to avoid division by zero. Last, I
                // wrap the sum in unchecked just for the case of more than about 20000
                // projections (10^5 * 2 * 10^4 = 2 * 10^9 =about int.MaxValue).

                int sum = 0;
                unchecked {
                    for (int i = 0; i < orderedProjections.Length; i++) {
                        sum += 100000 * (i + 1) / (orderedProjections[i].MatchCount + 1);
                    }
                }
                return sum;
            }
        }

        public abstract class AbstractSelfOptimizingProjector<TResortableProjectorWithCost> : IProjector 
            where TResortableProjectorWithCost : AbstractResortableProjectorWithCost {
            protected readonly IEqualityComparer<char> _equalityComparer;

            private readonly List<TResortableProjectorWithCost> _projectors;
            private readonly IProjector _fallBackProjector;

            private readonly int _reorganizeInterval;
            private int _stepsToReorganize;
            private Projection[] _orderedProjectionsForReducingHitCounts;

            protected AbstractSelfOptimizingProjector(Projection[] orderedProjections, bool ignoreCase,
                int reorganizeInterval) {
                _stepsToReorganize = _reorganizeInterval = reorganizeInterval;

                _equalityComparer = ignoreCase
                    ? (IEqualityComparer<char>) new CharIgnoreCaseEqualityComparer()
                    : EqualityComparer<char>.Default;

                // The following is ok if derived projectors do not initialize something in their
                // state which they use in CreateSelectingProjectors ...
                // ReSharper disable once VirtualMemberCallInConstructor 
                _projectors = CreateResortableProjectors(orderedProjections);

                _fallBackProjector = new SimpleProjector(orderedProjections);

                _orderedProjectionsForReducingHitCounts = orderedProjections;
            }

            protected abstract List<TResortableProjectorWithCost> CreateResortableProjectors(Projection[] orderedProjections);

            private void Reorganize() {
                _projectors.Sort();
                foreach (var p in _orderedProjectionsForReducingHitCounts) {
                    p.ForgetMatchCount(factor: 0.7);
                }
            }

            public Item Project(Item item, bool left) {
                if (_stepsToReorganize-- < 0) {
                    Reorganize();
                    _stepsToReorganize = _reorganizeInterval;
                }
                return (SelectProjector(_projectors, item, left) ?? _fallBackProjector).Project(item, left);
            }

            [CanBeNull]
            protected abstract TResortableProjectorWithCost SelectProjector(IEnumerable<TResortableProjectorWithCost> projectors, Item item, bool left);
        }
    }
}
