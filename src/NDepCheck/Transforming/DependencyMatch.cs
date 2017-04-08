using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public sealed class DependencyMatch {
        [NotNull]
        private readonly MarkerPattern _markerPattern;

        private readonly bool? _isSingleCycle;
        private readonly bool? _ctZero;
        private readonly bool? _badCtZero;
        private readonly bool? _questionableCtZero;

        public DependencyMatch(string pattern, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            string ctPattern = patternParts[0];
            if (ctPattern.Contains("=")) {
                _isSingleCycle = !ctPattern.Contains("~=");
            }
            if (ctPattern.Contains("#")) {
                _ctZero = !ctPattern.Contains("~#");
            }
            if (ctPattern.Contains("!")) {
                _badCtZero = !ctPattern.Contains("~!");
            }
            if (ctPattern.Contains("?")) {
                _questionableCtZero = !ctPattern.Contains("~?");
            }
            _markerPattern = new MarkerPattern(patternParts.Length > 1 ? patternParts[1] : "", ignoreCase);
        }

        public bool Match(Dependency dependency) {
            if (!_markerPattern.Match(dependency)) {
                return false;
            } else if (_isSingleCycle.HasValue && _isSingleCycle.Value != Equals(dependency.UsingItem, dependency.UsedItem)) {
                return false;
            } else if (_ctZero.HasValue && _ctZero.Value != dependency.Ct > 0) {
                return false;
            } else if (_questionableCtZero.HasValue && _questionableCtZero.Value != dependency.QuestionableCt > 0) {
                return false;
            } else if (_badCtZero.HasValue && _badCtZero.Value != dependency.BadCt > 0) {
                return false;
            } else {
                return true;
            }
        }

    }
}