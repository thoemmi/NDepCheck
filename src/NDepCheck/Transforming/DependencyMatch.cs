using System;

namespace NDepCheck.Transforming {
    public class DependencyMatch {
        private readonly ItemMatch _usingMatch;
        private readonly DependencyPattern _dependencyPattern;
        private readonly ItemMatch _usedMatch;

        public DependencyMatch(string left, string dep, string right, bool ignoreCase) {
            _usingMatch = left != "" ? new ItemMatch(left, ignoreCase) : null;
            _dependencyPattern = dep != "" ? new DependencyPattern(dep, ignoreCase) : null;
            _usedMatch = right != "" ? new ItemMatch(right, ignoreCase) : null;
        }

        public static DependencyMatch Create(string pattern, bool ignoreCase) {
            int l = pattern.IndexOf("--", StringComparison.InvariantCulture);
            string left = l < 0 ? "" : pattern.Substring(0, l);
            int r = pattern.LastIndexOf("->", StringComparison.InvariantCulture);
            string right = r < 0 || r < l ? "" : pattern.Substring(r + 2);

            int ldep = l < 0 ? 0 : l + 2;
            int rdep = r < 0 || r < l ? pattern.Length : r;

            string dep = pattern.Substring(ldep, rdep - ldep);
            return new DependencyMatch(left.Trim(), dep.Trim(), right.Trim(), ignoreCase);
        }

        public bool IsMatch(Dependency d) {
            return ItemMatch.IsMatch(_usingMatch, d.UsingItem)
                   && (_dependencyPattern == null || _dependencyPattern.IsMatch(d))
                   && ItemMatch.IsMatch(_usedMatch, d.UsedItem);
        }

        public static readonly string DEPENDENCY_MATCH_HELP = $@"
TBD

A dependency match is a string that is matched against dependencies for various
plugins. A dependency match has the following format (unfortunately, not all
plugins follow this format as of today):
   
    [itempattern] -- [dependencypattern] -> [itempattern] 

For the format of item patterns, please see the help text for 'item'.
A dependency pattern has the following format:

    {{countpattern}} [markerpattern]

There are 8 possible count patterns:
    #   count > 0
    ~#  count = 0
    !   bad count > 0
    ~!  bad count = 0
    ?   questionable count > 0
    ~?  questionable count = 0
    =   dependency is a loop (i.e., goes from an item to itself)
    ~=  dependency is not a loop 
The count patterns are combined with a logical 'and'. For example,
    ?~!
matches all dependencies with a questionable count, but no bad count.

The marker pattern is described in the help text for 'marker'.
";
    }
}