using JetBrains.Annotations;

namespace NDepCheck {
    public class RuleViolation {
        [NotNull]
        private readonly Dependency _dependency;
        private readonly ViolationType _violationType;

        public RuleViolation([NotNull]Dependency dependency, ViolationType violationType) {
            _dependency = dependency;
            _violationType = violationType;
        }

        [NotNull]
        public Dependency Dependency => _dependency;

        public ViolationType ViolationType => _violationType;
    }

    public enum ViolationType {
        Warning,
        Error,
    }
}