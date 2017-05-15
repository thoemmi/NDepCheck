using System;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public class DependencyMatch {
        [CanBeNull]
        public ItemMatch UsingMatch {
            get;
        }
        [CanBeNull]
        public DependencyPattern DependencyPattern {
            get;
        }
        [CanBeNull]
        public ItemMatch UsedMatch {
            get;
        }

        private static readonly string[] NO_STRINGS = new string[0];

        public DependencyMatch([CanBeNull] ItemMatch usingMatch,
            [CanBeNull] DependencyPattern dependencyPattern, [CanBeNull] ItemMatch usedMatch) {
            UsingMatch = usingMatch;
            DependencyPattern = dependencyPattern;
            UsedMatch = usedMatch;
        }

        public DependencyMatch(ItemType usingTypeHint, string usingPattern, string dependencyPattern, ItemType usedTypeHint, string usedPattern, bool ignoreCase) : this(
            usingPattern != "" ? new ItemMatch(usingTypeHint, usingPattern, 0, ignoreCase) : null,
            dependencyPattern != "" ? new DependencyPattern(dependencyPattern, ignoreCase) : null,
            usedPattern != "" ? new ItemMatch(usedTypeHint, usedPattern, usingPattern.Count(c => c == '('), ignoreCase) : null) {
        }

        public static DependencyMatch Create(string pattern, bool ignoreCase, string arrowTail = "->", ItemType usingTypeHint = null, ItemType usedTypeHint = null) {
            int l = pattern.IndexOf("--", StringComparison.InvariantCulture);
            string left = l < 0 ? "" : pattern.Substring(0, l);
            int r = pattern.LastIndexOf(arrowTail, StringComparison.InvariantCulture);
            string right = r < 0 || r < l ? "" : pattern.Substring(r + 2);

            int ldep = l < 0 ? 0 : l + 2;
            int rdep = r < 0 || r < l ? pattern.Length : r;

            string dep = pattern.Substring(ldep, rdep - ldep);
            return new DependencyMatch(usingTypeHint, left.Trim(), dep.Trim(), usedTypeHint, right.Trim(), ignoreCase);
        }

        public bool IsMatch<TItem>([NotNull] AbstractDependency<TItem> d) where TItem : AbstractItem<TItem> {
            MatchResult matchLeft = UsingMatch == null ? new MatchResult(true, null) : UsingMatch.Matches(d.UsingItem, NO_STRINGS);
            return matchLeft.Success
                   && (DependencyPattern == null || DependencyPattern.IsMatch(d))
                   && (UsedMatch == null || UsedMatch.Matches(d.UsedItem, matchLeft.Groups).Success);
        }

        public static readonly string DEPENDENCY_MATCH_HELP = @"
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