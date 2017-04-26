using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public sealed class DependencyMatch {
        [NotNull]
        private readonly MarkerPattern _markerPattern;

        private readonly bool? _ctZero;
        private readonly bool? _badCtZero;
        private readonly bool? _questionableCtZero;
        private readonly bool? _isSingleCycle;

        public DependencyMatch(string pattern, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            string ctPattern = patternParts[0];
            if (ctPattern.Contains("#")) {
                _ctZero = !ctPattern.Contains("~#");
            }
            if (ctPattern.Contains("!")) {
                _badCtZero = !ctPattern.Contains("~!");
            }
            if (ctPattern.Contains("?")) {
                _questionableCtZero = !ctPattern.Contains("~?");
            }
            if (ctPattern.Contains("=")) {
                _isSingleCycle = !ctPattern.Contains("~=");
            }
            _markerPattern = new MarkerPattern(patternParts.Length > 1 ? patternParts[1] : "", ignoreCase);
        }

        public bool IsMatch(Dependency dependency) {
            if (!_markerPattern.IsMatch(dependency)) {
                return false;
            } else if (_ctZero.HasValue && _ctZero.Value != dependency.Ct > 0) {
                return false;
            } else if (_questionableCtZero.HasValue && _questionableCtZero.Value != dependency.QuestionableCt > 0) {
                return false;
            } else if (_badCtZero.HasValue && _badCtZero.Value != dependency.BadCt > 0) {
                return false;
            } else if (_isSingleCycle.HasValue && _isSingleCycle.Value != Equals(dependency.UsingItem, dependency.UsedItem)) {
                return false;
            } else {
                return true;
            }
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