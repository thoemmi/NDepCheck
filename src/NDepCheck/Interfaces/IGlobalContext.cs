using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck {
    public interface IGlobalContext {
        [CanBeNull]
        DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel([NotNull]Options options, [NotNull]string dependencyFilename, [NotNull] string fileIncludeStack);

        [CanBeNull]
        DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel([NotNull]DirectoryInfo relativeRoot, [NotNull]string ruleSource,
            [NotNull]Options options, [NotNull]IDictionary<string, string> defines, [NotNull]IDictionary<string, Macro> macros, bool ignoreCase, [NotNull] string fileIncludeStack);

        int Run([NotNull] string[] args, Options options);
    }
}
