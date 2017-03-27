using System;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck {
    public class OptionAction {
        public readonly char Option;
        public readonly Func<string[], int, int> Action;

        public OptionAction(char option, Func<string[], int, int> action) {
            Option = option;
            Action = action;
        }
    }

    public static class Options {
        public static bool ArgMatches(string arg, params char[] option) {
            return option.Any(o => arg.ToLowerInvariant().StartsWith("/" + o) || arg.ToLowerInvariant().StartsWith("-" + o));
        }

        /// <summary>
        /// Helper method to get option value of value 
        /// </summary>
        public static string ExtractOptionValue(string[] args, ref int i) {
            string optionValue;
            string arg = args[i];
            string[] argparts = arg.Split(new[] { '=' }, 2);
            if (argparts.Length > 1) {
                // /#=value ==> optionValue: "value"
                optionValue = argparts[1];
            } else if (i < args.Length - 1 && (arg.StartsWith("/") || arg.StartsWith("-"))) {
                // /# value ==> optionValue: "value"
                // -# value ==> optionValue: "value"
                optionValue = args[++i];
            } else if (arg.Length <= 2) {
                // /# ==> optionValue: null
                optionValue = null;
            } else if (arg[2] == '=') {
                // /#= value ==> optionValue: "value"
                // /#=value ==> optionValue: "value" // AGAIN?
                optionValue = arg.Length == 3 ? args[++i] : arg.Substring(3);
            } else {
                // /# value ==> optionValue: "value"
                // /#value ==> optionValue: "value" // AGAIN?
                optionValue = arg.Length == 2 ? args[++i] : arg.Substring(2);
            }

            if (optionValue != null && optionValue.StartsWith("{")) {
                return CollectMultipleArgs(args, ref i, optionValue);
            } else {
                return optionValue;
            }
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
                var optionAction = optionActions.FirstOrDefault(oa => ArgMatches(arg, oa.Option));
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
    }
}
