using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public abstract class AbstractTransformerWithConfigurationPerInputfile<TConfigurationPerInputfile> : ITransformer {
        public abstract string GetHelp(bool detailedHelp, string filter);

        #region Configure

        /// <summary>
        /// Constant for variable settings.
        /// </summary>
        private const string ASSIGN = ":=";

        private readonly Dictionary<string, TConfigurationPerInputfile> _fileName2config = new Dictionary<string, TConfigurationPerInputfile>();

        public abstract void Configure(GlobalContext globalContext, [NotNull] string configureOptions);

        public TConfigurationPerInputfile GetOrReadChildConfiguration(GlobalContext globalContext, 
            Func<TextReader> createReader, string fullSourceName, bool ignoreCase, string fileIncludeStack) {
            TConfigurationPerInputfile childConfiguration;
            if (!_fileName2config.TryGetValue(fullSourceName, out childConfiguration)) {
                using (var tr = createReader()) {
                    childConfiguration = CreateConfigurationFromText(globalContext, fullSourceName, 0, tr, ignoreCase,
                        fileIncludeStack + "+" + fullSourceName);
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
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack);

        protected void ProcessTextInner(GlobalContext globalContext, string fullConfigFileName, int startLineNo, TextReader tr,
            bool ignoreCase, string fileIncludeStack, 
            [NotNull] Action<TConfigurationPerInputfile,string> onIncludedConfiguration,
            [NotNull] Func<string, int, bool> onLineWithLineNo) {

            int lineNo = startLineNo;

            for (;;) {
                string line = tr.ReadLine();

                if (line == null) {
                    break;
                }

                int commentStart = line.IndexOf("//", StringComparison.InvariantCulture);
                if (commentStart >= 0) {
                    line = line.Substring(0, commentStart);
                }

                line = globalContext.ExpandDefines(line.Trim()).Trim();
                lineNo++;

                try {
                    if (line == "" || line.StartsWith("#") || line.StartsWith("//")) {
                        // ignore;
                    } else if (line.StartsWith("+")) {
                        string includeFilename = line.Substring(1).Trim();
                        string fullIncludeFileName = Path.Combine(Path.GetDirectoryName(fullConfigFileName) ?? @"\", includeFilename);
                        TConfigurationPerInputfile childConfiguration = GetOrReadChildConfiguration(globalContext,                             
                            () => new StreamReader(fullIncludeFileName), fullIncludeFileName,
                            ignoreCase, fileIncludeStack);
                        onIncludedConfiguration(childConfiguration, fullConfigFileName);
                    } else if (line.Contains(ASSIGN)) {
                        KeyValuePair<string, string> kvp = ParseVariableDefinition(fullConfigFileName, lineNo, line);
                        globalContext.SetDefine(kvp.Key, kvp.Value, $"at {fullConfigFileName}:{lineNo}");
                    } else if (onLineWithLineNo(line, lineNo)) {
                        // line's content has been added to result as side-effect
                    } else {
                        throw new ApplicationException($"Cannot parse line '{line}' at {fullConfigFileName}:{lineNo}");
                    }
                } catch (Exception ex) {
                    throw new ApplicationException($"Problem '{ex.Message}' at {fullConfigFileName}:{lineNo}");
                }
            }
        }

        private KeyValuePair<string, string> ParseVariableDefinition([NotNull] string ruleFileName, int lineNo, [NotNull] string line) {
            int i = line.IndexOf(ASSIGN, StringComparison.Ordinal);
            string key = line.Substring(0, i).Trim();
            if (key != key.ToUpper()) {
                throw new ApplicationException("'" + key + "' at " + ruleFileName + ":" + lineNo + " is not uppercase-only");
            }
            string value = line.Substring(i + ASSIGN.Length).Trim();

            return new KeyValuePair<string, string>(key, value);
        }

        #endregion Configure

        #region Transform

        public abstract bool RunsPerInputContext { get; }

        public abstract int Transform(GlobalContext context, string dependenciesFileName, IEnumerable<Dependency> dependencies, 
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies);

        public abstract IEnumerable<Dependency> GetTestDependencies();

        public abstract void FinishTransform(GlobalContext context);

        #endregion Transform
    }
}