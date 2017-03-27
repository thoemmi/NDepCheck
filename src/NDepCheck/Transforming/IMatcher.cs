using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public interface IMatcher {
        bool IsMatch([NotNull]string value, [NotNull]string[] groups);

        [CanBeNull]
        string[] Match([NotNull]string value);

        bool MatchesAlike(IMatcher other);
    }
}