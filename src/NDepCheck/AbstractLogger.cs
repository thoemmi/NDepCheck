using System.Text;

namespace NDepCheck {
    public abstract class AbstractLogger : ILogger {
        public bool HasWarningsOrErrors { get; protected set; }

        protected abstract void DoWriteError(string msg);

        public virtual void WriteError(string msg) {
            HasWarningsOrErrors = true;
            DoWriteError(msg);
        }

        public virtual void WriteError(string msg, string nestedFilenames, int lineNo) {
            HasWarningsOrErrors = true;
            if (string.IsNullOrEmpty(nestedFilenames)) {
                WriteError(msg);
            } else {
                WriteError(msg + $" ({nestedFilenames}:{lineNo})");
            }
        }

        protected abstract void DoWriteWarning(string msg);

        public virtual void WriteWarning(string msg) {
            HasWarningsOrErrors = true;
            DoWriteWarning(msg);
        }

        public virtual void WriteWarning(string msg, string nestedFilenames, int lineNo) {
            HasWarningsOrErrors = true;
            if (string.IsNullOrEmpty(nestedFilenames)) {
                DoWriteWarning(msg);
            } else {
                DoWriteWarning(msg + $" ({nestedFilenames}:{lineNo})");
            }
        }

        public abstract void WriteInfo(string msg);

        public abstract void WriteDebug(string msg);

        public virtual void WriteViolation(Dependency dependency) {
            if (dependency.BadCt > 0) {
                WriteError(FormatMessage(dependency, dependency.BadDependencyMessage()));
            } else if (dependency.QuestionableCt > 0) {
                WriteWarning(FormatMessage(dependency, dependency.QuestionableDependencyMessage()));
            }
        }

        private static string FormatMessage(Dependency dependency, string message) {
            return dependency.Source != null
                ? new StringBuilder(message).Append(" (probably at ").Append(dependency.Source).Append(")").ToString()
                : message;
        }
    }
}