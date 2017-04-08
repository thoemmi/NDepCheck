using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck {
    public class OptionAction {
        public Option Option { get; }
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

        public bool Matches(string arg) {
            return ArgMatches(arg, ShortName, Name) || ArgMatches(arg, MoreNames);
        }

        public static string CreateHelp(IEnumerable<Option> options, bool detailed) {
            var sb = new StringBuilder();
            foreach (var o in options) {
                sb.AppendLine("-" + o.Name + " or -" + o.ShortName + "   " + o.Usage);
                sb.AppendLine("    " + o.Description);
                if (o.Default == null) {
                    sb.AppendLine(o.Multiple ? "    Required; can be specified more than once" : "    Required");
                } else {
                    sb.AppendLine(o.Multiple
                        ? $"    Default: {o.Default}; can be specified more than once"
                        : $"    Default: {o.Default}");
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
                } else if (value.StartsWith("/") || value.StartsWith("-")) {
                    --i;
                    return null;
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

            HashSet<Option> requiredOptions =
                new HashSet<Option>(optionActions.Where(oa => oa.Option != null && oa.Option.Required).Select(oa => oa.Option));

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                OptionAction optionAction = optionActions.FirstOrDefault(oa => oa.Option.Matches(arg));
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
                    Throw(message, argsAsString);
                }
            }

            if (requiredOptions.Any()) {
                Throw("Missing required options: " + string.Join(", ", requiredOptions.OrderBy(o => o.Name).Select(o => o.Name)), argsAsString);
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
