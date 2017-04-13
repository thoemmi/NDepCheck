using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems {
        private abstract class AbstractProjectorWithProjectionList : IProjector {
            protected readonly Projection[] _orderedProjections;

            protected AbstractProjectorWithProjectionList(Projection[] orderedProjections) {
                _orderedProjections = orderedProjections;
            }

            public Item Project(Item item, bool left) {
                foreach (var p in _orderedProjections) {
                    Item result = p.Match(item, left);
                    if (result != null) {
                        p.IncreaseHitCount();
                        return result;
                    }
                }
                return null;
            }
        }

        private class SimpleProjector : AbstractProjectorWithProjectionList {
            public SimpleProjector(Projection[] orderedProjections) : base(orderedProjections) {
            }
        }

        private abstract class SelfOptimizingProjector : IProjector {
            protected readonly IEqualityComparer<char> _equalityComparer;

            protected class SelectingProjector : AbstractProjectorWithProjectionList, IComparable<SelectingProjector> {
                public readonly Func<Item, bool, bool> Match;
                private readonly string _name;

                public SelectingProjector(Func<Item, bool, bool> match, Projection[] orderedProjections, string name) : base(orderedProjections) {
                    Match = match;
                    _name = name;
                }

                public override string ToString() {
                    return ":" + _name;
                }

                private int Cost {
                    get {
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
                            for (int i = 0; i < _orderedProjections.Length; i++) {
                                sum += 100000 * (i + 1) / (_orderedProjections[i].HitCt + 1);
                            }
                        }
                        return sum;
                    }
                }

                public int CompareTo(SelectingProjector other) {
                    return Cost - other.Cost;
                }
            }

            private class CharIgnoreCaseEqualityComparer : IEqualityComparer<char> {
                public bool Equals(char x, char y) {
                    return char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
                }

                public int GetHashCode(char obj) {
                    return char.ToUpperInvariant(obj).GetHashCode();
                }
            }

            private readonly List<SelectingProjector> _projectors;
            private readonly IProjector _fullProjector;
            private readonly int _reorganizeInterval;
            private int _stepsToReorganize;

            protected SelfOptimizingProjector(Projection[] orderedProjections, bool ignoreCase, int reorganizeInterval) {
                _stepsToReorganize = _reorganizeInterval = reorganizeInterval;

                _fullProjector = new SimpleProjector(orderedProjections);
                _equalityComparer = ignoreCase
                    ? (IEqualityComparer<char>)new CharIgnoreCaseEqualityComparer()
                    : EqualityComparer<char>.Default;

                // The following is ok if derived projectors do not initialize something in their
                // state which they use in CreateSelectingProjectors ...
                // ReSharper disable once VirtualMemberCallInConstructor 
                _projectors = CreateSelectingProjectors(orderedProjections);
            }

            protected abstract List<SelectingProjector> CreateSelectingProjectors(Projection[] orderedProjections);

            private void Reorganize() {
                _projectors.Sort();
            }

            public Item Project(Item item, bool left) {
                if (_stepsToReorganize-- < 0) {
                    Reorganize();
                    _stepsToReorganize = _reorganizeInterval;
                }
                IProjector selector = _projectors.FirstOrDefault(p => p.Match(item, left)) ?? _fullProjector;
                return selector.Project(item, left);
            }
        }

        private class SelfOptimizingFirstLetterProjector : SelfOptimizingProjector {
            public SelfOptimizingFirstLetterProjector(Projection[] orderedProjections, bool ignoreCase, int reorganizeInterval) :
                base(orderedProjections, ignoreCase, reorganizeInterval) {
            }

            protected override List<SelectingProjector> CreateSelectingProjectors(Projection[] orderedProjections) {
                var result = new List<SelectingProjector>();

                for (int fieldPos = 0; ; fieldPos++) {
                    // Example: We have 5 projections, 4 with known prefxes abc, def, ab, and xyz,
                    // and a last catch-all projection. Obviously, for a string starting with
                    // e.g. s..., the first four projections can never match - so, we can run
                    // such a string immediately through the ** matcher!
                    //       a  d  x  [^adx]
                    // abc   +
                    // def      +
                    // ab    +
                    // xyz         +
                    // **    +  +     +

                    int fieldPos0 = fieldPos;
                    var matchersAtFieldPos = orderedProjections
                            .Select(p => new {
                                Matcher = p.ItemMatch.ItemPattern.Matchers.ElementAtOrDefault(fieldPos0),
                                Projection = p
                            })
                            .ToArray();

                    if (matchersAtFieldPos.All(pm => pm.Matcher == null)) {
                        break;
                    }

                    HashSet<char> allFirstLetters = new HashSet<char>(
                        matchersAtFieldPos
                            .Select(pm => pm?.Matcher?.GetKnownFixedPrefix().ElementAtOrDefault(0) ?? default(char))
                            .Where(c => c != default(char)),
                        _equalityComparer);

                    if (allFirstLetters.Any()) {
                        foreach (var firstLetter in allFirstLetters) {
                            // Create a SelectingProjector that looks only at matchers that might match this letter
                            Projection[] projectionsWhoseMatcherStartsWithFirstLetter =
                                matchersAtFieldPos.Where(mp =>
                                        mp.Matcher.GetKnownFixedPrefix() == ""
                                        || _equalityComparer.Equals(mp.Matcher.GetKnownFixedPrefix()[0], firstLetter))
                                    .Select(mp => mp.Projection)
                                    .ToArray();

                            result.Add(new SelectingProjector((item, ignoreCase) =>
                                            _equalityComparer.Equals(firstLetter, item.Values[fieldPos0].ElementAtOrDefault(0)),
                                            projectionsWhoseMatcherStartsWithFirstLetter,
                                            "=" + firstLetter));
                        }
                        {
                            // Create a matcher that collects all strings that definitely will NOT match
                            // any of the known prefixes of other matchers. This is the actual benefit of this
                            // matcher - in other words, we could just add this single matcher and should
                            // already be much better thatn brute force sequential comparison!
                            Projection[] projectionsWhoseMatcherDoesNotStartWithAnyFirstLetters =
                                matchersAtFieldPos.Where(
                                        mp => mp.Matcher.GetKnownFixedPrefix() == "" ||
                                              !allFirstLetters.Contains(mp.Matcher.GetKnownFixedPrefix()[0]))
                                    .Select(mp => mp.Projection)
                                    .ToArray();
                            result.Add(new SelectingProjector(
                                    (item, ignoreCase) =>
                                        // Here is the crucial condition which allows this matcher to
                                        // move towards the front end of the projectors' list in a
                                        // SelfOptimizingProjector.
                                        !allFirstLetters.Contains(item.Values[fieldPos0].ElementAtOrDefault(0)),
                                    projectionsWhoseMatcherDoesNotStartWithAnyFirstLetters,
                                    "#" + string.Join("", allFirstLetters)
                                ));
                        }
                    }
                }

                return result;
            }
        }

        //    private class SelfOptimizingPrefixProjector : SelfOptimizingProjector {
        //        public SelfOptimizingPrefixProjector(Projection[] orderedProjections, int reorganizeInterval = 1000) :
        //            base(orderedProjections, reorganizeInterval) {
        //        }

        //        protected override List<SelectingProjector> CreateSelectingProjectors(Projection[] orderedProjections) {
        //            //       abc def xyz ab[^c] [^ab|^def|^xyz]
        //            // abc   +
        //            // def       +
        //            // ab    +           +
        //            // xyz           +
        //            // **    +   +   +   +      +

        //throw new NotImplementedException();
        //        }
        //    }
    }
}
