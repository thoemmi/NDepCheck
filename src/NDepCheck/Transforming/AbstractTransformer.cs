using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public abstract class AbstractTransformer<T> : ITransformer {
        public abstract string GetHelp(bool detailedHelp);

        #region Configure

        /// <summary>
        /// Constant for variable settings.
        /// </summary>
        private const string ASSIGN = ":=";

        private readonly Dictionary<string, T> _fileName2config = new Dictionary<string, T>();

        public abstract void Configure(GlobalContext globalContext, [NotNull] string configureOptions);

        public T GetOrReadChildConfiguration(GlobalContext globalContext, 
            Func<TextReader> createReader, string fullSourceName,bool ignoreCase, string fileIncludeStack) {
            T childConfiguration;
            if (!_fileName2config.TryGetValue(fullSourceName, out childConfiguration)) {
                using (var tr = createReader()) {
                    childConfiguration =  CreateConfigurationFromText(globalContext, fullSourceName, 0, tr, ignoreCase,
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

        protected abstract T CreateConfigurationFromText(GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack);

        protected void ProcessTextInner(GlobalContext globalContext, string fullConfigFileName, int startLineNo, TextReader tr,
            bool ignoreCase, string fileIncludeStack, 
            [NotNull] Action<T,string> onIncludedConfiguration,
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

                line = ExpandDefines(line.Trim(), globalContext).Trim();
                lineNo++;

                try {
                    if (line == "" || line.StartsWith("#") || line.StartsWith("//")) {
                        // ignore;
                    } else if (line.StartsWith("+")) {
                        string includeFilename = line.Substring(1).Trim();
                        string fullIncludeFileName = Path.Combine(Path.GetDirectoryName(fullConfigFileName) ?? @"\", includeFilename);
                        T childConfiguration = GetOrReadChildConfiguration(globalContext,                             
                            () => new StreamReader(fullIncludeFileName), fullIncludeFileName,
                            ignoreCase, fileIncludeStack);
                        onIncludedConfiguration(childConfiguration, fullConfigFileName);
                    } else if (line.Contains(ASSIGN)) {
                        KeyValuePair<string, string> kvp = ParseVariableDefinition(fullConfigFileName, lineNo, line);
                        var globalVars = globalContext.GlobalVars;
                        if (globalVars.ContainsKey(kvp.Key)) {
                            if (globalVars[kvp.Key] != kvp.Value) {
                                throw new ApplicationException($"'{kvp.Key}' cannot be redefined as '{kvp.Value}' at {fullConfigFileName}:{lineNo}");
                            }
                        } else {
                            globalVars.Add(kvp.Key, kvp.Value);
                        }
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

        /// <summary>
        /// Expand first found macro in s.
        /// </summary>
        /// <returns></returns>
        internal string ExpandDefines([CanBeNull] string s, GlobalContext globalContext) {
            Dictionary<string, string> vars = globalContext.GlobalVars;
            s = s ?? "";
            foreach (string key in vars.Keys.OrderByDescending(k => k.Length)) {
                s = Regex.Replace(s, @"\b" + key + @"\b", vars[key]);
            }
            return s;
        }

        #endregion Configure

        #region Transform

        public abstract bool RunsPerInputContext { get; }

        public abstract int Transform(GlobalContext context, string dependenciesFileName,
                                      IEnumerable<Dependency> dependencies, string transformOptions, 
                                      string dependencySourceForLogging, Dictionary<FromTo, Dependency> newDependenciesCollector);

        public abstract IEnumerable<Dependency> GetTestDependencies();

        public abstract void FinishTransform(GlobalContext context);

        #endregion Transform
    }
}