using System.Collections.Generic;
using System.Text;
using System.Web;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NDepCheck.MSBuild {
    public class MSBuildLogger : ILogger {
        private readonly TaskLoggingHelper _log;
        private readonly bool _generateErrorHtml;
        private readonly bool _logWarnings;
        private string _currentAssemblyFilename;
        private bool _currentAssemblyFilenameWasLogged;
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private readonly Dictionary<string,int> _assembliesWithErrors = new Dictionary<string, int>();

        public MSBuildLogger(TaskLoggingHelper log, bool generateErrorHtml, bool logWarnings) {
            _log = log;
            _generateErrorHtml = generateErrorHtml;
            _logWarnings = logWarnings;
        }

        public void StartProcessingAssembly(string assemblyFilename) {
            _currentAssemblyFilename = assemblyFilename;
            _currentAssemblyFilenameWasLogged = false;
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
            if (!_currentAssemblyFilenameWasLogged) {
                _stringBuilder.AppendLine("<h3><a name=\"" + _currentAssemblyFilename + "\">" + _currentAssemblyFilename + "</a></h3>");
                _currentAssemblyFilenameWasLogged = true;
            }
            lock (_assembliesWithErrors) {
                int errors;
                _assembliesWithErrors.TryGetValue(_currentAssemblyFilename, out errors);
                _assembliesWithErrors[_currentAssemblyFilename] = errors + 1;
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
            var summary = new StringBuilder();
            summary.AppendLine("<h2>Summary</h2>");
            if (_assembliesWithErrors.Count > 0) {
                foreach (var assembly in _assembliesWithErrors) {
                    summary.AppendFormat("<a href=\"#{0}\">{0}</a>: {1} error{2}<br/>", assembly.Key, assembly.Value, assembly.Value == 1 ? "" : "s");
                    summary.AppendLine();
                }
                summary.AppendLine("<hr center width=\"80%\" />");
            } else {
                summary.AppendLine("Congratulations, no errors detected");
            }
            return summary.ToString() + _stringBuilder.ToString();
        }
    }
}