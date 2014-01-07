namespace NDepCheck {
    public class RuleViolation {
        private readonly Dependency _dependency;
        private readonly ViolationType _violationType;

        public RuleViolation(Dependency dependency, ViolationType violationType) {
            _dependency = dependency;
            _violationType = violationType;
        }

        public Dependency Dependency {
            get { return _dependency; }
        }

        public ViolationType ViolationType {
            get { return _violationType; }
        }
    }

    public enum ViolationType {
        Warning,
        Error,
    }
}