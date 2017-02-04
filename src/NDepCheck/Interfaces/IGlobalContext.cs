using System.Collections.Generic;
using System.IO;

namespace NDepCheck {
    public interface IGlobalContext {
        DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel(Options options, string dependencyFilename);
        DependencyRuleSet GetOrCreateDependencyRuleSet_MayBeCalledInParallel(DirectoryInfo relativeRoot, string rulefilename,
            Options options, IDictionary<string, string> defines, IDictionary<string, Macro> macros, bool ignoreCase);



        int Run(string[] args);
    }
}
