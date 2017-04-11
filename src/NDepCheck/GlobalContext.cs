using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Calculating;
using NDepCheck.Reading;
using NDepCheck.Rendering;
using NDepCheck.Transforming;

namespace NDepCheck {
    public class NamedTextWriter : IDisposable {
        public NamedTextWriter(TextWriter writer, string fileName) {
            Writer = writer;
            FileName = fileName;
        }

        public TextWriter Writer {
            get;
        }
        public string FileName {
            get;
        }

        public void Dispose() {
            Writer?.Dispose();
        }
    }

    public class GlobalContext {
        private const string HELP_SEPARATOR = "=============================================\r\n";
        internal bool RenderingDone {
            get; set;
        }
        internal bool TransformingDone {
            get; set;
        }
        internal bool InputFilesOrTestDataSpecified {
            get; set;
        }
        internal bool HelpShown {
            get; private set;
        }

        public bool ShowUnusedQuestionableRules {
            get; set;
        }
        public bool ShowUnusedRules {
            get; set;
        }
        public bool IgnoreCase {
            get; set;
        }

        [NotNull]
        private readonly List<InputOption> _inputSpecs = new List<InputOption>();

        [NotNull]
        public Dictionary<string, string> GlobalVars { get; } = new Dictionary<string, string>();

        [NotNull]
        private readonly Dictionary<string, InputContext> _inputContexts = new Dictionary<string, InputContext>();

        private readonly Stack<IEnumerable<Dependency>> _dependenciesWithoutInputContextStack =
            new Stack<IEnumerable<Dependency>>();

        private IEnumerable<Dependency> DependenciesWithoutInputContext => _dependenciesWithoutInputContextStack.Peek();

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

        [NotNull]
        public List<InputOption> InputSpecs => _inputSpecs;

        public bool WorkLazily {
            get; set;
        }

        public string Name {
            get;
        }

        private static int _cxtId = 0;

        public GlobalContext() {
            Name = "[" + ++_cxtId + "]";
            _dependenciesWithoutInputContextStack.Push(Enumerable.Empty<Dependency>());
        }

        public void SetDefine(string key, string value, string location) {
            if (GlobalVars.ContainsKey(key)) {
                if (GlobalVars[key] != value) {
                    throw new ApplicationException($"'{key}' cannot be redefined as '{value}' {location}");
                }
            } else {
                GlobalVars.Add(key, value);
            }
        }

        public string ExpandDefines(string s) {
            Dictionary<string, string> vars = GlobalVars;
            s = s ?? "";
            foreach (string key in vars.Keys.OrderByDescending(k => k.Length)) {
                s = Regex.Replace(s, @"\b" + key + @"\b", vars[key] ?? "");
            }
            return s;
        }

        public void ReadAllNotYetReadIn() {
            IEnumerable<AbstractDependencyReader> allReaders = 
                _inputSpecs.SelectMany(i => i.CreateOrGetReaders(this, false)).OrderBy(r => r.FullFileName).ToArray();

            foreach (var r in allReaders) {
                InputContext inputContext;
                if (!_inputContexts.TryGetValue(r.FullFileName, out inputContext)) {
                    _inputContexts.Add(r.FullFileName, r.ReadDependencies(0));
                }
            }
        }

        public string RenderToFile([CanBeNull] string assemblyName, [CanBeNull] string rendererClassName, [CanBeNull] string rendererOptions, [CanBeNull] string fileName) {
            IDependencyRenderer renderer = GetOrCreatePlugin<IDependencyRenderer>(assemblyName, rendererClassName);

            ReadAllNotYetReadIn();

            Dependency[] allDependencies = GetAllDependencies().ToArray();
            string masterFileName = renderer.GetMasterFileName(this, rendererOptions, fileName);
            if (WorkLazily && File.Exists(masterFileName)) {
                // we dont do anything - TODO check change dates of input files vs. the master file's last update date
            } else {
                renderer.Render(this, allDependencies, rendererOptions ?? "", fileName, IgnoreCase);
            }
            RenderingDone = true;

            return masterFileName;
        }

        private IEnumerable<Dependency> GetAllDependencies() {
            return _inputContexts.Values.SelectMany(ic => ic.Dependencies).Concat(DependenciesWithoutInputContext);
        }

        [CanBeNull]
        public Dependency GetExampleDependency() {
            ReadAllNotYetReadIn();
            return _inputContexts.Values.SelectMany(ic => ic.Dependencies).Concat(DependenciesWithoutInputContext).FirstOrDefault();
        }

        private T GetOrCreatePlugin<T>([CanBeNull] string assemblyName, [CanBeNull] string pluginClassName)
            where T : IPlugin {
            if (pluginClassName == null) {
                throw new ArgumentNullException(nameof(pluginClassName), "Plugin class name missing");
            }

            IEnumerable<Type> pluginTypes = GetPluginTypes<T>(assemblyName);
            Type pluginType =
                pluginTypes.FirstOrDefault(
                    t => string.Compare(t.FullName, pluginClassName, StringComparison.InvariantCultureIgnoreCase) == 0) ??
                pluginTypes.FirstOrDefault(
                    t => string.Compare(t.Name, pluginClassName, StringComparison.InvariantCultureIgnoreCase) == 0);
            if (pluginType == null) {
                throw new ApplicationException(
                    $"No plugin type found in assembly '{ShowAssemblyName(assemblyName)}' matching '{pluginClassName}'");
            }
            try {
                // plugins can have state, therefore we must manage them
                T result = (T) _plugins.FirstOrDefault(t => t.GetType() == pluginType);
                if (result == null) {
                    _plugins.Add(result = (T) Activator.CreateInstance(pluginType));
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

        public string RenderTestData([CanBeNull] string assemblyName, [CanBeNull] string rendererClassName, string rendererOptions, [NotNull] string baseFileName) {
            IDependencyRenderer renderer = GetOrCreatePlugin<IDependencyRenderer>(assemblyName, rendererClassName);

            IEnumerable<Item> items;
            IEnumerable<Dependency> dependencies;

            renderer.CreateSomeTestItems(out items, out dependencies);
            renderer.Render(this, dependencies, rendererOptions, baseFileName, IgnoreCase);

            RenderingDone = true;

            return renderer.GetMasterFileName(this, rendererOptions, baseFileName);
        }


        public void AddNegativeInputOption(string filePattern) {
            foreach (var spec in _inputSpecs) {
                spec.AddNegative(filePattern);
            }
        }

        public void CreateInputOption(string[] args, ref int i, string filePattern, string assembly, string readerFactoryClass) {
            if (readerFactoryClass == null) {
                throw new ApplicationException($"No reader class found for file pattern '{filePattern}'");
            }
            IReaderFactory readerFactory = GetOrCreatePlugin<IReaderFactory>(assembly, readerFactoryClass);
            _inputSpecs.Add(new InputFileOption(filePattern, readerFactory).AddNegative(i + 2 < args.Length && args[i + 1] == "-" ? args[i += 2] : null));
            InputFilesOrTestDataSpecified = true;
        }

        public void ShowAllPluginsAndTheirHelp<T>(string assemblyName, string filter) where T : IPlugin {
            IEnumerable<Type> pluginTypes = GetPluginTypes<T>(assemblyName);
            var matched = new List<Type>();
            foreach (var t in pluginTypes) {
                try {
                    T renderer = (T) Activator.CreateInstance(t);
                    string help = t.FullName + ":\r\n" + renderer.GetHelp(detailedHelp: false, filter: "");
                    if (help.IndexOf(filter ?? "", StringComparison.InvariantCultureIgnoreCase) >= 0) {
                        Log.WriteInfo(HELP_SEPARATOR + help + "\r\n");
                        matched.Add(t);
                    }
                } catch (Exception ex) {
                    Log.WriteError($"Cannot print help for renderer '{t.FullName}'; reason: {ex.Message}");
                }
            }
            switch (matched.Count) {
                case 0:
                    Log.WriteWarning($"Found no plugin types in '{ShowAssemblyName(assemblyName)}' match '{filter}'");
                    break;
                case 1:
                    Log.WriteInfo(HELP_SEPARATOR);
                    ShowDetailedHelp<T>(assemblyName, matched[0].FullName, "");
                    break;
                default:
                    Log.WriteInfo(HELP_SEPARATOR);
                    break;
            }
        }

        public void TransformTestData(string assembly, string transformerClass, string transformerOptions) {
            ITransformer transformer = GetOrCreatePlugin<ITransformer>(assembly, transformerClass);
            _dependenciesWithoutInputContextStack.Push(transformer.GetTestDependencies());

            var newDependenciesCollector = new List<Dependency>();
            transformer.Transform(this, "TestData", DependenciesWithoutInputContext, transformerOptions, "TestData",
                newDependenciesCollector);

            _dependenciesWithoutInputContextStack.Pop();
            _dependenciesWithoutInputContextStack.Push(newDependenciesCollector);
        }

        public int Transform([CanBeNull] string assembly, [NotNull] string transformerClass,
            [CanBeNull] string transformerOptions) {
            ReadAllNotYetReadIn();

            ITransformer transformer = GetOrCreatePlugin<ITransformer>(assembly, transformerClass);

            var newDependenciesCollector = new List<Dependency>();
            int result = Program.OK_RESULT;
            int sum;
            if (transformer.RunsPerInputContext) {
                foreach (var ic in _inputContexts.Values) {
                    result = Transform(transformerOptions, transformer, ic.Filename, ic.Dependencies, "dependencies in file " + ic.Filename, newDependenciesCollector, result);
                }
                result = Transform(transformerOptions, transformer, "", DependenciesWithoutInputContext, "generated dependencies", newDependenciesCollector, result);
                sum = 0;
                foreach (var ic in _inputContexts.Values) {
                    sum += ic.PushDependencies(newDependenciesCollector.Where(d => d.InputContext == ic));
                }
                sum +=
                    PushDependenciesWithoutInputContext(
                        newDependenciesCollector.Where(d => d.InputContext == null).ToArray());
            } else {
                result = transformer.Transform(this, "", GetAllDependencies().ToArray(), transformerOptions, "all dependencies",
                    newDependenciesCollector);
                sum = 0;
                sum += PushDependenciesWithoutInputContext(newDependenciesCollector.ToArray());
                // Also push on input contexts to keep all dependecy stacks synchronous
                foreach (var ic in _inputContexts.Values) {
                    sum += ic.PushDependencies(ic.Dependencies);
                }
            }
            Log.WriteInfo($"{sum} dependencies");

            return result;
        }

        private int Transform(string transformerOptions, ITransformer transformer, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string dependencySourceForLogging, List<Dependency> newDependenciesCollector, int result) {
            try {
                int r = transformer.Transform(this, dependenciesFilename, dependencies, transformerOptions,
                    dependencySourceForLogging, newDependenciesCollector);
                result = Math.Max(result, r);
            } catch (Exception ex) {
                Log.WriteError($"Error while transforming '{dependencySourceForLogging}': {ex.GetType().Name} - {ex.Message}");
                result = Program.EXCEPTION_RESULT;
            }
            return result;
        }

        private int PushDependenciesWithoutInputContext(Dependency[] dependencies) {
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

            _inputSpecs.Clear();

            RenderingDone = false;
            TransformingDone = false;

            ItemType.Reset();
            Item.Reset();
            AbstractDotNetAssemblyDependencyReader.Reset();

            GlobalVars.Clear();
        }

        public AbstractDotNetAssemblyDependencyReader GetDotNetAssemblyReaderFor(string usedAssembly) {
            return FirstMatchingReader(usedAssembly, _inputSpecs, needsOnlyItemTails: false);
        }

        private AbstractDotNetAssemblyDependencyReader FirstMatchingReader(string usedAssembly,
            List<InputOption> fileOptions, bool needsOnlyItemTails) {
            AbstractDotNetAssemblyDependencyReader result =
                fileOptions.SelectMany(i => i.CreateOrGetReaders(this, needsOnlyItemTails))
                    .OfType<AbstractDotNetAssemblyDependencyReader>()
                    .FirstOrDefault(r => r.AssemblyName == usedAssembly);
            return result;
        }

        public void ShowDetailedHelp<T>([CanBeNull] string assemblyName, [NotNull] string pluginClassName,
            [CanBeNull] string filter) where T : IPlugin {
            try {
                T plugin = GetOrCreatePlugin<T>(assemblyName, pluginClassName);
                Log.WriteInfo(HELP_SEPARATOR + plugin.GetType().FullName + ":\r\n" +
                              plugin.GetHelp(detailedHelp: true, filter: filter) + "\r\n");
            } catch (Exception ex) {
                Log.WriteError(
                    $"Cannot print help for plugin '{pluginClassName}' in assembly '{ShowAssemblyName(assemblyName)}'; reason: {ex.Message}");
            }
            HelpShown = true;
        }

        public void ConfigureTransformer([CanBeNull] string assemblyName, [NotNull] string transformerClass,
            [CanBeNull] string transformerOptions, bool forceReloadConfiguration) {
            try {
                ITransformer plugin = GetOrCreatePlugin<ITransformer>(assemblyName, transformerClass);
                plugin.Configure(this, transformerOptions, forceReloadConfiguration);
            } catch (Exception ex) {
                Log.WriteError(
                    $"Cannot configure plugin '{transformerClass}' in assembly '{ShowAssemblyName(assemblyName)}'; reason: {ex.Message}");
            }
        }

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
            Log.WriteInfo("Writing " + fullFileName);
            return new NamedTextWriter(fullFileName == "-" ? Console.Out : new StreamWriter(fullFileName), fullFileName);
        }

        public static bool IsConsoleOutFileName(string fileName) {
            return string.IsNullOrWhiteSpace(fileName) || fileName == "-";
        }

        public static ItemType GetItemType(string definition) {
            IEnumerable<string> parts = definition.Split('(', ':', ')').Select(s => s.Trim()).Where(s => s != "");
            string name = parts.First();

            return ItemType.New(name, parts.Skip(1).ToArray());
        }

        public void LogAboutNDependencies(int maxCount, [CanBeNull] string pattern) {
            DependencyMatch m = pattern == null ? null : new DependencyMatch(pattern, IgnoreCase);
            InputContext[] nonEmptyInputContexts = _inputContexts.Values.Where(ic => ic.Dependencies.Any()).ToArray();
            maxCount = Math.Max(3 * nonEmptyInputContexts.Length, maxCount);
            int depsPerContext = maxCount / (nonEmptyInputContexts.Length + 1);
            foreach (var ic in nonEmptyInputContexts) {
                foreach (var d in ic.Dependencies.Where(d => m == null || m.Matches(d)).Take(depsPerContext)) {
                    maxCount--;
                    Log.WriteInfo(d.AsDipStringWithTypes(false));
                }
                Log.WriteInfo("...");
            }
            foreach (var d in DependenciesWithoutInputContext.Where(d => m == null || m.Matches(d)).Take(maxCount)) {
                Log.WriteInfo(d.AsDipStringWithTypes(false));
            }
        }

        public void LogDependencyCount(string pattern) {
            DependencyMatch m = pattern == null ? null : new DependencyMatch(pattern, IgnoreCase);
            int sum = DependenciesWithoutInputContext.Count(d => m == null || m.Matches(d));
            foreach (var ic in _inputContexts.Values) {
                sum += ic.Dependencies.Count(d => m == null || m.Matches(d));
            }
            Log.WriteInfo(sum + " dependencies" + (m == null ? "" : " matching " + pattern));
            foreach (var d in GetAllDependencies().Where(d => m == null || m.Matches(d)).Take(3)) {
                Log.WriteInfo(d.AsDipStringWithTypes(false));
            }
        }

        public void LogItemCount(string pattern) {
            ItemMatch m = pattern == null ? null : new ItemMatch(GetExampleDependency(), pattern, IgnoreCase);
            IEnumerable<Item> allItems = new HashSet<Item>(GetAllDependencies().SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
            IEnumerable<Item> matchingItems = allItems.Where(i => ItemMatch.Matches(m, i));
            Log.WriteInfo(matchingItems.Count() + " items" + (m == null ? "" : " matching " + pattern));
            foreach (var i in matchingItems.Take(3)) {
                Log.WriteInfo(i.AsStringWithOrderAndType());
            }
        }

        public void ShowAllVars() {
            foreach (var v in GlobalVars.Keys.OrderBy(k => k)) {
                Log.WriteInfo($"-dd {v,-15} {GlobalVars[v]}");
            }
        }

        public string NormalizeLine([CanBeNull] string line) {
            if (line != null) {
                int commentStart = line.IndexOf("//", StringComparison.InvariantCulture);
                if (commentStart >= 0) {
                    line = line.Substring(0, commentStart);
                }
                return ExpandDefines(line.Trim()).Trim();
            } else {
                return null;
            }
        }

        public void Calculate(string varname, string assembly, string calculatorClass, IEnumerable<string> input) {
            ICalculator calculator = GetOrCreatePlugin<ICalculator>(assembly, calculatorClass);
            if (GlobalVars.ContainsKey(varname)) {
                throw new ArgumentException($"Variable {varname} is already defined");
            }
            try {
                string result = calculator.Calculate(input.ToArray());
                GlobalVars[varname] = result;
            } catch (Exception ex) {
                Log.WriteError($"Cannot compute value with ${calculatorClass}; reason: {ex.GetType().Name} '{ex.Message}'");
            }
        }
    }
}