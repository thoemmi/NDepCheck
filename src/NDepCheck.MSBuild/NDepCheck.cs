using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NDepCheck.MSBuild {
    public class NDepCheck : Task {
        public string DotFilename { get; set; }
        public bool ShowTransitiveEdges { get; set; }
        public bool ShowUnusedQuestionableRules { get; set; }
        public ITaskItem DefaultRuleSet { get; set; }
        public int MaxCpuCount { get; set; }
        public bool Debug { get; set; }
        public bool Verbose { get; set; }
        public string XmlOutput { get; set; }
        public bool CheckOnlyAssemblyDependencies { get; set; }

        public NDepCheck() {
            throw new NotImplementedException("Options below are no longer valid");
        }

        [Required]
        public ITaskItem[] Assemblies { get; set; }

        public ITaskItem[] Directories { get; set; }

        [Output]
        public int ExitCode { get; set; }

        public override bool Execute() {
            var logger = new MsBuildLogger(Log);
            global::NDepCheck.Log.Logger = logger;
            global::NDepCheck.Log.SetLevel(
                Verbose ? global::NDepCheck.Log.Level.Verbose : global::NDepCheck.Log.Level.Standard);

            var args = new List<string>();
            if (Verbose) {
                args.Add("/v");
            }

            if (ShowUnusedQuestionableRules) {
                args.Add("/q");
            }

            args.Add("/n " + (MaxCpuCount == 0 || MaxCpuCount < -1 ? Environment.ProcessorCount : MaxCpuCount));

            if (DefaultRuleSet != null) {
                args.Add("/x " + DefaultRuleSet.ItemSpec);
            }

            //Directories?.Select(GetDirectoryOptionFromTaskItem).AddTo(options.Directories);

            var program = new Program();
            ExitCode = program.Run(args.ToArray(), new string[0], new GlobalContext(), null, logCommands: false); 

            return ExitCode == 0;
        }

        //private static DirectoryOption GetDirectoryOptionFromTaskItem(ITaskItem taskItem) {
        //    bool recursive = true;
        //    bool tmp;
        //    var recursiveString = taskItem.GetMetadata("Recursive");
        //    if (!string.IsNullOrEmpty(recursiveString) && Boolean.TryParse(recursiveString, out tmp)) {
        //        recursive = tmp;
        //    }
        //    return new DirectoryOption(taskItem.ItemSpec, recursive, ".dep");
        //}
    }
}