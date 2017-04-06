using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck {
    public class OptionAction {
        private readonly string _oldOption;
        private readonly Option _newOption;
        public readonly Func<string[], int, int> Action;

        public OptionAction(Option option, Func<string[], int, int> action) {
            _newOption = option;
            Action = action;
        }

        public OptionAction(string option, Func<string[], int, int> action) {
            _oldOption = option;
            Action = action;
        }

        public bool ArgMatches(string arg) {
            return _oldOption == null ? _newOption.Matches(arg) : Option.ArgMatches(arg, _oldOption);
        }
    }

    public class Option {
        public readonly string ShortName;
        public readonly string Name;
        public readonly string Usage;
        public readonly string Description;
        public readonly bool Multiple;
        public readonly string[] MoreNames;

        public Option(string shortname, string name, string usage, string description, bool multiple = false, string[] moreNames = null) {
            ShortName = shortname;
            Name = name;
            Usage = usage;
            Description = description;
            Multiple = multiple;
            MoreNames = moreNames ?? new string[0];
        }

        public string Opt => "-" + Name;

        public override string ToString() {
            return "/" + ShortName;
        }

        public bool Matches(string arg) {
            return ArgMatches(arg, ShortName, Name) || ArgMatches(arg, MoreNames);
        }

        public static string CreateHelp(Option[] options, bool detailed) {
            var sb = new StringBuilder();
            foreach (var o in options) {
                sb.AppendLine("-"+ o.Name + " or -" + o.ShortName + "   " + o.Usage);
                sb.AppendLine("    " + o.Description);
                if (o.Multiple) {
                    sb.AppendLine("    Can be specified more than once");
                }
            }
            return sb.ToString();
        }

        public OptionAction Action(Func<string[], int, int> action) {
            return new OptionAction(this, action);
        }

        public static bool ArgMatches(string arg, params string[] option) {
            return option.Any(o => {
                string lower = arg.ToLowerInvariant();
                if (!lower.StartsWith("/" + o) && !lower.StartsWith("-" + o)) {
                    return false;
                } else {
                    string rest = arg.Substring(1 + o.Length);
                    return rest == "" || rest.StartsWith("=");
                }
            });
        }

        /// <summary>
        /// Helper method to get option value of value 
        /// </summary>
        public static string ExtractOptionValue(string[] args, ref int i) {
            string optionValue;
            string arg = args[i];
            string[] argparts = arg.Split(new[] { '=' }, 2);
            var nextI = i;
            if (argparts.Length > 1 && argparts[1] != "") {
                // /#=value ==> optionValue: "value"
                optionValue = argparts[1];
            } else {
                // /# value ==> optionValue: "value"
                // -# value ==> optionValue: "value"
                // /#= value ==> optionValue: "value"
                // /#= value ==> optionValue: "value"
                optionValue = nextI + 1 >= args.Length ? null : args[++nextI];
            }

            if (optionValue == null) {
                i = nextI;
                return null;
            } else if ((optionValue.StartsWith("/") || optionValue.StartsWith("-")) && optionValue.Length > 1) {
                // This is the following option - i is not changed
                return null;
            } else if (optionValue.StartsWith("{")) {
                i = nextI;
                return CollectMultipleArgs(args, ref i, optionValue);
            } else {
                i = nextI;
                return optionValue;
            }
        }

        public static int ExtractIntOptionValue(string[] args, ref int j, string msg) {
            int value;
            if (!int.TryParse(ExtractOptionValue(args, ref j), out value)) {
                Throw(msg, args);
            }
            return value;
        }

        private static string CollectMultipleArgs(string[] args, ref int i, string value) {
            // Collect everything up to }
            var sb = new StringBuilder(value);
            while (!value.EndsWith("}")) {
                if (i >= args.Length - 1) {
                    throw new ArgumentException("Missing } at end of options");
                }
                value = args[++i];
                sb.AppendLine(value);
            }
            return sb.ToString();
        }

        public static string ExtractNextValue(string[] args, ref int i) {
            if (i >= args.Length - 1) {
                return null;
            } else {
                string value = args[++i];
                if (value.StartsWith("{")) {
                    return CollectMultipleArgs(args, ref i, value);
                } else {
                    return value;
                }
            }
        }

        internal static void Throw(string message, string[] args) {
            Throw(message, string.Join(" ", args));
        }

        internal static void Throw(string message, string argsAsString) {
            throw new ArgumentException(message + " (provided options: " + argsAsString + ")");
        }

        internal static void Parse([NotNull] string argsAsString, params OptionAction[] optionActions) {
            if (argsAsString == null) {
                throw new ArgumentNullException(nameof(argsAsString));
            }
            string[] args;
            if (argsAsString.StartsWith("{")) {
                args =
                    argsAsString.Split(' ', '\r', '\n')
                        .Select(a => a.TrimStart('{').TrimEnd('}').Trim())
                        .Where(a => a != "")
                        .ToArray();
            } else if (argsAsString == "") {
                args = new string[0];
            } else {
                args = new[] { argsAsString };
            }

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                var optionAction = optionActions.FirstOrDefault(oa => oa.ArgMatches(arg));
                if (optionAction != null) {
                    i = optionAction.Action(args, i);
                    if (i == int.MaxValue) {
                        break;
                    }
                } else {
                    Throw("Invalid option " + arg, args);
                }
            }
        }

        public static IEnumerable<string> ExpandFilename(string pattern, params string[] extensions) {
            if (pattern.StartsWith("@")) {
                using (TextReader nameFile = new StreamReader(pattern.Substring(1))) {
                    for (;;) {
                        string name = nameFile.ReadLine();
                        if (name == null) {
                            break;
                        }
                        name = name.Trim();
                        if (name != "") {
                            yield return name;
                        }
                    }
                }
            } else if (pattern.Contains("*") || pattern.Contains("?")) {
                int sepPos = pattern.LastIndexOf(Path.DirectorySeparatorChar);

                string dir = sepPos < 0 ? "." : pattern.Substring(0, sepPos);
                string filePattern = sepPos < 0 ? pattern : pattern.Substring(sepPos + 1);
                foreach (string name in Directory.GetFiles(dir, filePattern)) {
                    yield return name;
                }
            } else if (Directory.Exists(pattern)) {
                foreach (var ext in extensions) {
                    foreach (string name in Directory.GetFiles(pattern, "*" + ext)) {
                        yield return name;
                    }
                }
            } else {
                yield return pattern;
            }
        }
    }
}
