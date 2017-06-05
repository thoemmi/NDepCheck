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

        public virtual void WriteViolation(Dependency dependency, bool simpleRuleOutput) {
            string message = dependency.Source != null 
                ? $"{dependency.NotOkMessage(simpleRuleOutput: simpleRuleOutput, newLine: false)} (probably at {dependency.Source})" 
                : dependency.NotOkMessage(simpleRuleOutput: simpleRuleOutput, newLine: false);
            if (dependency.BadCt > 0) {
                WriteError(message);
            } else if (dependency.QuestionableCt > 0) {
                WriteWarning(message);
            } else {
                WriteInfo(message);
            }
        }
    }
}