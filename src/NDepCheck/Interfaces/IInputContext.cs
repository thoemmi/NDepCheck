using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck {
    public interface IInputContext {
        // Structure
        [NotNull]
        IEnumerable<Dependency> Dependencies { get; }

        [CanBeNull]
        DependencyRuleSet GetOrCreateDependencyRuleSetMayBeCalledInParallel([NotNull] IGlobalContext checkerContext, [NotNull] Options options, string includeRecursion);

        // Actions
        void Add(RuleViolation ruleViolation);
        [NotNull]
        IEnumerable<RuleViolation> RuleViolations { get; }

        [NotNull]
        string Filename { get; }

        int ErrorCount { get; }

        int WarningCount { get; }
    }
}