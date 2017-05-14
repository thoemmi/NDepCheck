using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems {
        public class FirstLetterMatchProjector : AbstractProjectorWithProjectionList, IResortableProjectorWithCost {
            public readonly Func<Item, bool, bool> Match;

            public FirstLetterMatchProjector([NotNull] Func<Item, bool, bool> match, [NotNull] Projection[] orderedProjections, [CanBeNull] string name) : base(orderedProjections, name) {
                Match = match;
            }

            public double CostPerProjection => (MatchCount + 1e-3) / (ProjectCount + 1e-9);

            public override Item Project(Item item, bool left) {
                return ProjectBySequentialSearch(item, left);
            }

            public int CompareTo(IResortableProjectorWithCost other) {
                return CostPerProjection.CompareTo(other.CostPerProjection);
            }
        }

        public class SelfOptimizingFirstLetterProjector : AbstractSelfOptimizingProjector<FirstLetterMatchProjector> {
            public SelfOptimizingFirstLetterProjector(Projection[] orderedProjections, bool ignoreCase, int reorganizeIntervalIncrement, string name) :
                base(orderedProjections, ignoreCase, reorganizeIntervalIncrement, name) {
            }

            protected override List<FirstLetterMatchProjector> CreateResortableProjectors(Projection[] orderedProjections) {
                var result = new List<FirstLetterMatchProjector>();

                for (int fieldPos = 0; ; fieldPos++) {
                    // Example: We have 6 projections, 5 with known prefxes abc, de, ab, abd, and 
                    // x, and a last catch-all projection. Obviously, for a string starting with
                    // e.g. s..., the first five projections can never match - so, we can run such
                    // a string immediately through the ** matcher!
                    //       a  d  x  [^adx]
                    // abc   +
                    // de       +
                    // abd   +
                    // ab    +
                    // x           +
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

                            result.Add(new FirstLetterMatchProjector((item, ignoreCase) =>
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
                            result.Add(new FirstLetterMatchProjector(
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

            protected override FirstLetterMatchProjector SelectProjector(IReadOnlyList<FirstLetterMatchProjector> projectors,
                                                                 Item item, bool left, int stepsToNextReorganize) {
                return projectors.FirstOrDefault(p => p.Match(item, left));
            }
        }
    }
}
