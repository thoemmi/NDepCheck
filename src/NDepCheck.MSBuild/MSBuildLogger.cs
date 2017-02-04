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

        public void WriteError(string msg, string fileName, int startLine) {
            _log.LogError(null, null, null, fileName, startLine, 0, 0, 0, msg);
        }

        public void WriteViolation(RuleViolation ruleViolation) {
            switch (ruleViolation.ViolationType) {
                case ViolationType.Warning:
                    _log.LogWarning(
                        null,
                        null,
                        null,
                        ruleViolation.Dependency.FileName,
                        ruleViolation.Dependency.StartLine,
                        ruleViolation.Dependency.StartColumn,
                        ruleViolation.Dependency.EndLine,
                        ruleViolation.Dependency.EndColumn,
                        ruleViolation.Dependency.QuestionableMessage());
                    break;
                case ViolationType.Error:
                    _log.LogError(
                        null,
                        null,
                        null,
                        ruleViolation.Dependency.FileName,
                        ruleViolation.Dependency.StartLine,
                        ruleViolation.Dependency.StartColumn,
                        ruleViolation.Dependency.EndLine,
                        ruleViolation.Dependency.EndColumn,
                        ruleViolation.Dependency.IllegalMessage());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void WriteWarning(string msg) {
            _log.LogWarning(msg);
        }

        public void WriteWarning(string msg, string fileName, int startLine) {
            _log.LogWarning(null, null, null, fileName, startLine, 0, 0, 0, msg);
        }

        public void WriteInfo(string msg) {
            _log.LogMessage(MessageImportance.Normal, msg);
        }

        public void WriteDebug(string msg) {
            _log.LogMessage(MessageImportance.Low, msg);
        }
    }
}