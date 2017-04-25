using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck {
    public class OptionAction {
        public Option Option {
            get;
        }
        public readonly Func<string[], int, int> Action;

        public OptionAction(Option option, Func<string[], int, int> action) {
            Option = option;
            Action = action;
        }
    }

    public class Option {
        public readonly string ShortName;
        public readonly string Name;
        public readonly string Usage;
        public readonly string Description;
        public readonly bool Multiple;
        public readonly string Default;
        public readonly string[] MoreNames;
        public readonly Option OrElse;

        public Option(string shortname, string name, string usage, string description, Option orElse, bool multiple = false, string[] moreNames = null)
            : this(shortname, name, usage, description, @default: "", multiple: multiple, moreNames: moreNames) {
            OrElse = orElse; // not yet used ...
        }

        public Option(string shortname, string name, string usage, string description, bool @default, bool multiple = false, string[] moreNames = null)
            : this(shortname, name, usage, description, @default: @default ? "true" : "false", multiple: multiple, moreNames: moreNames) {
        }

        public Option(string shortname, string name, string usage, string description, string @default,
                      bool multiple = false, string[] moreNames = null) {
            ShortName = shortname;
            Name = name;
            Usage = usage;
            Description = description;
            Multiple = multiple;
            Default = @default;
            MoreNames = moreNames ?? new string[0];
        }

        public string Opt => "-" + Name;

        public bool Required => Default == null;

        public override string ToString() {
            return "/" + ShortName;
        }

        public bool IsMatch(string arg) {
            return ArgMatches(arg, ShortName, Name) || ArgMatches(arg, MoreNames);
        }

        [NotNull]
        public static string CreateHelp(IEnumerable<Option> options, bool detailed, string filter) {
            var sb = new StringBuilder();
            if (detailed) {
                sb.AppendLine();
            }
            int lineLength = 0;
            foreach (var o in options.Where(o => ("/" + o.Name + " /" + o.ShortName + " -" + o.Name + " -" + o.ShortName + " " + o.Description)
                                                 .IndexOf(filter ?? "", StringComparison.InvariantCultureIgnoreCase) >= 0)) {
                if (detailed) {
                    sb.AppendLine("-" + o.Name + " or -" + o.ShortName + "   " + o.Usage);
                    sb.AppendLine("    " + o.Description);
                    if (o.Required) {
                        sb.AppendLine(o.Multiple ? "    Required; can be specified more than once" : "    Required");
                    } else if (o.Default == "") {
                        if (o.Multiple) {
                            sb.AppendLine("    Can be specified more than once");
                        }
                    } else {
                        sb.AppendLine(o.Multiple
                            ? $"    Default: {o.Default}; can be specified more than once"
                            : $"    Default: {o.Default}");
                    }
                } else {
                    var optString = "-" + o.ShortName + " " + o.Usage;
                    lineLength += optString.Length;
                    if (lineLength > 60) {
                        sb.AppendLine();
                        lineLength = 0;
                    }
                    sb.Append(o.Required ? "  " + optString : "  [" + optString + "]");
                }
            }
            return sb.ToString();
        }

        [NotNull]
        public OptionAction Action(Func<string[], int, int> action) {
            return new OptionAction(this, action);
        }

        public static bool ArgMatches(string arg, params string[] option) {
            return option.Any(o => {
                string lower = arg.ToLowerInvariant();
                if (lower.StartsWith("/" + o) || lower.StartsWith("-" + o)) {
                    string rest = arg.Substring(1 + o.Length);
                    return rest == "" || rest.StartsWith("=");
                } else {
                    return false;
                }
            });
        }

        [NotNull]
        public static string ExtractRequiredOptionValue(string[] args, ref int i, string message) {
            string optionValue = ExtractOptionValue(args, ref i);
            if (string.IsNullOrWhiteSpace(optionValue)) {
                throw new ArgumentException(message);
            }
            return optionValue;
        }

        /// <summary>
        /// Helper method to get option value of value 
        /// </summary>
        [CanBeNull]
        public static string ExtractOptionValue(string[] args, ref int i, bool allowOptionValue = false) {
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
            } else if (!allowOptionValue && LooksLikeAnOption(optionValue)) {
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

        private static bool LooksLikeAnOption(string s) {
            return s.Length > 1
                   && (s.StartsWith("-")
                       || s.StartsWith("/") && !s.Substring(1).Contains("/") // this allows some paths with / as option value
                   );
        }

        public static int ExtractIntOptionValue(string[] args, ref int j, string msg) {
            int value;
            if (!int.TryParse(ExtractOptionValue(args, ref j), out value)) {
                ThrowArgumentException(msg, string.Join(" ", args));
            }
            return value;
        }

        [NotNull]
        private static string CollectMultipleArgs(string[] args, ref int i, string value) {
            // Collect everything up to }
            var sb = new StringBuilder();
            var valueArgs = value.Split(' ', '\t', '\r', '\n').Where(s => !string.IsNullOrWhiteSpace(s));
            foreach (var v in valueArgs) {
                sb.AppendLine(v);
            }

            while (!value.EndsWith("}")) {
                if (i >= args.Length - 1) {
                    throw new ArgumentException("Missing } at end of options");
                }
                value = args[++i]; // TODO: Also do split???
                sb.AppendLine(value);
            }
            return sb.ToString();
        }

        [CanBeNull]
        public static string ExtractNextValue(string[] args, ref int i, bool allowOptionValue = false) {
            if (i >= args.Length - 1) {
                return null;
            } else {
                string value = args[++i];
                if (value.StartsWith("{")) {
                    return CollectMultipleArgs(args, ref i, value);
                } else if (!allowOptionValue && LooksLikeAnOption(value)) {
                    --i;
                    return null;
                } else {
                    return value;
                }
            }
        }

        internal static void ThrowArgumentException(string message, string argsAsString) {
            throw new ArgumentException(message + " (provided options: " + (argsAsString.Length > 305 ? argsAsString.Substring(0, 300) + "..." : argsAsString) + ")");
        }

        internal static void Parse([NotNull] GlobalContext globalContext, [CanBeNull] string argsAsString, params OptionAction[] optionActions) {
            string[] args;
            if (string.IsNullOrWhiteSpace(argsAsString)) {
                args = new string[0];
            } else if (argsAsString.Trim().StartsWith("{")) {
                var list = new List<string>();
                using (var sr = new StringReader(argsAsString.TrimStart('{', ' ', '\t', '\r', '\n').TrimEnd('}', ' ', '\t', '\r', '\n'))) {
                    for (;;) {
                        string line = sr.ReadLine();
                        if (line == null) {
                            break;
                        }
                        if (line == "") {
                            // ignore;
                        } else {
                            list.Add(line);
                        }
                    }
                }
                args = list.ToArray();
            } else {
                args = new[] { argsAsString };
            }

            HashSet<Option> requiredOptions =
                new HashSet<Option>(optionActions.Where(oa => oa.Option != null && oa.Option.Required).Select(oa => oa.Option));

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                OptionAction optionAction = optionActions.FirstOrDefault(oa => oa.Option.IsMatch(arg));
                if (optionAction != null) {
                    requiredOptions.Remove(optionAction.Option);
                    i = optionAction.Action(args, i);
                    if (i == int.MaxValue) {
                        break;
                    }
                } else {
                    string message;
                    if (arg.Count(c => c == '/' || c == '-') > 1) {
                        message = "Invalid option " + arg + ", maybe {...} missing";
                    } else {
                        message = "Invalid option " + arg;
                    }
                    message += "\r\nAllowed options: " +
                               CreateHelp(optionActions.Select(oa => oa.Option), detailed: false, filter: "");
                    ThrowArgumentException(message, argsAsString);
                }
            }

            if (requiredOptions.Any()) {
                ThrowArgumentException("Missing required options: " + string.Join(", ", requiredOptions.OrderBy(o => o.Name).Select(o => o.Name)), argsAsString);
            }
        }

        [NotNull, ItemNotNull]
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
                if (!Directory.Exists(dir)) {
                    throw new IOException($"Directory '{dir}' does not exist (current directory is '{Environment.CurrentDirectory}')");
                }

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
