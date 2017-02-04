using System.Collections.Generic;

namespace NDepCheck {
    public interface IInputContext {
        // Structure
        IEnumerable<Dependency> Dependencies { get; }
        DependencyRuleSet GetOrCreateDependencyRuleSetMayBeCalledInParallel(IGlobalContext checkerContext, Options options);

        // Actions
        void Add(RuleViolation ruleViolation);
        IEnumerable<RuleViolation> RuleViolations { get; }
        string Filename { get; }
        int ErrorCount { get; }
        int WarningCount { get; }
    }
}