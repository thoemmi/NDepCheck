using JetBrains.Annotations;

namespace NDepCheck {
    public class RuleViolation {
        [NotNull]
        public Dependency Dependency {
            get;
        }

        public DependencyCheckResult ViolationType {
            get;
        }

        public RuleViolation([NotNull]Dependency dependency, DependencyCheckResult violationType) {
            Dependency = dependency;
            ViolationType = violationType;
        }
    }
}