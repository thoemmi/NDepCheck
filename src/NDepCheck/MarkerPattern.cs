using System.Collections.Generic;

namespace NDepCheck {
    public class MarkerPattern {
        public HashSet<string> Present { get; }
        public HashSet<string> Absent { get; }

        public MarkerPattern(string s, bool ignoreCase) {
            Present = new HashSet<string>(ObjectWithMarkers.GetComparer(ignoreCase));
            Absent = new HashSet<string>(ObjectWithMarkers.GetComparer(ignoreCase));
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
            Present = new HashSet<string>(present, ObjectWithMarkers.GetComparer(ignoreCase));
            Absent = new HashSet<string>(absent, ObjectWithMarkers.GetComparer(ignoreCase));
        }

        public bool IsMatch(ObjectWithMarkers obj) {
            return obj.IsMatch(this);
        }
    }
}