using JetBrains.Annotations;

namespace NDepCheck {
    public class RuleViolation {
        [NotNull]
        public Dependency Dependency {
            get;
        }

        public ViolationType ViolationType {
            get;
        }

        public RuleViolation([NotNull]Dependency dependency, ViolationType violationType) {
            Dependency = dependency;
            ViolationType = violationType;
        }
    }

    public enum ViolationType {
        Warning,
        Error,
    }
}