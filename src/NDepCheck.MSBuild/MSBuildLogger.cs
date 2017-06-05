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

        public void WriteError(string msg, string nestedFilenames, int startLine) {
            _log.LogError(null, null, null, nestedFilenames, startLine, 0, 0, 0, msg);
        }

        public void WriteViolation(Dependency dependency, bool simpleRuleOutput) {
            if (dependency.BadCt > 0) {
                _log.LogError(
                    "Bad dependency",
                    null,
                    null,
                    dependency.Source,
                    dependency.NotOkMessage(simpleRuleOutput: simpleRuleOutput, newLine: false));
            } else if (dependency.QuestionableCt > 0) {
                _log.LogWarning(
                    "Questionable dependency",
                    null,
                    null,
                    dependency.Source,
                    dependency.NotOkMessage(simpleRuleOutput: simpleRuleOutput, newLine: false));
            }
        }

        public void WriteWarning(string msg) {
            _log.LogWarning(msg);
        }

        public void WriteWarning(string msg, string nestedFilenames, int startLine) {
            _log.LogWarning(null, null, null, nestedFilenames, startLine, 0, 0, 0, msg);
        }

        public void WriteInfo(string msg) {
            _log.LogMessage(MessageImportance.Normal, msg);
        }

        public void WriteDebug(string msg) {
            _log.LogMessage(MessageImportance.Low, msg);
        }
    }
}