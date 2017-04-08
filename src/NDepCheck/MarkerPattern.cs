using System;
using System.Collections.Generic;

namespace NDepCheck {
    public class MarkerPattern {
        public HashSet<string> Present { get; }
        public HashSet<string> Absent { get; }

        public MarkerPattern(string s, bool ignoreCase) {
            Present = new HashSet<string>(GetComparer(ignoreCase));
            Absent = new HashSet<string>(GetComparer(ignoreCase));
            string[] elements = s.Split('&');
            foreach (var e in elements) {
                string element = e.Trim();
                if (element == "~" || element == "") {
                    // ignore
                } else if (element.StartsWith("~")) {
                    Absent.Add(element.Substring(1).Trim());
                } else {
                    Present.Add(element);
                }
            }
        }

        public MarkerPattern(IEnumerable<string> present, IEnumerable<string> absent, bool ignoreCase) {
            Present = new HashSet<string>(present, GetComparer(ignoreCase));
            Absent = new HashSet<string>(absent, GetComparer(ignoreCase));
        }

        private static StringComparer GetComparer(bool ignoreCase) {
            return ignoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture;
        }

        public bool Match(ObjectWithMarkers obj) {
            return obj.Matches(this);
        }
    }
}