using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public abstract class AbstractTransformerWithConfigurationPerInputfile<TConfigurationPerInputfile> : ITransformer {
        public abstract string GetHelp(bool detailedHelp, string filter);

        #region Configure

        /// <summary>
        /// Constant for variable settings.
        /// </summary>
        private const string ASSIGN = ":=";

        private readonly Dictionary<string, Dictionary<string, string>> _fileName2configValues = new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, TConfigurationPerInputfile> _fileName2config = new Dictionary<string, TConfigurationPerInputfile>();
        private ValuesFrame _localVars;

        public virtual void Configure(GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _localVars = new ValuesFrame();
        }

        public TConfigurationPerInputfile GetOrReadChildConfiguration(GlobalContext globalContext,
            Func<TextReader> createReader, string fullSourceName, bool ignoreCase, string fileIncludeStack, bool forceReload) {
            TConfigurationPerInputfile childConfiguration;

            Dictionary<string, string> previousConfigValues;
            if (_fileName2configValues.TryGetValue(fullSourceName, out previousConfigValues)) {
                if (!forceReload) {
                    // Check saved names against 
                    var differences = new StringBuilder();
                    foreach (var kvp in previousConfigValues) {
                        string currentValue = globalContext.GetValue(kvp.Key);
                        if (currentValue != kvp.Value) {
                            differences.AppendLine($"{kvp.Key}: {currentValue} vs. {kvp.Value}");
                        }
                    }
                    if (differences.Length > 0) {
                        throw new ApplicationException($"File {fullSourceName} is read with different values:\r\n{differences}");
                    }
                }
                previousConfigValues = null; // no collecting of config values!
            } else {
                // Collect new config values
                _fileName2configValues.Add(fullSourceName, previousConfigValues = new Dictionary<string, string>());
            }

            if (!_fileName2config.TryGetValue(fullSourceName, out childConfiguration)) {
                using (var tr = createReader()) {
                    childConfiguration = CreateConfigurationFromText(globalContext, fullSourceName, 0, tr, ignoreCase,
                        fileIncludeStack + "+" + fullSourceName, forceReload, previousConfigValues);
                    _fileName2config[fullSourceName] = childConfiguration;
                }
            }
            return childConfiguration;
        }

        // Maybe necessary ............................. currently unused
        protected string GetCanonicalName(DirectoryInfo relativeRoot, string fileName) {
            return GetCanonicalName(Path.Combine(relativeRoot.FullName, fileName));
        }

        // Maybe necessary ............................. currently unused
        protected static string GetCanonicalName(string path) {
            return new Uri(path).LocalPath;
        }

        protected abstract TConfigurationPerInputfile CreateConfigurationFromText(GlobalContext globalContext, string fullConfigFileName,
                    int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration,
                    [CanBeNull] Dictionary<string, string> configValueCollector);

        protected void ProcessTextInner(GlobalContext globalContext, string fullConfigFileName, int startLineNo, TextReader tr,
            bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration,
            [NotNull] Action<TConfigurationPerInputfile, string> onIncludedConfiguration,
            [NotNull] Func<string, int, string> onLineWithLineNo, 
            
            [CanBeNull] Dictionary<string, string> configValueCollector) {

            int lineNo = startLineNo;

            for (;;) {
                string line = NormalizeLine(globalContext, tr.ReadLine(), configValueCollector);

                if (line == null) {
                    break;
                }
                lineNo++;

                try {
                    if (line == "" || line.StartsWith("//")) {
                        // ignore;
                    } else if (line.StartsWith("+")) {
                        string includeFilename = line.Substring(1).Trim();
                        string fullIncludeFileName = Path.Combine(Path.GetDirectoryName(fullConfigFileName) ?? @"\", includeFilename);
                        TConfigurationPerInputfile childConfiguration = GetOrReadChildConfiguration(globalContext,
                            () => new StreamReader(fullIncludeFileName), fullIncludeFileName,
                            ignoreCase, fileIncludeStack, forceReloadConfiguration);
                        onIncludedConfiguration(childConfiguration, fullConfigFileName);
                    } else if (line.Contains(ASSIGN)) {
                        KeyValuePair<string, string>? kvp = ParseVariableDefinition(fullConfigFileName, lineNo, line);
                        if (kvp != null) {
                            _localVars.SetDefine(kvp.Value.Key, kvp.Value.Value, $"at {fullConfigFileName}:{lineNo}");
                        }
                    } else {
                        string errorOrNull = onLineWithLineNo(line, lineNo);
                        // line's content has been added to result as side-effect of onLineWithLineNo(...)
                        if (errorOrNull != null) {
                            throw new ApplicationException($"Cannot parse line '{line}' at {fullConfigFileName}:{lineNo}; reason: {errorOrNull}");
                        }
                    }
                } catch (Exception ex) {
                    throw new ApplicationException($"Problem '{ex.Message}' at {fullConfigFileName}:{lineNo}");
                }
            }
        }

        private KeyValuePair<string, string>? ParseVariableDefinition([NotNull] string ruleFileName, int lineNo, [NotNull] string line) {
            int i = line.IndexOf(ASSIGN, StringComparison.Ordinal);
            string key = line.Substring(0, i).Trim();
            string value = line.Substring(i + ASSIGN.Length).Trim();

            if (key == value) {
                // This happens if the variable has been defined by multiply reading an input file; it is ok :-)
                return null;
            } else {
                if (key != key.ToUpper()) {
                    throw new ApplicationException("'" + key + "' at " + ruleFileName + ":" + lineNo + " is not uppercase-only");
                }

                return new KeyValuePair<string, string>(key, value);
            }
        }

        private string NormalizeLine(GlobalContext globalContext, [CanBeNull] string line, 
                                     [CanBeNull] Dictionary<string, string> configValueCollector) {
            if (line != null) {
                int commentStart = line.IndexOf("//", StringComparison.InvariantCulture);
                if (commentStart >= 0) {
                    line = line.Substring(0, commentStart);
                }
                return globalContext.ExpandDefines(_localVars.ExpandDefines(line.Trim(), null), configValueCollector)?.Trim();
            } else {
                return null;
            }
        }

        #endregion Configure

        #region Transform

        public abstract bool RunsPerInputContext {
            get;
        }

        public abstract int Transform(GlobalContext globalContext, string dependenciesFileName, IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies);

        public abstract IEnumerable<Dependency> GetTestDependencies();

        public virtual void AfterAllTransforms(GlobalContext context) {
            // empty
        }

        #endregion Transform
    }
}