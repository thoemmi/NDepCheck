using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

    public class Options {
        private readonly List<DirectoryOption> _directories = new List<DirectoryOption>();
        private readonly List<InputFileOption> _itemFiles = new List<InputFileOption>();
        private readonly List<InputFileOption> _inputFiles = new List<InputFileOption>();

        /// <value>
        /// With -r, -g: If not null, show a concrete dependency for each illegal edge.
        /// With -m: Use as prefix length.
        /// </value>
        public int? StringLength;

        public bool ShowUnusedQuestionableRules { get; set; }

        public bool ShowUnusedRules { get; set; }

        public bool IgnoreCase;

        public string DefaultRuleSource { get; set; }

        public string RuleFileExtension = ".dep";

        public int MaxCpuCount { get; set; }

        public List<InputFileOption> InputFiles => _inputFiles;

        public List<DirectoryOption> Directories => _directories;

        // Internal collectors to track actions
        internal bool InputFilesSpecified { get; set; }
        internal bool GraphingDone { get; set; }
        internal bool CheckingDone { get; set; }
        public ItemType UsingItemType { get; set; } = ItemType.SIMPLE;
        public ItemType UsedItemType { get; set; } = ItemType.SIMPLE;

        public Options() {
            MaxCpuCount = 1;
        }

        public void CreateDirectoryOption(string path, bool recurse) {
            if (Directory.Exists(path)) {
                _directories.Add(new DirectoryOption(path, recurse, RuleFileExtension));
            } else {
                Log.WriteWarning("Directory " + path + " not found - ignored in dep-File");
            }
        }


        public RegexOptions GetignoreCase() {
            return IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        }

        public void CreateInputOption(string extension, string filePattern, string negativeFilePattern,
            bool readOnlyItems) {
            (readOnlyItems ? _itemFiles : _inputFiles).Add(new InputFileOption(extension, filePattern,
                negativeFilePattern));
        }

        public void Reset() {
            _directories.Clear();
            _inputFiles.Clear();

            GraphingDone = false;
            CheckingDone = false;
        }

        public AbstractDotNetAssemblyDependencyReader GetDotNetAssemblyReaderFor(string usedAssembly) {
            return FirstMatchingReader(usedAssembly, _inputFiles, false) ??
                   FirstMatchingReader(usedAssembly, _itemFiles, true);
        }

        private AbstractDotNetAssemblyDependencyReader FirstMatchingReader(string usedAssembly,
            List<InputFileOption> fileOptions, bool needsOnlyItemTails) {
            AbstractDotNetAssemblyDependencyReader result =
                fileOptions.SelectMany(i => i.CreateOrGetReaders(this, needsOnlyItemTails))
                    .OfType<AbstractDotNetAssemblyDependencyReader>()
                    .FirstOrDefault(r => r.AssemblyName == usedAssembly);
            return result;
        }

        public static bool ArgMatches(string arg, char option) {
            return arg.ToLowerInvariant().StartsWith("/" + option) || arg.ToLowerInvariant().StartsWith("-" + option);
        }

        /// <summary>
        /// Helper method to get option value of value 
        /// </summary>
        public static string ExtractOptionValue(string[] args, ref int i) {
            string optionValue;
            string arg = args[i];
            string[] argparts = arg.Split(new[] {'='}, 2);
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

        internal static void Parse([NotNull] string argsAsString, [NotNull] Action<string> simpleStringAction, params OptionAction[] optionActions) {
            if (argsAsString == null) {
                throw new ArgumentNullException(nameof(argsAsString));
            }
            if (simpleStringAction== null) {
                throw new ArgumentNullException(nameof(simpleStringAction));
            }
            if (argsAsString.StartsWith("{")) {
                string[] args =
                    argsAsString.Split(' ', '\r', '\n')
                        .Select(a => a.TrimStart('{').TrimEnd('}').Trim())
                        .Where(a => a != "")
                        .ToArray();
                for (int i = 0; i < args.Length; i++) {
                    string arg = args[i];
                    var optionAction = optionActions.FirstOrDefault(oa => ArgMatches(arg, oa.Option));
                    if (optionAction != null) {
                        i = optionAction.Action(args, i);
                    } else {
                        Throw("Invalid option " + arg, args);
                    }
                }
            } else {
                simpleStringAction(argsAsString);
            }
        }
    }
}