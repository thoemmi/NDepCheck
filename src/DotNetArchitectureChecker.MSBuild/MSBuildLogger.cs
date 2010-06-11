using System.Text;
using System.Web;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DotNetArchitectureChecker.MSBuild {
    public class MSBuildLogger : ILogger {
        private readonly TaskLoggingHelper _log;
        private readonly bool _generateErrorHtml;
        private readonly bool _logWarnings;
        private string _currentAssemblyFilename;
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        public MSBuildLogger(TaskLoggingHelper log, bool generateErrorHtml, bool logWarnings) {
            _log = log;
            _generateErrorHtml = generateErrorHtml;
            _logWarnings = logWarnings;
        }

        public void StartProcessingAssembly(string assemblyFilename) {
            _currentAssemblyFilename = assemblyFilename;
        }

        public void WriteError(string msg) {
            _log.LogError(msg);
            AppendErrorToHtml(msg);
        }

        public void WriteError(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            _log.LogError((string)null, (string)null, (string)null, fileName, (int)startLine, (int)startColumn, (int)endLine, (int)endColumn, msg);
            AppendErrorToHtml(Format(msg, fileName, startLine));
        }

        private void AppendErrorToHtml(string msg) {
            if (!_generateErrorHtml) {
                return;
            }
            if (!string.IsNullOrEmpty(_currentAssemblyFilename)) {
                _stringBuilder.AppendLine("<h3>" + _currentAssemblyFilename + "</h3>");
                _currentAssemblyFilename = null;
            }
            _stringBuilder.AppendLine("<span style=\"color: red;\"/>" + HttpUtility.HtmlEncode(msg) + "</span><hr center width=\"80%\">");
        }

        public void WriteWarning(string msg) {
            _log.LogWarning(msg);
        }

        public void WriteWarning(string msg, string fileName, uint startLine, uint startColumn, uint endLine, uint endColumn) {
            if (_logWarnings) {
                _log.LogWarning((string) null, (string) null, (string) null, fileName, (int) startLine, (int) startColumn, (int) endLine,
                                (int) endColumn, msg);
            }
        }

        public void WriteInfo(string msg) {
            _log.LogMessage(MessageImportance.Normal, msg);
        }

        public void WriteDebug(string msg) {
            _log.LogMessage(MessageImportance.Low, msg);
        }

        private static string Format(string msg, string fileName, uint lineNumber) {
            if (fileName != null) {
                msg += " (probably at " + fileName;
                if (lineNumber > 0)
                    msg += ":" + lineNumber;
                msg += ")";
            }
            return msg;
        }

        public string GetErrorHtml() {
            return _stringBuilder.ToString();
        }
    }
}