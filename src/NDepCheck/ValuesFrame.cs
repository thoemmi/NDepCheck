using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck {
    public class ValuesFrame {
        [NotNull]
        private readonly Dictionary<string, string> _values;

        public ValuesFrame(Dictionary<string, string> values = null) {
            _values = values ?? new Dictionary<string, string> ();
        }

        public void SetDefine(string key, string value, string location) {
            // -dd X a+b sometimes ends (or ended?) up as -dd a+b a+b after reading expanded files; 
            // we never add such things to the dictionary.
            if (key != value) {
                if (!Regex.IsMatch(key, "^[A-Z0-9_]+$")) {
                    throw new ApplicationException($"Invalid name '{key}'");
                }

                if (_values.ContainsKey(key)) {
                    if (_values[key] != value) {
                        throw new ApplicationException($"'{key}' cannot be redefined as '{value}' {location}");
                    }
                } else {
                    _values.Add(key, value);
                }
            }
        }

        public void Clear() {
            _values.Clear();
        }

        [ContractAnnotation("s:null => null; s:notnull => notnull")]
        public string ExpandDefines([CanBeNull] string s, [CanBeNull] Dictionary<string, string> configValueCollector) {
            if (s == null) {
                return null;
            } else {
                foreach (var kvp in _values) {
                    s = Regex.Replace(s, @"\b" + kvp.Key + @"\b", m => {
                        if (configValueCollector != null) {
                            configValueCollector[kvp.Key] = kvp.Value;
                        }
                        return kvp.Value;
                    });
                }
                return s;
            }
        }

        public void ShowAllValues(string header) {
            if (_values.Any()) {
                Log.WriteInfo(header);
                foreach (var v in _values.Keys.OrderBy(k => k)) {
                    Log.WriteInfo($"-dd {v,-15} {_values[v]}");
                }
            }
        }

        public string GetValue(string valueName) {
            string result;
            _values.TryGetValue(valueName, out result);
            return result;
        }
    }
}