using System;
using System.Collections.Generic;
using System.IO;

namespace NDepCheck {
    public class CheckerContext {
        private readonly IDictionary<string, DependencyRuleSet> _fullFilename2RulesetCache = new Dictionary<string, DependencyRuleSet>();
        private AssemblyContext _currentAssemblyContext;
        private readonly List<AssemblyContext> _assemblyContexts = new List<AssemblyContext>();
        private readonly bool _collectViolations;

        public CheckerContext(bool collectViolations) {
            _collectViolations = collectViolations;
        }

        public IAssemblyContext CurrentAssemblyContext {
            get { return _currentAssemblyContext; }
        }

        public IEnumerable<IAssemblyContext> AssemblyContexts {
            get { return _assemblyContexts; }
        }

        public DependencyRuleSet Create(DirectoryInfo relativeRoot,
                string rulefilename) {
            return Create(relativeRoot, rulefilename,
            new Dictionary<string, string>(), new Dictionary<string, DependencyRuleSet.Macro>());
        }

        public DependencyRuleSet Create(DirectoryInfo relativeRoot,
                string rulefilename,
                IDictionary<string, string> defines,
                IDictionary<string, DependencyRuleSet.Macro> macros) {
            string fullRuleFilename = Path.Combine(relativeRoot.FullName, rulefilename);
            DependencyRuleSet result;
            if (!_fullFilename2RulesetCache.TryGetValue(fullRuleFilename, out result)) {
                try {
                    long start = Environment.TickCount;
                    result = new DependencyRuleSet(this, fullRuleFilename, defines, macros);
                    Log.WriteDebug("Completed reading " + fullRuleFilename + " in " +
                                                            (Environment.TickCount - start) + " ms");
                    _fullFilename2RulesetCache.Add(fullRuleFilename, result);
                }
                catch (FileNotFoundException) {
                    Log.WriteError("File " + fullRuleFilename + " not found");
                    return null;
                }
            }
            return result;
        }

        /// <summary>
        /// Read rule set from file.
        /// </summary>
        /// <returns>Read rule set; or <c>null</c> if not poeeible to read it.</returns>
        public DependencyRuleSet Load(string dependencyFilename, List<DirectoryOption> directories) {
            foreach (var d in directories) {
                string fullName = d.GetFullNameFor(dependencyFilename);
                if (fullName != null) {
                    DependencyRuleSet result = Create(new DirectoryInfo("."), fullName);
                    if (result != null) {
                        return result;
                    }
                }
            }
            return null; // if nothing found
        }

        public IAssemblyContext OpenAssemblyContext(string filename) {
            if (_currentAssemblyContext != null) {
                throw new InvalidOperationException("There's already an AssemblyContext open");
            }
            _currentAssemblyContext = new AssemblyContext(filename, _collectViolations, CloseAssemblyContext);
            _assemblyContexts.Add(_currentAssemblyContext);
            return _currentAssemblyContext;
        }

        private class AssemblyContext : IAssemblyContext {
            private readonly string _filename;
            private readonly bool _collectViolations;
            private readonly List<RuleViolation> _ruleViolations = new List<RuleViolation>();
            private readonly Action _disposeAction;
            private int _errorCount;
            private int _warningCount;

            public AssemblyContext(string filename, bool collectViolations, Action disposeAction) {
                _filename = filename;
                _collectViolations = collectViolations;
                _disposeAction = disposeAction;
            }

            public string Filename {
                get { return _filename; }
            }

            public int ErrorCount {
                get { return _errorCount; }
            }

            public int WarningCount {
                get { return _warningCount; }
            }

            public IEnumerable<RuleViolation> RuleViolations {
                get { return _ruleViolations; }
            }

            public void Add(RuleViolation ruleViolation) {
                if (_collectViolations) {
                    _ruleViolations.Add(ruleViolation);
                }
                switch (ruleViolation.ViolationType) {
                    case ViolationType.Warning:
                        _warningCount++;
                        break;
                    case ViolationType.Error:
                        _errorCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            void IDisposable.Dispose() {
                _disposeAction();
            }
        }

        private void CloseAssemblyContext() {
            _currentAssemblyContext = null;
        }
    }

    public interface IAssemblyContext : IDisposable {
        void Add(RuleViolation ruleViolation);
        IEnumerable<RuleViolation> RuleViolations { get; }
        string Filename { get; }
        int ErrorCount { get; }
        int WarningCount { get; }
    }
}