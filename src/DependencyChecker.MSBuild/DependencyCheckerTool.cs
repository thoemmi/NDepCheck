using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DependencyChecker.MSBuild {
    public class DependencyCheckerTool : Task, ILogger {
        public bool Debug { get; set; }
        public string DotFile { get; set; }
        public int MaxInvalidEdgeStringLength { get; set; }
        public bool RememberLastCheckDate { get; set; }
        public bool ShowInvalidEdges { get; set; }
        public bool ShowTransitiveEdges { get; set; }
        public bool Verbose { get; set; }

        [Required]
        public ITaskItem[] RuleFiles { get; set; }

        [Required]
        public ITaskItem[] Assemblies { get; set; }

        [Output]
        public string ExitCode { get; set; }

        [Output]
        public string Error { get; set; }

        [Output]
        public string ErrorHTML {
            get {
                return Error.Replace(Environment.NewLine, "<hr center width=80%>" + Environment.NewLine);
            }
        }

        public DependencyCheckerTool() {
            MaxInvalidEdgeStringLength = -1;
        }

        #region ILogger Members

        public void WriteError(string msg) {
            Log.LogError(msg);
            Error += msg + Environment.NewLine;
        }

        public void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            //WriteError(Format(msg, fileName, lineNumber));
            Log.LogError(null, null, null, fileName, (int)startLine, (int)startColumn, (int)endLine, (int)endColumn, msg, new object[0]);
            Error += Format(msg, fileName, startLine) + Environment.NewLine;
        }

        public void WriteWarning(string msg) {
            Log.LogWarning(msg);
        }

        public void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            //WriteWarning(Format(msg, fileName, lineNumber));
            Log.LogWarning(null, null, null, fileName, (int)startLine, (int)startColumn, (int)endLine, (int)endColumn, msg, new object[0]);
        }

        public void WriteInfo(string msg) {
            Log.LogMessage(MessageImportance.Normal, msg);
        }

        public void WriteDebug(string msg) {
            Log.LogMessage(MessageImportance.Low, msg);
        }

        private static string Format(string msg, string fileName, uint lineNumber) {
            if (fileName != null) {
                msg += "(probably at " + fileName;
                if (lineNumber > 0)
                    msg += ":" + lineNumber;
                msg += ")";
            }
            return msg;
        }
        #endregion

        public override bool Execute() {
            Error = string.Empty;
            //System.Diagnostics.Debugger.Launch();
            var args = RuleFiles.Select(taskItem => "-f" + taskItem.ItemSpec).ToList();
            if (Verbose) {
                args.Add("-v");
            }
            if (Debug) {
                args.Add("-d");
            }
            if (RememberLastCheckDate) {
                args.Add("-r");
            }
            if (!String.IsNullOrEmpty(DotFile)) {
                args.Add("-d" + DotFile);
            }
            if (ShowTransitiveEdges) {
                args.Add("-t");
            }
            if (ShowInvalidEdges) {
                if (MaxInvalidEdgeStringLength > 0) {
                    args.Add("-i" + MaxInvalidEdgeStringLength);
                } else {
                    args.Add("-i");
                }
            }
            args.AddRange(Assemblies.Select(taskItem => taskItem.ItemSpec));

            DependencyChecker.Logger = this;
            var checker = new DependencyChecker();

            Log.LogMessage(MessageImportance.Low, "Calling DependencyChecker with this arguments: " + String.Join(" ", args.ToArray()));
            int retwert = checker.Run(args.ToArray());
            ExitCode = retwert.ToString();
            return true;
        }
    }
}