using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming {
    public class VariableSetAndInputFileName {
        public static readonly VariableSetAndInputFileName BASE = new VariableSetAndInputFileName("", new Dictionary<string, string>(), null);

        public string Filename { get; }
        private readonly VariableSetAndInputFileName _parent;
        private readonly Dictionary<string, string> _variables;

        public VariableSetAndInputFileName(string filename, Dictionary<string, string> variables, VariableSetAndInputFileName parent) {
            Filename = filename;
            _variables = variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // clone it to make it immutable
            _parent = parent;
        }

        public override bool Equals(object obj) {
            var other = obj as VariableSetAndInputFileName;
            if (other == null
                || Filename != other.Filename
                || _variables.Count != other._variables.Count
                || !Equals(_parent, other._parent)) {
                return false;
            } else {
                foreach (var v in _variables) {
                    string otherValue;
                    if (!other._variables.TryGetValue(v.Key, out otherValue) || v.Value != otherValue) {
                        return false;
                    }
                }
                return true;
            }
        }

        public override int GetHashCode() {
            var h = Filename.GetHashCode() ^ _parent.GetHashCode();
            foreach (var kvp in _variables) {
                h ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
            }
            return h;
        }

        public string Value(string varName) {
            string result;
            return !_variables.TryGetValue(varName, out result) && _parent != null ? _parent.Value(varName) : result;
        }

        public static string ExpandVariables(string s, Dictionary<string, string> vars) {
            foreach (string key in vars.Keys.OrderByDescending(k => k.Length)) {
                // TODO: Make it better: non-word-chars at beginning and end!!!
                s = s.Replace(key, vars[key]);
            }
            return s;
        }

        internal string ExpandVariables(string s) {
            s = ExpandVariables(s, _variables);
            return _parent == null ? s : _parent.ExpandVariables(s);
        }
    }
}