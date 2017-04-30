using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public interface IMatcher {
        bool IsMatch([NotNull] string value, [NotNull] string[] groups);

        [CanBeNull]
        string[] Matches([NotNull] string value);

        bool MatchesAlike(IMatcher other);

        /// <summary>
        /// Function used by self-organzing projectors for projections, e.g.
        /// the "SelfOptimizingProjector" inside the ProjectItems transformer.
        /// </summary>
        /// <returns>fixed known prefix matched by this matcher; or empty 
        /// string if no fixed prefix exists</returns>
        [NotNull]
        string GetKnownFixedPrefix();
        /// <summary>
        /// Function used by self-organzing projectors for projections, e.g.
        /// the "SelfOptimizingProjector" inside the ProjectItems transformer.
        /// </summary>
        /// <returns>fixed known suffix matched by this matcher; or empty 
        /// string if no fixed suffix exists</returns>
        [NotNull]
        string GetKnownFixedSufffix();
    }
}