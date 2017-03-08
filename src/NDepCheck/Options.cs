using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NDepCheck {
    public class Options {
        private readonly List<DirectoryOption> _directories = new List<DirectoryOption>();
        private readonly List<InputFileOption> _itemFiles = new List<InputFileOption>();
        private readonly List<InputFileOption> _inputFiles = new List<InputFileOption>();

        /////// <summary>
        /////// Set output file name. If set to <c>null</c> (or left
        /////// at <c>null</c>), no DOT output is created.
        /////// </summary>
        /////// <value>The dot filename.</value>
        ////public string DotFilename { get; set; }

        ////public readonly List<string> GraphTransformations = new List<string>();

        /// <value>
        /// With /g: If not null, show a concrete dependency for each illegal edge.
        /// With /m: Use as prefix length.
        /// </value>
        public int? StringLength;

        public bool ShowUnusedQuestionableRules { get; set; }

        public bool ShowUnusedRules { get; set; }

        public bool IgnoreCase;

        public string DefaultRuleSetFile { get; set; }

        public string RuleFileExtension = ".dep";

        public int MaxCpuCount { get; set; }

        public List<InputFileOption> InputFiles => _inputFiles;

        public List<DirectoryOption> Directories => _directories;

        // Internal collectors to track actions
        internal bool InputFilesSpecified { get; set; }
        internal bool GraphingDone { get; set; }
        internal bool CheckingDone { get; set; }

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

        public void CreateInputOption(string extension, string filePattern, string negativeFilePattern, bool readOnlyItems) {
            (readOnlyItems ? _itemFiles : _inputFiles).Add(new InputFileOption(extension, filePattern, negativeFilePattern));
        }



        public void Reset() {
            _directories.Clear();
            DefaultRuleSetFile = null;

            InputFilesSpecified = false;
            GraphingDone = false;
            CheckingDone = false;
        }

        public AbstractDotNetAssemblyDependencyReader GetDotNetAssemblyReaderFor(string usedAssembly) {
            return FirstMatchingReader(usedAssembly, _inputFiles, false) ?? FirstMatchingReader(usedAssembly, _itemFiles, true);
        }

        private AbstractDotNetAssemblyDependencyReader FirstMatchingReader(string usedAssembly, List<InputFileOption> fileOptions, bool needsOnlyItemTails) {
            AbstractDotNetAssemblyDependencyReader result = fileOptions
                .SelectMany(i => i.CreateOrGetReaders(this, needsOnlyItemTails))
                .OfType<AbstractDotNetAssemblyDependencyReader>()
                .FirstOrDefault(r => r.AssemblyName == usedAssembly);
            return result;
        }
    }
}