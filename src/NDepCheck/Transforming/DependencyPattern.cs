using System;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public sealed class DependencyPattern {
        [NotNull]
        private readonly MarkerPattern _markerPattern;

        private readonly bool? _ctZero;
        private readonly bool? _badCtZero;
        private readonly bool? _questionableCtZero;
        private readonly bool? _isSingleCycle;

        public DependencyPattern(string pattern, bool ignoreCase) {
            string[] patternParts = pattern.Split('\'');
            string ctPattern = patternParts[0];
            if (Regex.IsMatch(ctPattern, "[^~#!?=]")) {
                throw new ArgumentException($"Count pattern can only contain #, ~#, !, ~!, ?, ~? , = and ~=, but is '{ctPattern}'; maybe a ' is missing before the marker pattern");
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
   }
}