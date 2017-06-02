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

        private static readonly string NewLine = System.Environment.NewLine;

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

        private readonly List<Environment> _environments = new List<Environment> { CreateDefaultEnvironment() };

        public Environment CurrentEnvironment => _environments[_environments.Count - 1];

        private int _autoEnvironmentForTransform = 10;
        private int _autoEnvironmentForRead = 0;


        //private readonly Stack<IEnumerable<Dependency>> _dependencies = new Stack<IEnumerable<Dependency>>();

        //private IEnumerable<Dependency> AllDependencies => _dependencies.Peek();

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

        public TimeSpan TimeLongerThan { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan AbortTime { get; set; } = TimeSpan.FromSeconds(10);

        [CanBeNull]
        private CancellationTokenSource _cancellationTokenSource;

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
                    $"directory {System.Environment.CurrentDirectory}; problem: {ex.Message}", ex);
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
                    string help = fullName + ":" + NewLine + plugin.GetHelp(detailedHelp: false, filter: "");
                    if (help.IndexOf(filter ?? "", StringComparison.InvariantCultureIgnoreCase) >= 0) {
                        matched.Add(fullName, HELP_SEPARATOR + NewLine + help + NewLine);
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

            IEnumerable<string> includedFilenames = includes.SelectMany(p => Option.ExpandFilePatternFileNames(p, extensionsForDirectoryReading));
            IEnumerable<string> excludedFilenames = excludes.SelectMany(p => Option.ExpandFilePatternFileNames(p, extensionsForDirectoryReading));
            string[] fileNames = includedFilenames
                                         .Except(excludedFilenames)
                                         .Distinct()
                                         .OrderBy(s => s)
                                         .ToArray();

            if (Log.IsVerboseEnabled) {
                Log.WriteInfo("Files to be read");
                foreach (var f in fileNames) {
                    Log.WriteInfo("  " + f);
                }
            }

            IDependencyReader[] readers = fileNames.Select(fileName => readerFactory.CreateReader(fileName, needsOnlyItemTails: false/*???*/)).ToArray();

            foreach (var r in readers) {
                r.SetReadersInSameReadFilesBeforeReadDependencies(readers);
            }

            // Currently, we add the previous set of dependencies to the newly read ones; with the introduction of a useful "working set" concept, this should vanish ...
            var readSet = new List<Dependency>();
            foreach (var r in readers) {
                Dependency[] dependencies = r.ReadDependencies(CurrentEnvironment, 0, IgnoreCase).ToArray();
                if (!dependencies.Any()) {
                    Log.WriteWarning("No dependencies found in " + r.FullFileName);
                }

                readSet.AddRange(dependencies);
            }
            if (_autoEnvironmentForRead > 0) {
                CreateEnvironment(readerFactory.GetType().Name, EnvironmentCreationType.AutoRead, readSet);
                RemoveSuperfluousEnvironments(_autoEnvironmentForRead, EnvironmentCreationType.AutoRead);
            } else {
                CurrentEnvironment.AddDependencies(readSet);
            }

            Log.WriteInfo($"... now {CurrentEnvironment.DependencyCount} dependencies");
        }

        private void RemoveSuperfluousEnvironments(int limit, EnvironmentCreationType type) {
            if (limit <= 0) {
                throw new ArgumentException("internal error", nameof(limit));
            }
            var matchingEnvironments = _environments.Where(e => e.Type == type).ToArray();
            if (matchingEnvironments.Length > limit) {
                var toBeRemoved = matchingEnvironments.Take(matchingEnvironments.Length - limit);
                _environments.RemoveAll(e => toBeRemoved.Contains(e));
            }
        }

        private void CreateEnvironment(string namePrefix, EnvironmentCreationType type, IEnumerable<Dependency> dependencies) {
            _environments.Add(new Environment(GetNewEnvironmentNameStartingWith(namePrefix), type, dependencies));
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
            CreateEnvironment(transformerClass + ".TestDependencies", EnvironmentCreationType.AutoTransform, transformer.CreateSomeTestDependencies(CurrentEnvironment));

            var newDependenciesCollector = new List<Dependency>();
            transformer.Transform(this, CurrentEnvironment.Dependencies, transformerOptions, newDependenciesCollector);

            CurrentEnvironment.ReplaceDependencies(newDependenciesCollector);

            SomethingDone = true;
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

                    if (_autoEnvironmentForTransform > 0) {
                        CreateEnvironment(transformerClass, EnvironmentCreationType.AutoTransform, CurrentEnvironment.Dependencies);
                        RemoveSuperfluousEnvironments(_autoEnvironmentForTransform, EnvironmentCreationType.AutoTransform);
                    }

                    var newDependenciesCollector = new List<Dependency>();
                    int result = transformer.Transform(this, CurrentEnvironment.Dependencies, transformerOptions, newDependenciesCollector);

                    if (newDependenciesCollector.Contains(null)) {
                        throw new ArgumentNullException(nameof(newDependenciesCollector), "Contains null item");
                    }

                    CurrentEnvironment.ReplaceDependencies(newDependenciesCollector);

                    Log.WriteInfo($"... now {CurrentEnvironment.DependencyCount} dependencies");

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

            IEnumerable<Dependency> dependencies = renderer.CreateSomeTestDependencies(CurrentEnvironment);
            renderer.Render(this, dependencies, rendererOptions, target, IgnoreCase);

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

                    string masterFileName = renderer.GetMasterFileName(this, rendererOptions, target).FullFileName;
                    if (WorkLazily && File.Exists(masterFileName)) {
                        // we dont do anything - TODO check change dates of input files vs. the master file's last update date
                    } else {
                        RestartAbortWatchDog();

                        renderer.Render(this, CurrentEnvironment.Dependencies, rendererOptions ?? "", target, IgnoreCase);
                    }
                    SomethingDone = true;

                    return masterFileName;
                } finally {
                    StopAbortWatchDog();
                }
            }
        }

        public RegexOptions GetIgnoreCase() {
            return IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        }

        public void ResetAll() {
            _environments.Clear();
            _environments.Add(CreateDefaultEnvironment());

            SomethingDone = false;
            SomethingDone = false;

            ItemType.Reset();
            ////Item.Reset();

            _globalValues.Clear();
        }

        public void ShowDetailedHelp<T>([CanBeNull] string assemblyName, [CanBeNull] string pluginClassName,
            [CanBeNull] string filter) where T : IPlugin {
            try {
                T plugin = GetOrCreatePlugin<T>(assemblyName, pluginClassName);
                Log.WriteInfo(plugin.GetType().FullName + ":" + NewLine +
                              plugin.GetHelp(detailedHelp: true, filter: filter) + NewLine);
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
            IEnumerable<Dependency> matchingDependencies = CurrentEnvironment.Dependencies.Where(d => m == null || m.IsMatch(d));
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
            ItemMatch m = pattern == null ? null : new ItemMatch(pattern, IgnoreCase, anyWhereMatcherOk: true);
            IEnumerable<Item> allItems =
                new HashSet<Item>(CurrentEnvironment.Dependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
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

        #region Environment handling

        private string GetNewEnvironmentNameStartingWith(string s) {
            int ct = _environments.Count(e => e.Name.StartsWith(s));
            return s + (ct == 0 ? "" : "_" + ct);
        }

        public void AutoForTransform(string flagArgument) {
            SetAutoFlag(flagArgument, ref _autoEnvironmentForTransform);
        }

        public void AutoForRead(string flagArgument) {
            SetAutoFlag(flagArgument, ref _autoEnvironmentForRead);
        }

        private static void SetAutoFlag(string arg, ref int flag) {
            int ct;
            if (arg == null) {
                flag = 10;
            } else if (arg == "-") {
                flag = 0;
            } else if (int.TryParse(arg, out ct) && ct >= 0 && ct < 100) {
                flag = ct;
            } else {
                throw new ArgumentException("Possible arguments: 0...100, none for 10, - for 0");
            }
        }

        private static Environment CreateDefaultEnvironment() {
            return new Environment("#0", EnvironmentCreationType.Manual, new Dependency[0]);
        }

        [CanBeNull]
        private Environment FindEnvironment([NotNull] string name) {
            Environment result = _environments.FirstOrDefault(e => e.Name == name) ??
                                 _environments.FirstOrDefault(e => e.Name.StartsWith(name));
            if (result == null) {
                int id;
                if (int.TryParse(name, out id) && id > 0 && id <= _environments.Count) {
                    result = _environments[_environments.Count - id];
                }
            }
            return result;
        }

        /// <summary>
        /// Return dependencies in environment identified by name; if name is null, return the
        /// environment below the current environment, if it exists.
        /// </summary>
        [CanBeNull]
        public IEnumerable<Dependency> FindDependenciesInEnvironment([CanBeNull] string name) {
            return name == null
                ? (_environments.Count >= 2 ? _environments[_environments.Count - 2].Dependencies : null)
                : FindEnvironment(name)?.Dependencies;
        }

        public void CloneEnvironments(string newName, IEnumerable<string> clonedNames) {
            if (clonedNames.Any()) {
                foreach (var n in clonedNames) {
                    Environment e = FindEnvironment(n);
                    if (e == null) {
                        Log.WriteError($"No environment with name '{n}' found");
                    } else {
                        CreateEnvironment(e.Name, EnvironmentCreationType.Manual, e.Dependencies.Select(d => d.Clone()));
                    }
                }
            } else {
                Environment e = CurrentEnvironment;
                CreateEnvironment(e.Name, EnvironmentCreationType.Manual, e.Dependencies.Select(d => d.Clone()));
            }
        }

        public void PushNewEnvironment(string newName) {
            CreateEnvironment(newName, EnvironmentCreationType.Manual, new Dependency[0]);
        }

        public void DeleteEnvironments(IEnumerable<string> namesToBeDeleted) {
            if (namesToBeDeleted.Any()) {
                foreach (var n in namesToBeDeleted) {
                    Environment e = FindEnvironment(n);
                    if (e == null) {
                        Log.WriteWarning($"No environment with name '{n}' found");
                    } else {
                        _environments.Remove(e);
                    }
                }
            } else {
                _environments.RemoveAt(_environments.Count - 1);
            }
            if (!_environments.Any()) {
                _environments.Add(CreateDefaultEnvironment());
            }
        }

        public void IncludeEnvironments(IEnumerable<string> namesToBeIncluded, bool removeIncluded) {
            if (namesToBeIncluded.Contains(CurrentEnvironment.Name)) {
                Log.WriteError($"Cannot add current environment to itself");
            }
            if (namesToBeIncluded.Any()) {
                foreach (var n in namesToBeIncluded) {
                    Environment e = FindEnvironment(n);
                    if (e == null) {
                        Log.WriteError($"No environment with name '{n}' found");
                    } else {
                        CurrentEnvironment.AddDependencies(e.Dependencies);
                        if (removeIncluded) {
                            _environments.Remove(e);
                        }
                    }
                }
            } else {
                if (_environments.Count >= 2) {
                    Environment e = _environments[_environments.Count - 2];
                    CurrentEnvironment.AddDependencies(e.Dependencies);
                    if (removeIncluded) {
                        _environments.Remove(e);
                    }
                }
            }
        }

        public void MakeTop(string name) {
            Environment e = FindEnvironment(name);
            if (e == null) {
                Log.WriteError($"No environment with name '{name}' found");
            } else {
                _environments.Remove(e);
                _environments.Add(e);
            }
        }

        public void ListEnvironments() {
            for (int i = _environments.Count - 1; i >= 0; i--) {
                Log.WriteInfo(_environments.Count - i + ": " + _environments[i]);
            }
        }

        #endregion Environment handling

        public void RenameCurrentEnvironment(string newName) {
            CurrentEnvironment.Name = newName;
        }
    }
}
