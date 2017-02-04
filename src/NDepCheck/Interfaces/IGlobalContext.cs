using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck {
    public interface IGlobalContext {
        [CanBeNull]
        DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel([NotNull]Options options, [NotNull]string dependencyFilename, [NotNull] string includeRecursion);

        [CanBeNull]
        DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel([NotNull]DirectoryInfo relativeRoot, [NotNull]string rulefilename,
            [NotNull]Options options, [NotNull]IDictionary<string, string> defines, [NotNull]IDictionary<string, Macro> macros, bool ignoreCase,
            [NotNull] string includeRecursion);

        int Run([NotNull] string[] args);
    }
}
