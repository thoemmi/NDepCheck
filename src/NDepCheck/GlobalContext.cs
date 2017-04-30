using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Matching;
using NDepCheck.Reading;

////namespace NDepCheck.Try {
////    public class IMarkerSet { }

////    public struct Item {
////        private readonly ItemType Type;
////        private readonly string[] Values;
////        private readonly IMarkerSet Markers;
////    }

////    public struct Dependency {
////        private int UsingItemIndex;
////        private int Ct, BadCt, QuestionableCt;
////        private int UsedItemIndex;
////    }

////    public class ImmtuableReferences<T> : IEnumerable<T> {
////        private ImmutableCollection<T> Referenced;
////        private int[] Indices;

////        private IEnumerator<T> GetAll() {
////            foreach (var i in Indices) {
////                yield return Referenced.LocalElements[i];
////            }
////        }

////        public IEnumerator<T> GetEnumerator() {
////            return GetAll();
////        }

////        IEnumerator IEnumerable.GetEnumerator() {
////            return GetAll();
////        }
////    }

////    public class ImmutableCollection<T> : IEnumerable<T> {
////        public readonly T[] LocalElements;
////        private readonly List<ImmtuableReferences<T>> References;

////        private IEnumerator<T> GetAll() {
////            foreach (var t in LocalElements) {
////                yield return t;
////            }
////            foreach (var r in References) {
////                foreach (var t in r) {
////                    yield return t;
////                }
////            }
////        }

////        public IEnumerator<T> GetEnumerator() {
////            return GetAll();
////        }

////        IEnumerator IEnumerable.GetEnumerator() {
////            return GetAll();
////        }
////    }



////}

namespace NDepCheck {

    public class GlobalContext {
        private const string HELP_SEPARATOR = "=============================================";
        internal bool RenderingDone { get; set; }
        internal bool TransformingDone { get; set; }
        internal bool InputFilesOrTestDataSpecified { get; set; }
        internal bool HelpShown { get; private set; }

        public bool IgnoreCase { get; set; }

        private readonly ValuesFrame _globalValues = new ValuesFrame();
        private ValuesFrame _localParameters = new ValuesFrame();

        [NotNull]
        private readonly Dictionary<string, InputContext> _inputContexts =
            new Dictionary<string, InputContext>();

        private readonly Stack<IEnumerable<Dependency>> _dependenciesWithoutInputContextStack =
            new Stack<IEnumerable<Dependency>>();

        private IEnumerable<Dependency> DependenciesWithoutInputContext
            => _dependenciesWithoutInputContextStack.Peek();

        public int BadDependenciesCountWithoutInputContext => DependenciesWithoutInputContext.Sum(d => d.BadCt);

        public int QuestionableDependenciesCountWithoutInputContext
            => DependenciesWithoutInputContext.Sum(d => d.QuestionableCt);

        [NotNull]
        private readonly List<IPlugin> _plugins = new List<IPlugin>();

        static GlobalContext() {
            // Initialize all built-in reader factories because they contain predefined ItemTypes
            foreach (var t in GetPluginTypes<IReaderFactory>("")) {
                Activator.CreateInstance(t);
            }
        }

        [NotNull]
        public IEnumerable<InputContext> InputContexts => _inputContexts.Values;

        public bool WorkLazily { get; set; }

        public string Name { get; }

        public TimeSpan TimeLongerThan { get; set; } = TimeSpan.FromSeconds(10);

        private static int _cxtId = 0;

        public GlobalContext() {
            Name = "[" + ++_cxtId + "]";
            _dependenciesWithoutInputContextStack.Push(Enumerable.Empty<Dependency>());
        }

        public void SetDefine(string key, string value, string location) {
            _globalValues.SetDefine(key, value, location);
        }

        [ContractAnnotation("s:null => null; s:notnull => notnull")]
        public string ExpandDefinesAndHexChars([CanBeNull] string s,
            [CanBeNull] Dictionary<string, string> configValueCollector) {
            return
                ExpandHexChars(_globalValues.ExpandDefines(_localParameters.ExpandDefines(s, configValueCollector),
                    configValueCollector));
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

        //public void ReadAllNotYetReadIn() {
        //    var stopwatch = new Stopwatch();
        //    stopwatch.Start();
        //    IEnumerable<AbstractDependencyReader> allReaders =
        //        _inputSpecs.SelectMany(i => i.CreateOrGetReaders(this, false))
        //            .OrderBy(r => r.FullFileName)
        //            .ToArray();

        //    foreach (var r in allReaders) {
        //        InputContext inputContext;
        //        if (!_inputContexts.TryGetValue(r.FullFileName, out inputContext)) {
        //            _inputContexts.Add(r.FullFileName, r.ReadDependencies(0, IgnoreCase));
        //        }
        //    }
        //    stopwatch.Stop();
        //    Program.LogElapsedTime(this, stopwatch, "Reading");
        //}

        private IEnumerable<Dependency> GetAllDependencies() {
            return _inputContexts.Values.SelectMany(ic => ic.Dependencies).Concat(DependenciesWithoutInputContext);
        }

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
                // plugins can have state, therefore we must manage them
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
                    string help = fullName + ":\r\n" + plugin.GetHelp(detailedHelp: false, filter: "");
                    if (help.IndexOf(filter ?? "", StringComparison.InvariantCultureIgnoreCase) >= 0) {
                        matched.Add(fullName, HELP_SEPARATOR + "\r\n" + help + "\r\n");
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
        /// <param name="args"></param>
        /// <param name="i"></param>
        /// <param name="assemblyName"></param>
        /// <param name="readerFactoryClassNameOrNull">if null, detect from reader class from first extension in patterns</param>
        /// <param name="firstFilePattern"></param>
        public void ReadFiles(string[] args, ref int i, string assemblyName, [CanBeNull] string readerFactoryClassNameOrNull,
                              [CanBeNull] string firstFilePattern = null) {
            List<string> includes = new List<string> { firstFilePattern ?? Option.ExtractRequiredOptionValue(args, ref i, "missing file pattern") };

            List<string> excludes = new List<string>();
            while (i + 1 < args.Length) {
                string arg = args[++i];
                if (arg == "-" || arg == "+") {
                    string pattern = Option.ExtractRequiredOptionValue(args, ref i, $"missing file pattern after {arg}");
                    (arg == "+" ? includes : excludes).Add(pattern);
                } else {
                    i--;
                    break;
                }
            }

            IReaderFactory readerFactory;
            if (readerFactoryClassNameOrNull == null) {
                readerFactory = GetSuitableInternalReader(assemblyName, includes.Concat(excludes));
            } else {
                readerFactory = GetOrCreatePlugin<IReaderFactory>(assemblyName, readerFactoryClassNameOrNull);
            }

            InputFilesOrTestDataSpecified = true;

            IEnumerable<string> extensionsForDirectoryReading = readerFactory.SupportedFileExtensions;

            string[] fileNames = includes.SelectMany(p => Option.ExpandFilePatternToFullFileNames(p, extensionsForDirectoryReading)).Except(
                excludes.SelectMany(p => Option.ExpandFilePatternToFullFileNames(p, extensionsForDirectoryReading))).ToArray();

            IDependencyReader[] readers = fileNames.Select(fileName => readerFactory.CreateReader(fileName, needsOnlyItemTails: false/*???*/)).ToArray();

            foreach (var r in readers) {
                r.SetReadersInSameReadFilesBeforeReadDependencies(readers);
            }

            foreach (var r in readers) {
                InputContext inputContext;
                if (!_inputContexts.TryGetValue(r.FullFileName, out inputContext)) {
                    _inputContexts.Add(r.FullFileName, r.ReadDependencies(0, IgnoreCase));
                }
            }
        }

        public IReaderFactory GetSuitableInternalReader(string assemblyName, IEnumerable<string> filenames) {
            string[] extensions = filenames.Select(p => {
                    try {
                        return Path.GetExtension(p);
                    } catch (ArgumentException) {
                        return null;
                    }
                })
                .Where(p => p != null)
                .ToArray();
            return CreatePlugins<IReaderFactory>(assemblyName)
                .FirstOrDefault(t => t.SupportedFileExtensions.Intersect(extensions).Any());
        }

        public void ConfigureTransformer([CanBeNull] string assemblyName, [NotNull] string transformerClass,
            [CanBeNull] string transformerOptions, bool forceReloadConfiguration) {
            // Reading might define item types that are needed in configuration
            //ReadAllNotYetReadIn();

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
            _dependenciesWithoutInputContextStack.Push(transformer.GetTestDependencies());

            var newDependenciesCollector = new List<Dependency>();
            transformer.Transform(this, "TestData", DependenciesWithoutInputContext, transformerOptions, "TestData",
                newDependenciesCollector);

            TransformingDone = true;

            _dependenciesWithoutInputContextStack.Pop();
            _dependenciesWithoutInputContextStack.Push(newDependenciesCollector);
        }

        public int Transform([CanBeNull] string assemblyName, [NotNull] string transformerClass,
            [CanBeNull] string transformerOptions) {
            if (Option.IsHelpOption(transformerOptions)) {
                ShowDetailedHelp<ITransformer>(assemblyName, transformerClass, "");
                return Program.OPTIONS_PROBLEM;
            } else {
                //ReadAllNotYetReadIn();

                ITransformer transformer = GetOrCreatePlugin<ITransformer>(assemblyName, transformerClass);
                Log.WriteInfo($"Transforming with {assemblyName}.{transformerClass}");

                var newDependenciesCollector = new List<Dependency>();
                int result = Program.OK_RESULT;
                if (transformer.RunsPerInputContext) {
                    foreach (var ic in _inputContexts.Values) {
                        result = Transform(transformerOptions, transformer, ic.Filename, ic.Dependencies,
                            "dependencies in file " + ic.Filename, newDependenciesCollector, result);
                    }
                    result = Transform(transformerOptions, transformer, "", DependenciesWithoutInputContext,
                        "generated dependencies", newDependenciesCollector, result);
                } else {
                    result = transformer.Transform(this, "", GetAllDependencies().ToArray(), transformerOptions,
                        "all dependencies", newDependenciesCollector);
                }

                if (newDependenciesCollector.Contains(null)) {
                    throw new NullReferenceException("newDependenciesCollector contains null dependency");
                }

                transformer.AfterAllTransforms(this);
                int sum = 0;
                foreach (var ic in _inputContexts.Values) {
                    sum += ic.PushDependencies(newDependenciesCollector.Where(d => d.InputContext == ic));
                }
                sum +=
                    PushDependenciesWithoutInputContext(
                        newDependenciesCollector.Where(d => d.InputContext == null).ToArray());
                TransformingDone = true;

                Log.WriteInfo($"... now {sum} dependencies");

                return result;
            }
        }

        private int Transform(string transformerOptions, ITransformer transformer,
            [CanBeNull] string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string dependencySourceForLogging, List<Dependency> newDependenciesCollector, int result) {
            try {
                int r = transformer.Transform(this, dependenciesFilename, dependencies, transformerOptions,
                    dependencySourceForLogging, newDependenciesCollector);
                result = Math.Max(result, r);
            } catch (Exception ex) {
                Log.WriteError(
                    $"Error while transforming '{dependencySourceForLogging}': {ex.GetType().Name} - {ex.Message}");
                result = Program.EXCEPTION_RESULT;
            }
            return result;
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
            string rendererOptions, [NotNull] string baseFileName) {
            IRenderer renderer = GetOrCreatePlugin<IRenderer>(assemblyName, rendererClassName);

            IEnumerable<Dependency> dependencies = renderer.CreateSomeTestDependencies();
            renderer.Render(this, dependencies, dependencies.Count(), rendererOptions, baseFileName, IgnoreCase);

            RenderingDone = true;

            return renderer.GetMasterFileName(this, rendererOptions, baseFileName);
        }

        public string RenderToFile([CanBeNull] string assemblyName, [CanBeNull] string rendererClassName,
            [CanBeNull] string rendererOptions, [CanBeNull] string fileName) {
            if (Option.IsHelpOption(rendererOptions)) {
                ShowDetailedHelp<ITransformer>(assemblyName, rendererClassName, "");
                return null;
            } else {
                IRenderer renderer = GetOrCreatePlugin<IRenderer>(assemblyName, rendererClassName);

                ////ReadAllNotYetReadIn();

                Dependency[] allDependencies = GetAllDependencies().ToArray();
                string masterFileName = renderer.GetMasterFileName(this, rendererOptions, fileName);
                if (WorkLazily && File.Exists(masterFileName)) {
                    // we dont do anything - TODO check change dates of input files vs. the master file's last update date
                } else {
                    renderer.Render(this, allDependencies, allDependencies.Length, rendererOptions ?? "", fileName,
                        IgnoreCase);
                }
                RenderingDone = true;

                return masterFileName;
            }
        }

        private int PushDependenciesWithoutInputContext(Dependency[] dependencies) {
            if (dependencies.Contains(null)) {
                throw new ArgumentNullException(nameof(dependencies), "Contains null item");
            }

            _dependenciesWithoutInputContextStack.Push(dependencies);
            return dependencies.Length;
        }

        public void UndoTransform() {
            if (_dependenciesWithoutInputContextStack.Count > 1) {
                _dependenciesWithoutInputContextStack.Pop();
                int sum = _dependenciesWithoutInputContextStack.Peek().Count();
                foreach (var ic in _inputContexts.Values) {
                    sum += ic.PopDependencies();
                }
                Log.WriteInfo($"{sum} dependencies");
            }
        }

        public RegexOptions GetIgnoreCase() {
            return IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        }

        public void ResetAll() {
            _inputContexts.Clear();
            _dependenciesWithoutInputContextStack.Clear();
            _dependenciesWithoutInputContextStack.Push(Enumerable.Empty<Dependency>());

            //_inputSpecs.Clear();

            RenderingDone = false;
            TransformingDone = false;

            ItemType.Reset();
            Item.Reset();

            _globalValues.Clear();
        }

        public void ShowDetailedHelp<T>([CanBeNull] string assemblyName, [CanBeNull] string pluginClassName,
            [CanBeNull] string filter) where T : IPlugin {
            try {
                T plugin = GetOrCreatePlugin<T>(assemblyName, pluginClassName);
                Log.WriteInfo(plugin.GetType().FullName + ":\r\n" +
                              plugin.GetHelp(detailedHelp: true, filter: filter) + "\r\n");
            } catch (Exception ex) {
                Log.WriteError(
                    $"Cannot print help for plugin '{pluginClassName}' in assembly '{ShowAssemblyName(assemblyName)}'; reason: {ex.Message}");
            }
            HelpShown = true;
        }

        // TODO: ---> Option ?????????????
        public static string CreateFullFileName(string fileName, string extension) {
            if (fileName == null || IsConsoleOutFileName(fileName)) {
                return "-";
            } else {
                if (extension != null) {
                    fileName = Path.ChangeExtension(fileName, extension);
                }
                fileName = Path.GetFullPath(fileName);
                return fileName;
            }
        }

        public static NamedTextWriter CreateTextWriter(string fullFileName) {
            if (fullFileName == "-") {
                Log.WriteInfo("Writing to console");
                return new NamedTextWriter(Console.Out, null);
            } else {
                Log.WriteInfo("Writing " + fullFileName);
                return new NamedTextWriter(new StreamWriter(fullFileName), fullFileName);

            }
        }

        public static bool IsConsoleOutFileName(string fileName) {
            return string.IsNullOrWhiteSpace(fileName) || fileName == "-";
        }

        public ItemType GetItemType(string definition) {
            IEnumerable<string> parts = definition.Split('(', ':', ')').Select(s => s.Trim()).Where(s => s != "");
            string name = parts.First();

            return ItemType.New(name, parts.Skip(1).ToArray(), IgnoreCase);
        }

        public void LogAboutNDependencies(int maxCount, [CanBeNull] string pattern) {
            //ReadAllNotYetReadIn();

            DependencyMatch m = pattern == null ? null : DependencyMatch.Create(pattern, IgnoreCase);
            InputContext[] nonEmptyInputContexts =
                _inputContexts.Values.Where(ic => ic.Dependencies.Any()).ToArray();
            maxCount = Math.Max(3 * nonEmptyInputContexts.Length, maxCount);
            int depsPerContext = maxCount / (nonEmptyInputContexts.Length + 1);
            foreach (var ic in nonEmptyInputContexts) {
                IEnumerable<Dependency> matchingDependencies =
                    ic.Dependencies.Where(d => m == null || m.IsMatch(d)).Take(depsPerContext + 1);
                foreach (var d in matchingDependencies.Take(depsPerContext)) {
                    maxCount--;
                    Log.WriteInfo(d.AsDipStringWithTypes(false));
                }
                if (matchingDependencies.Skip(depsPerContext).Any()) {
                    Log.WriteInfo("...");
                }
            }
            {
                IEnumerable<Dependency> matchingDependencies =
                    DependenciesWithoutInputContext.Where(d => m == null || m.IsMatch(d)).Take(maxCount + 1);
                foreach (var d in matchingDependencies.Take(maxCount)) {
                    Log.WriteInfo(d.AsDipStringWithTypes(false));
                }
                if (matchingDependencies.Skip(maxCount).Any()) {
                    Log.WriteInfo("...");
                }
            }

            LogOnlyItemCount(pattern);
        }

        public void LogAboutNItems(int maxCount, [CanBeNull] string pattern) {
            //ReadAllNotYetReadIn();

            List<Item> matchingItems = LogOnlyItemCount(pattern).ToList();
            matchingItems.Sort(
                (i1, i2) =>
                    string.Compare(i1.Name, i2.Name,
                        IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture));
            foreach (var i in matchingItems.Take(maxCount)) {
                Log.WriteInfo(i.AsFullString());
            }
            TransformingDone = true;
        }

        public void LogDependencyCount(string pattern) {
            //ReadAllNotYetReadIn();

            DependencyMatch m = pattern == null ? null : DependencyMatch.Create(pattern, IgnoreCase);
            int sum = DependenciesWithoutInputContext.Count(d => m == null || m.IsMatch(d));
            foreach (var ic in _inputContexts.Values) {
                sum += ic.Dependencies.Count(d => m == null || m.IsMatch(d));
            }
            Log.WriteInfo(sum + " dependencies" + (m == null ? "" : " matching " + pattern));
            foreach (var d in GetAllDependencies().Where(d => m == null || m.IsMatch(d)).Take(3)) {
                Log.WriteInfo(d.AsDipStringWithTypes(false));
            }
            TransformingDone = true;
        }

        public void LogItemCount(string pattern) {
            //ReadAllNotYetReadIn();

            IEnumerable<Item> matchingItems = LogOnlyItemCount(pattern);
            foreach (var i in matchingItems.Take(3)) {
                Log.WriteInfo(i.AsFullString());
            }
            TransformingDone = true;
        }

        private IEnumerable<Item> LogOnlyItemCount(string pattern) {
            ItemMatch m = pattern == null ? null : new ItemMatch(pattern, IgnoreCase);
            IEnumerable<Item> allItems =
                new HashSet<Item>(GetAllDependencies().SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
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
