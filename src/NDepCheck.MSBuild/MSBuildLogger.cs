using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NDepCheck.MSBuild {
    public class MsBuildLogger : ILogger {
        private readonly TaskLoggingHelper _log;

        public MsBuildLogger(TaskLoggingHelper log) {
            _log = log;
        }

        public void WriteError(string msg) {
            _log.LogError(msg);
        }

        public void WriteError(string msg, string fileName, uint startLine) {
            _log.LogError(null, null, null, fileName, (int) startLine, 0, 0, 0, msg);
        }

        public void WriteViolation(RuleViolation ruleViolation) {
            switch (ruleViolation.ViolationType) {
                case ViolationType.Warning:
                    _log.LogWarning(
                        null,
                        null,
                        null,
                        ruleViolation.Dependency.FileName,
                        (int) ruleViolation.Dependency.StartLine,
                        (int) ruleViolation.Dependency.StartColumn,
                        (int) ruleViolation.Dependency.EndLine,
                        (int) ruleViolation.Dependency.EndColumn,
                        ruleViolation.Dependency.IllegalMessage());
                    break;
                case ViolationType.Error:
                    _log.LogError(
                        null,
                        null,
                        null,
                        ruleViolation.Dependency.FileName,
                        (int) ruleViolation.Dependency.StartLine,
                        (int) ruleViolation.Dependency.StartColumn,
                        (int) ruleViolation.Dependency.EndLine,
                        (int) ruleViolation.Dependency.EndColumn,
                        ruleViolation.Dependency.IllegalMessage());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void WriteWarning(string msg) {
            _log.LogWarning(msg);
        }

        public void WriteWarning(string msg, string fileName, uint startLine) {
            _log.LogWarning(null, null, null, fileName, (int) startLine, 0, 0, 0, msg);
        }

        public void WriteInfo(string msg) {
            _log.LogMessage(MessageImportance.Normal, msg);
        }

        public void WriteDebug(string msg) {
            _log.LogMessage(MessageImportance.Low, msg);
        }
    }
}