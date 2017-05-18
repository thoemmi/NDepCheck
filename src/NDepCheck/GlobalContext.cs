using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck {
    public class GlobalContext {
        private const string HELP_SEPARATOR = "=============================================";

        internal bool SomethingDone {
            get; set;
        }

        internal bool InputFilesOrTestDataSpecified {
            get; set;
        }

        internal bool HelpShown {
            get; private set;
        }

        public bool IgnoreCase {
            get; set;
        }

        private readonly ValuesFrame _globalValues = new ValuesFrame();
        private ValuesFrame _localParameters = new ValuesFrame();

        private readonly Stack<IEnumerable<Dependency>> _dependencies = new Stack<IEnumerable<Dependency>>();

        private IEnumerable<Dependency> AllDependencies => _dependencies.Peek();

        [NotNull]
        private readonly List<IPlugin> _plugins = new List<IPlugin>();

        static GlobalContext() {
            // Initialize all built-in reader factories because they contain predefined ItemTypes
            foreach (var t in GetPluginTypes<IReaderFactory>("")) {
                Activator.CreateInstance(t);
            }
        }

        public bool WorkLazily {
            get; set;
        }

        public string Name {
            get;
        }

        public TimeSpan TimeLongerThan { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan AbortTime { get; set; } = TimeSpan.FromSeconds(10);

        [CanBeNull]
        private CancellationTokenSource _cancellationTokenSource;

        private static int _cxtId = 0;

        public GlobalContext() {
            Name = "[" + ++_cxtId + "]";
            _dependencies.Push(Enumerable.Empty<Dependency>());
        }

        public void SetDefine(string key, string value, string location) {
            _globalValues.SetDefine(key, value, location);
        }

        [ContractAnnotation("s:null => null; s:notnull => notnull")]
        public string ExpandDefinesAndHexChars([CanBeNull] string s,
            [CanBeNull] Dictionary<string, string> configValueCollector) {
            return ExpandHexChars(_globalValues.ExpandDefines(_localParameters.ExpandDefines(s, configValueCollector), configValueCollector));
        }

        public string GetValue(string valueName) {
            return _localParameters.GetValue(valueName) ?? _globalValues.GetValue(valueName);
        }

        public static string ExpandHexChars([CanBeNull] string s) {
            return s != null && s.Contains('%')
                ? Regex.Replace(s, "%[0-9a-fA-F][0-9a-fA-F]",
                    m => "" + (char)int.Parse(m.Value.Substring(1), NumberStyles.HexNumber))
                : s;
        }

        [NotNull]
        private T GetOrCreatePlugin<T>([CanBeNull] string assemblyName, [CanBeNull] string pluginClassName)
            where T : IPlugin {
            if (pluginClassName == null) {
                throw new ArgumentNullException(nameof(pluginClassName), "Plugin class name missing");
            }

            IEnumerable<Type> pluginTypes = GetPluginTypes<T>(assemblyName);
            Type pluginType =
                pluginTypes.FirstOrDefault(
                    t => string.Compare(t.FullName, pluginClassName, StringComparison.InvariantCultureIgnoreCase) ==
                        0) ??
                pluginTypes.FirstOrDefault(
                    t => string.Compare(t.Name, pluginClassName, StringComparison.InvariantCultureIgnoreCase) == 0);
            if (pluginType == null) {
                throw new ApplicationException(
                    $"No plugin type found in assembly '{ShowAssemblyName(assemblyName)}' matching '{pluginClassName}'");
            }
            try {
                // Plugins can have state, therefore we must manage them in a repository. A simple list is sufficient.
                T result = (T)_plugins.FirstOrDefault(t => t.GetType() == pluginType);
                if (result == null) {
                    _plugins.Add(result = (T)Activator.CreateInstance(pluginType));
                }
                return result;
            } catch (Exception ex) {
                throw new ApplicationException(
                    $"Cannot create '{pluginClassName}' from assembly '{ShowAssemblyName(assemblyName)}' running in working " +
                    $"directory {Environment.CurrentDirectory}; problem: {ex.Message}", ex);
            }
        }

        private static string ShowAssemblyName(string assemblyName) {
            return string.IsNullOrWhiteSpace(assemblyName)
                ? typeof(GlobalContext).Assembly.GetName().Name
                : assemblyName;
        }

        private static IOrderedEnumerable<Type> GetPluginTypes<T>([CanBeNull] string assemblyName) {
            try {
                Assembly pluginAssembly = string.IsNullOrWhiteSpace(assemblyName) || assemblyName == "."
                    ? typeof(GlobalContext).Assembly
                    : Assembly.LoadFrom(assemblyName);

                return
                    pluginAssembly.GetExportedTypes()
                        .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                        .OrderBy(t => t.FullName);
            } catch (Exception ex) {
                throw new ApplicationException(
                    $"Cannot load types from assembly '{ShowAssemblyName(assemblyName)}'; reason: {ex.Message}");
            }
        }

        private IEnumerable<T> CreatePlugins<T>(string assemblyName) where T : class, IPlugin {
            return GetPluginTypes<T>(assemblyName).Select(t => {
                try {
                    return (T)Activator.CreateInstance(t);
                } catch (Exception ex) {
                    Log.WriteError($"Cannot get help for renderer '{t.FullName}'; reason: {ex.Message}");
                    return null;
                }
            })
            .Where(p => p != null)
            .ToArray();
        }

        public void ShowAllPluginsAndTheirHelp<T>(string assemblyName, string filter) where T : class, IPlugin {
            var matched = new Dictionary<string, string>();
            foreach (var plugin in CreatePlugins<T>(assemblyName)) {
                string fullName = plugin.GetType().FullName;
                try {
                    string help = fullName + ":" + Environment.NewLine + plugin.GetHelp(detailedHelp: false, filter: "");
                    if (help.IndexOf(filter ?? "", StringComparison.InvariantCultureIgnoreCase) >= 0) {
                        matched.Add(fullName, HELP_SEPARATOR + Environment.NewLine + help + Environment.NewLine);
                    }
                } catch (Exception ex) {
                    Log.WriteError($"Cannot get help for renderer '{fullName}'; reason: {ex.Message}");
                }
            }
            switch (matched.Count) {
                case 0:
                    Log.WriteWarning($"Found no {typeof(T).Name} types in '{ShowAssemblyName(assemblyName)}' matching '{filter}'");
                    break;
                case 1:
                    ShowDetailedHelp<T>(assemblyName, matched.Single().Key, "");
                    break;
                default:
                    foreach (var kvp in matched.OrderBy(kvp => kvp.Key)) {
                        Log.WriteInfo(kvp.Value);
                    }
                    break;
            }
            Log.WriteInfo(HELP_SEPARATOR);
        }

        /// <summary>
        /// Extract file patterns from args and read files
        /// </summary>
        /// <param name="includes"></param>
        /// <param name="excludes"></param>
        /// <param name="assemblyName"></param>
        /// <param name="readerFactoryClassNameOrNull">if null, detect from reader class from first extension in patterns</param>
        public void ReadFiles(IEnumerable<string> includes, IEnumerable<string> excludes, string assemblyName, [CanBeNull] string readerFactoryClassNameOrNull) {
            IReaderFactory readerFactory;
            if (readerFactoryClassNameOrNull == null) {
                IEnumerable<string> allFileNames = includes.Concat(excludes);
                readerFactory = GetSuitableInternalReader(assemblyName, allFileNames);
                if (readerFactory == null) {
                    throw new ApplicationException($"Found no reader for files {string.Join(",", allFileNames)}");
                }
            } else {
                readerFactory = GetOrCreatePlugin<IReaderFactory>(assemblyName, readerFactoryClassNameOrNull);
            }

            InputFilesOrTestDataSpecified = true;

            IEnumerable<string> extensionsForDirectoryReading = readerFactory.SupportedFileExtensions;

            string[] fileNames = includes.SelectMany(p => Option.ExpandFilePatternFileNames(p, extensionsForDirectoryReading)).Except(
                excludes.SelectMany(p => Option.ExpandFilePatternFileNames(p, extensionsForDirectoryReading))).ToArray();

            IDependencyReader[] readers = fileNames.Select(fileName => readerFactory.CreateReader(fileName, needsOnlyItemTails: false/*???*/)).ToArray();

            foreach (var r in readers) {
                r.SetReadersInSameReadFilesBeforeReadDependencies(readers);
            }

            // Currently, we add the previous set of dependencies to the newly read ones; with the introduction of a useful "working set" concept, this should vanish ...
            var readSet = new List<Dependency>(_dependencies.Peek());
            foreach (var r in readers) {
                Dependency[] dependencies = r.ReadDependencies(0, IgnoreCase).ToArray();
                if (!dependencies.Any()) {
                    Log.WriteWarning("No dependencies found in " + r.FullFileName);
                }

                readSet.AddRange(dependencies);
            }
            _dependencies.Push(readSet);
        }

        [CanBeNull]
        public IReaderFactory GetSuitableInternalReader(string assemblyName, IEnumerable<string> filenames) {
            string[] extensions = filenames.Select(p => {
                try {
                    return Path.GetExtension(p);
                } catch (ArgumentException) {
                    return null;
                }
            })
                .Where(p => p != null)
                .Distinct()
                .ToArray();
            return CreatePlugins<IReaderFactory>(assemblyName)
                .FirstOrDefault(t => t.SupportedFileExtensions.Intersect(extensions).Any());
        }

        public void ConfigureTransformer([CanBeNull] string assemblyName, [NotNull] string transformerClass,
            [CanBeNull] string transformerOptions, bool forceReloadConfiguration) {
            try {
                ITransformer plugin = GetOrCreatePlugin<ITransformer>(assemblyName, transformerClass);
                plugin.Configure(this, transformerOptions, forceReloadConfiguration);
            } catch (Exception ex) {
                Log.WriteError(
                    $"Cannot configure plugin '{transformerClass}' in assembly '{ShowAssemblyName(assemblyName)}'; reason: {ex.Message}");
                throw;
            }
        }

        public void TransformTestData(string assemblyName, string transformerClass, string transformerOptions) {
            ITransformer transformer = GetOrCreatePlugin<ITransformer>(assemblyName, transformerClass);
            _dependencies.Push(transformer.CreateSomeTestDependencies());

            var newDependenciesCollector = new List<Dependency>();
            transformer.Transform(this, AllDependencies, transformerOptions,
                newDependenciesCollector);

            SomethingDone = true;

            _dependencies.Pop();
            _dependencies.Push(newDependenciesCollector);
        }

        public void CheckAbort() {
            _cancellationTokenSource?.Token.ThrowIfCancellationRequested();
        }

        public int Transform([CanBeNull] string assemblyName, [NotNull] string transformerClass,
            [CanBeNull] string transformerOptions) {
            if (Option.IsHelpOption(transformerOptions)) {
                ShowDetailedHelp<ITransformer>(assemblyName, transformerClass, "");
                return Program.OPTIONS_PROBLEM;
            } else {
                try {
                    ITransformer transformer = GetOrCreatePlugin<ITransformer>(assemblyName, transformerClass);
                    Log.WriteInfo($"Transforming with {assemblyName}.{transformerClass}");

                    RestartAbortWatchDog();

                    var newDependenciesCollector = new List<Dependency>();

                    int result = transformer.Transform(this, AllDependencies.ToArray(), transformerOptions, newDependenciesCollector);

                    Dependency[] dependencies = newDependenciesCollector.ToArray();
                    if (dependencies.Contains(null)) {
                        throw new ArgumentNullException(nameof(dependencies), "Contains null item");
                    }
                    _dependencies.Push(dependencies);

                    Log.WriteInfo($"... now {dependencies.Length} dependencies");

                    SomethingDone = true;
                    return result;
                } catch (Exception ex) {
                    Log.WriteError(
                        $"Error while transforming with '{transformerClass}': {ex.GetType().Name} - {ex.Message}");
                    return Program.EXCEPTION_RESULT;
                } finally {
                    StopAbortWatchDog();
                }
            }
        }

        public void RestartAbortWatchDog() {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource.CancelAfter(AbortTime);
        }

        public void StopAbortWatchDog() {
            _cancellationTokenSource?.Dispose();
        }

        public void Calculate(string valueKey, string assemblyName, string calculatorClass,
            IEnumerable<string> input) {
            if (Option.IsHelpOption(input.FirstOrDefault())) {
                ShowDetailedHelp<ITransformer>(assemblyName, calculatorClass, "");
            } else {
                ICalculator calculator = GetOrCreatePlugin<ICalculator>(assemblyName, calculatorClass);
                try {
                    string value = calculator.Calculate(input.ToArray());
                    _globalValues.SetDefine(valueKey, value, "Computed by " + calculatorClass);
                } catch (Exception ex) {
                    Log.WriteError(
                        $"Cannot compute value with ${calculatorClass}; reason: {ex.GetType().Name} '{ex.Message}'");
                }
            }
        }

        public string RenderTestData([CanBeNull] string assemblyName, [CanBeNull] string rendererClassName,
            string rendererOptions, [NotNull] WriteTarget target) {
            IRenderer renderer = GetOrCreatePlugin<IRenderer>(assemblyName, rendererClassName);

            IEnumerable<Dependency> dependencies = renderer.CreateSomeTestDependencies();
            renderer.Render(this, dependencies, dependencies.Count(), rendererOptions, target, IgnoreCase);

            SomethingDone = true;

            return renderer.GetMasterFileName(this, rendererOptions, target).FullFileName;
        }

        public string RenderToFile([CanBeNull] string assemblyName, [CanBeNull] string rendererClassName,
            [CanBeNull] string rendererOptions, [NotNull] WriteTarget target) {
            if (Option.IsHelpOption(rendererOptions)) {
                ShowDetailedHelp<ITransformer>(assemblyName, rendererClassName, "");
                return null;
            } else {
                try {
                    IRenderer renderer = GetOrCreatePlugin<IRenderer>(assemblyName, rendererClassName);

                    Dependency[] allDependencies = AllDependencies.ToArray();
                    string masterFileName = renderer.GetMasterFileName(this, rendererOptions, target).FullFileName;
                    if (WorkLazily && File.Exists(masterFileName)) {
                        // we dont do anything - TODO check change dates of input files vs. the master file's last update date
                    } else {
                        RestartAbortWatchDog();

                        renderer.Render(this, allDependencies, allDependencies.Length, rendererOptions ?? "", target, IgnoreCase);
                    }
                    SomethingDone = true;

                    return masterFileName;
                } finally {
                    StopAbortWatchDog();
                }
            }
        }

        public void UndoTransform() {
            if (_dependencies.Count > 1) {
                _dependencies.Pop();
                int sum = _dependencies.Peek().Count();
                Log.WriteInfo($"{sum} dependencies");
            }
        }

        public RegexOptions GetIgnoreCase() {
            return IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        }

        public void ResetAll() {
            _dependencies.Clear();
            _dependencies.Push(Enumerable.Empty<Dependency>());

            SomethingDone = false;
            SomethingDone = false;

            ItemType.Reset();
            Item.Reset();

            _globalValues.Clear();
        }

        public void ShowDetailedHelp<T>([CanBeNull] string assemblyName, [CanBeNull] string pluginClassName,
            [CanBeNull] string filter) where T : IPlugin {
            try {
                T plugin = GetOrCreatePlugin<T>(assemblyName, pluginClassName);
                Log.WriteInfo(plugin.GetType().FullName + ":" + Environment.NewLine +
                              plugin.GetHelp(detailedHelp: true, filter: filter) + Environment.NewLine);
            } catch (Exception ex) {
                Log.WriteError(
                    $"Cannot print help for plugin '{pluginClassName}' in assembly '{ShowAssemblyName(assemblyName)}'; reason: {ex.Message}");
            }
            HelpShown = true;
        }

        // TODO: ---> Option ????????????? or WriteTarget?????????
        public static WriteTarget CreateFullFileName(WriteTarget target, string extension) {
            if (target == null || target.IsConsoleOut) {
                return new WriteTarget(null, true, limitLinesForConsole: 100);
            } else {
                if (extension != null) {
                    return new WriteTarget(Path.ChangeExtension(target.FullFileName, extension), target.Append, limitLinesForConsole: 100);
                } else {
                    return new WriteTarget(Path.GetFullPath(target.FileName), target.Append, limitLinesForConsole: 100);
                }
            }
        }

        public ItemType GetItemType(string definition) {
            IEnumerable<string> parts = definition.Split('(', ':', ')').Select(s => s.Trim()).Where(s => s != "");
            string name = parts.First();

            return ItemType.New(name, parts.Skip(1).ToArray(), IgnoreCase);
        }

        public void LogAboutNDependencies(int maxCount, [CanBeNull] string pattern, [NotNull] WriteTarget target) {
            IEnumerable<Dependency> matchingDependencies = LogOnlyDependencyCount(pattern);
            int n = target.IsConsoleOut ? maxCount : int.MaxValue / 2;
            using (var tw = target.CreateWriter()) {
                foreach (var d in matchingDependencies.Take(n)) {
                    tw.WriteLine(d.AsLimitableStringWithTypes(false, threeLines: true));
                }
                if (matchingDependencies.Skip(n).Any()) {
                    tw.WriteLine("...");
                }
            }
            SomethingDone = true;
        }

        public void LogAboutNItems(int maxCount, [CanBeNull] string pattern, [NotNull] WriteTarget target) {
            List<Item> matchingItems = LogOnlyItemCount(pattern).ToList();
            int n = target.IsConsoleOut ? maxCount : int.MaxValue / 2;
            matchingItems.Sort((i1, i2) => string.Compare(i1.Name, i2.Name,
                        IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture));
            using (var tw = target.CreateWriter()) {
                foreach (var i in matchingItems.Take(n)) {
                    tw.WriteLine(i.AsFullString());
                }
                if (matchingItems.Skip(n).Any()) {
                    tw.WriteLine("...");
                }
            }
            SomethingDone = true;
        }

        public int LogDependencyCount(string pattern, int maxValue) {
            IEnumerable<Dependency> matchingDependencies = LogOnlyDependencyCount(pattern);
            foreach (var d in matchingDependencies.Take(3)) {
                Log.WriteInfo(d.AsLimitableStringWithTypes(withExampleInfo: false, threeLines: true));
            }
            if (matchingDependencies.Skip(3).Any()) {
                Log.WriteInfo("...");
            }
            SomethingDone = true;
            return matchingDependencies.Skip(maxValue).Any() ? Program.DEPENDENCIES_NOT_OK : Program.OK_RESULT;
        }

        private IEnumerable<Dependency> LogOnlyDependencyCount(string pattern) {
            DependencyMatch m = pattern == null ? null : DependencyMatch.Create(pattern, IgnoreCase);
            IEnumerable<Dependency> matchingDependencies = AllDependencies.Where(d => m == null || m.IsMatch(d));
            Log.WriteInfo(matchingDependencies.Count() + " dependencies" + (m == null ? "" : " matching " + pattern));
            return matchingDependencies;
        }

        public int LogItemCount(string pattern, int maxValue) {
            IEnumerable<Item> matchingItems = LogOnlyItemCount(pattern);
            foreach (var i in matchingItems.Take(3)) {
                Log.WriteInfo(i.AsFullString());
            }
            if (matchingItems.Skip(3).Any()) {
                Log.WriteInfo("...");
            }
            SomethingDone = true;
            return matchingItems.Skip(maxValue).Any() ? Program.DEPENDENCIES_NOT_OK : Program.OK_RESULT;
        }

        private IEnumerable<Item> LogOnlyItemCount(string pattern) {
            ItemMatch m = pattern == null ? null : new ItemMatch(pattern, IgnoreCase);
            IEnumerable<Item> allItems =
                new HashSet<Item>(AllDependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
            IEnumerable<Item> matchingItems = allItems.Where(i => ItemMatch.IsMatch(m, i));
            Log.WriteInfo(matchingItems.Count() + " items" + (m == null ? "" : " matching " + pattern));
            return matchingItems;
        }

        public void ShowAllValues() {
            _globalValues.ShowAllValues("Global values:");
            _localParameters.ShowAllValues("Local parameters:");
        }

        public ValuesFrame SetLocals(ValuesFrame locals) {
            ValuesFrame previousValue = _localParameters;
            _localParameters = locals;
            return previousValue;
        }

        public static bool IsInternalPlugin<T>(string name) where T : IPlugin {
            return GetPluginTypes<T>("").Any(t => t.Name == name);
        }
    }
}
