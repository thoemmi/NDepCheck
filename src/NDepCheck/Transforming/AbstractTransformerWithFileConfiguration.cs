using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public abstract class AbstractTransformerWithFileConfiguration<TConfiguration> : ITransformer {
        /// <summary>
        /// Constant for variable settings.
        /// </summary>
        private const string ASSIGN = ":=";

        private ValuesFrame _localVars;

        public abstract string GetHelp(bool detailedHelp, string filter);

        #region Configure

        private readonly Dictionary<string, TConfiguration> _configFile2Config = new Dictionary<string, TConfiguration>();

        private readonly Dictionary<string, Dictionary<string, string>> _container2configValues = new Dictionary<string, Dictionary<string, string>>();

        public virtual void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _localVars = new ValuesFrame();
        }

        protected string NormalizeLine([NotNull] GlobalContext globalContext, [CanBeNull] string line,
            [CanBeNull] Dictionary<string, string> configValueCollector) {
            if (line != null) {
                int commentStart = line.IndexOf("//", StringComparison.InvariantCulture);
                if (commentStart >= 0) {
                    line = line.Substring(0, commentStart);
                }
                return globalContext.ExpandDefinesAndHexChars(_localVars.ExpandDefines(line.Trim(), null), configValueCollector).Trim();
            } else {
                return null;
            }
        }

        protected void ProcessTextInner([NotNull] GlobalContext globalContext, string fullConfigFileName, int startLineNo,
            TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration,
            [NotNull] Action<TConfiguration, string> onIncludedConfiguration,
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
                    if (line == "") {
                        // ignore;
                    } else if (line.StartsWith("+")) {
                        string includeFilename = line.Substring(1).Trim();
                        string fullIncludeFileName = Path.Combine(Path.GetDirectoryName(fullConfigFileName) ?? @"\", includeFilename);
                        TConfiguration childConfiguration = GetOrReadChildConfiguration(globalContext,
                            () => new StreamReader(fullIncludeFileName), fullIncludeFileName,
                            ignoreCase, fileIncludeStack, forceReloadConfiguration);
                        onIncludedConfiguration(childConfiguration, fullConfigFileName);
                    } else if (line.Contains(ASSIGN)) {
                        KeyValuePair<string, string>? kvp = ParseVariableDefinition(line);
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

        private KeyValuePair<string, string>? ParseVariableDefinition([NotNull] string line) {
            int i = line.IndexOf(ASSIGN, StringComparison.Ordinal);
            string key = line.Substring(0, i).Trim();
            string value = line.Substring(i + ASSIGN.Length).Trim();

            if (key == value) {
                // This happens if the variable has been defined by multiply reading an input file; it is ok :-)
                return null;
            } else {
                return new KeyValuePair<string, string>(key, value);
            }
        }

        protected TConfiguration GetOrReadChildConfiguration([NotNull] GlobalContext globalContext,
            Func<TextReader> createReader, string containerUri, bool ignoreCase, string fileIncludeStack, bool forceReload) {
            TConfiguration childConfiguration;

            Dictionary<string, string> previousConfigValues;
            if (_container2configValues.TryGetValue(containerUri, out previousConfigValues)) {
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
                        throw new ApplicationException($"File {containerUri} is read with different values:{Environment.NewLine}{differences}");
                    }
                }
                previousConfigValues = null; // no collecting of config values!
            } else {
                // Collect new config values
                _container2configValues.Add(containerUri, previousConfigValues = new Dictionary<string, string>());
            }

            if (forceReload || !_configFile2Config.TryGetValue(containerUri, out childConfiguration)) {
                using (var tr = createReader()) {
                    childConfiguration = CreateConfigurationFromText(globalContext, containerUri, 0, tr, ignoreCase,
                        fileIncludeStack + "+" + containerUri, forceReload, previousConfigValues);
                    _configFile2Config[containerUri] = childConfiguration;
                }
            }
            return childConfiguration;
        }

        protected abstract TConfiguration CreateConfigurationFromText([NotNull] GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration,
            [CanBeNull] Dictionary<string, string> configValueCollector);

        #endregion Configure

        #region Transform

        public abstract int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, 
                                      string transformOptions, [NotNull] List<Dependency> transformedDependencies);

        public abstract IEnumerable<Dependency> CreateSomeTestDependencies();

        #endregion Transform
    }
}