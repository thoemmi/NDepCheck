using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NDepCheck.Reading;
using NDepCheck.Rendering;
using NDepCheck.Transforming;
using NDepCheck.WebServing;

namespace NDepCheck {
    public class GlobalContext {
        internal bool RenderingDone { get; set; }
        internal bool TransformingDone { get; set; }
        internal bool InputFilesSpecified { get; set; }
        internal bool HelpShown { get; private set; }

        public bool ShowUnusedQuestionableRules { get; set; }
        public bool ShowUnusedRules { get; set; }
        public bool IgnoreCase { get; set; }

        [NotNull] private readonly List<InputFileOption> _inputFileSpecs = new List<InputFileOption>();

        [NotNull]
        public Dictionary<string, string> GlobalVars { get; } = new Dictionary<string, string>();

        [NotNull, ItemNotNull] private readonly List<InputContext> _inputContexts = new List<InputContext>();

        private IEnumerable<Dependency> _dependenciesWithoutInputContext = Enumerable.Empty<Dependency>();

        [NotNull] private readonly List<IPlugin> _plugins = new List<IPlugin>();

        private WebServer _webServer;

        static GlobalContext() {
            // Initialize all built-in reader factories because they contain predefined ItemTypes
            foreach (var t in GetPluginTypes<IReaderFactory>("")) {
                Activator.CreateInstance(t);
            }
        }

        [NotNull]
        public IEnumerable<InputContext> InputContexts => _inputContexts;

        [NotNull]
        public List<InputFileOption> InputFileSpecs => _inputFileSpecs;

        public bool WorkLazily { get; set; }

        public void CreateInputOption(string filePattern, string negativeFilePattern, string assembly,
            string readerClass) {
            _inputFileSpecs.Add(new InputFileOption(filePattern, negativeFilePattern,
                GetOrCreatePlugin<IReaderFactory>(assembly, readerClass)));
        }

        public void SetDefine(string key, string value, string location) {
            if (GlobalVars.ContainsKey(key)) {
                if (GlobalVars[key] != value) {
                    throw new ApplicationException(
                        $"'{key}' cannot be redefined as '{value}' {location}");
                }
            } else {
                GlobalVars.Add(key, value);
            }
        }


        public string ExpandDefines(string s) {
            Dictionary<string, string> vars = GlobalVars;
            s = s ?? "";
            foreach (string key in vars.Keys.OrderByDescending(k => k.Length)) {
                s = Regex.Replace(s, @"\b" + key + @"\b", vars[key]);
            }
            return s;
        }


        public void ReadAllNotYetReadIn() {
            IEnumerable<AbstractDependencyReader> allReaders =
                InputFileSpecs.SelectMany(i => i.CreateOrGetReaders(this, false)).OrderBy(r => r.FileName);
            foreach (var r in allReaders) {
                InputContext inputContext = r.ReadOrGetDependencies(this, 0);
                if (inputContext != null) {
                    // Newly read input
                    _inputContexts.Add(inputContext);
                }
            }
        }

        public string RenderToFile([NotNull] string assemblyName, [NotNull] string rendererClassName,
            string rendererOptions, [CanBeNull] string fileName) {
            ReadAllNotYetReadIn();

            IEnumerable<Dependency> allDependencies = GetAllDependencies();
            IDependencyRenderer renderer = GetOrCreatePlugin<IDependencyRenderer>(assemblyName, rendererClassName);

            string masterFileName = renderer.Render(allDependencies, rendererOptions, fileName);
            RenderingDone = true;

            return masterFileName;
        }

        private IEnumerable<Dependency> GetAllDependencies() {
            return _inputContexts.SelectMany(ic => ic.Dependencies).Concat(_dependenciesWithoutInputContext).ToArray();
        }

        private T GetOrCreatePlugin<T>(string assemblyName, string pluginClassName) where T : IPlugin {
            IEnumerable<Type> pluginTypes = GetPluginTypes<T>(assemblyName);
            Type pluginType =
                pluginTypes.FirstOrDefault(
                    t => string.Compare(t.FullName, pluginClassName, StringComparison.InvariantCultureIgnoreCase) == 0) ??
                pluginTypes.FirstOrDefault(
                    t => string.Compare(t.Name, pluginClassName, StringComparison.InvariantCultureIgnoreCase) == 0);
            if (pluginType == null) {
                throw new ApplicationException(
                    $"No plugin type found in assembly {assemblyName} matching {pluginClassName}");
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
                    $"Cannot create {pluginClassName} from assembly {assemblyName} running in working directory {Environment.CurrentDirectory}; problem: " +
                    ex.Message, ex);
            }
        }

        private static IOrderedEnumerable<Type> GetPluginTypes<T>(string assemblyName) {
            try {
                Assembly pluginAssembly = string.IsNullOrWhiteSpace(assemblyName) || assemblyName == "."
                    ? typeof(GlobalContext).Assembly
                    : Assembly.LoadFrom(assemblyName);

                return
                    pluginAssembly.GetExportedTypes()
                        .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                        .OrderBy(t => t.FullName);
            } catch (Exception ex) {
                throw new ApplicationException($"Cannot load types from assembly {assemblyName}; reason: {ex.Message}");
            }
        }

        public string RenderTestData([NotNull] string assemblyName, [NotNull] string rendererClassName,
            string rendererOptions, [NotNull] string fileName) {
            IDependencyRenderer renderer = GetOrCreatePlugin<IDependencyRenderer>(assemblyName, rendererClassName);

            IEnumerable<Item> items;
            IEnumerable<Dependency> dependencies;

            renderer.CreateSomeTestItems(out items, out dependencies);

            string masterFileName = renderer.Render(dependencies, rendererOptions, fileName);

            RenderingDone = true;

            return masterFileName;
        }

        public void CreateInputOption(string[] args, ref int i, string filePattern, string assembly, string readerClass) {
            if (readerClass == null) {
                throw new ApplicationException(
                    $"No reader class found for file pattern {filePattern} - please specify explicitly with -h or -i option");
            }
            CreateInputOption(filePattern,
                negativeFilePattern: i + 2 < args.Length && args[i + 1] == "-" ? args[i += 2] : null, assembly: assembly,
                readerClass: readerClass);
            InputFilesSpecified = true;
        }

        public void ShowAllPluginsAndTheirHelp<T>(string assemblyName) {
            foreach (var t in GetPluginTypes<T>(assemblyName)) {
                try {
                    IDependencyRenderer renderer = (IDependencyRenderer) Activator.CreateInstance(t);
                    Log.WriteInfo("=============================================\r\n" + t.FullName + ":\r\n" +
                                  renderer.GetHelp(detailedHelp: false) + "\r\n");
                } catch (Exception ex) {
                    Log.WriteError("Cannot print help for Renderer " + t.FullName + "; reason: " + ex.Message);
                }
            }
            Log.WriteInfo("=============================================\r\n");
            HelpShown = true;
        }

        public void TransformTestData(string assembly, string transformerClass, string transformerOptions) {
            ITransformer transformer = GetOrCreatePlugin<ITransformer>(assembly, transformerClass);
            IEnumerable<Dependency> testData = transformer.GetTestDependencies();

            var newDependenciesCollector = new Dictionary<FromTo, Dependency>();
            transformer.Transform(this, "TestData", testData, transformerOptions, "TestData", newDependenciesCollector);

            _dependenciesWithoutInputContext = newDependenciesCollector.Values;
        }

        public int Transform(string assembly, string transformerClass, string transformerOptions) {
            ReadAllNotYetReadIn();

            ITransformer transformer = GetOrCreatePlugin<ITransformer>(assembly, transformerClass);

            var newDependenciesCollector = new Dictionary<FromTo, Dependency>();
            int result = Program.OK_RESULT;
            if (transformer.RunsPerInputContext) {
                foreach (var ic in _inputContexts) {
                    string dependencySourceForLogging = "dependencies in file " + ic.Filename;
                    int r = transformer.Transform(this, ic.Filename, ic.Dependencies, transformerOptions,
                        dependencySourceForLogging, newDependenciesCollector);
                    result = Math.Max(result, r);
                }
                {
                    int r = transformer.Transform(this, "", _dependenciesWithoutInputContext, transformerOptions,
                        "generated dependencies", newDependenciesCollector);
                    result = Math.Max(result, r);
                }
                foreach (var ic in _inputContexts) {
                    ic.SetDependencies(newDependenciesCollector.Values.Where(d => d.InputContext == ic));
                }
            } else {
                result = transformer.Transform(this, "", GetAllDependencies(), transformerOptions, "all dependencies",
                    newDependenciesCollector);
            }
            _dependenciesWithoutInputContext =
                newDependenciesCollector.Values.Where(d => d.InputContext == null).ToArray();

            return result;
        }

        public RegexOptions GetIgnoreCase() {
            return IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        }

        //[CanBeNull]
        //public string DefaultRuleSource { get; set; }

        public void Reset() {
            _inputContexts.Clear();
            _dependenciesWithoutInputContext = Enumerable.Empty<Dependency>();

            _inputFileSpecs.Clear();

            RenderingDone = false;
            TransformingDone = false;
        }

        public AbstractDotNetAssemblyDependencyReader GetDotNetAssemblyReaderFor(string usedAssembly) {
            return FirstMatchingReader(usedAssembly, _inputFileSpecs, needsOnlyItemTails: false);
        }

        private AbstractDotNetAssemblyDependencyReader FirstMatchingReader(string usedAssembly,
            List<InputFileOption> fileOptions, bool needsOnlyItemTails) {
            AbstractDotNetAssemblyDependencyReader result =
                fileOptions.SelectMany(i => i.CreateOrGetReaders(this, needsOnlyItemTails))
                    .OfType<AbstractDotNetAssemblyDependencyReader>()
                    .FirstOrDefault(r => r.AssemblyName == usedAssembly);
            return result;
        }

        public void ShowDetailedHelp<T>(string assembly, string pluginClassName) where T : IPlugin {
            try {
                T plugin = GetOrCreatePlugin<T>(assembly, pluginClassName);
                Log.WriteInfo("=============================================\r\n" + plugin.GetType().FullName + ":\r\n" +
                              plugin.GetHelp(detailedHelp: false) + "\r\n");
            } catch (Exception ex) {
                Log.WriteError(
                    $"Cannot print help for plugin {pluginClassName} in assembly {assembly}; reason: {ex.Message}");
            }
            HelpShown = true;
        }

        public void ConfigureTransformer(string assembly, string transformerClass, string transformerOptions) {
            try {
                ITransformer plugin = GetOrCreatePlugin<ITransformer>(assembly, transformerClass);
                plugin.Configure(this, transformerOptions);
            } catch (Exception ex) {
                Log.WriteError(
                    $"Cannot configure plugin {transformerClass} in assembly {assembly}; reason: {ex.Message}");
            }
        }

        public class NamedTextWriter : IDisposable {
            public NamedTextWriter(TextWriter writer, string fileName) {
                Writer = writer;
                FileName = fileName;
            }

            public TextWriter Writer { get; }
            public string FileName { get; }

            public void Dispose() {
                Writer?.Dispose();
            }
        }

        public static NamedTextWriter CreateTextWriter(string fileName, string extension = null) {
            if (fileName == null || IsConsoleOutFileName(fileName)) {
                return new NamedTextWriter(Console.Out, "-");
            } else {
                if (extension != null) {
                    fileName = Path.ChangeExtension(fileName, extension);
                }
                Log.WriteInfo("Writing " + fileName);
                return new NamedTextWriter(new StreamWriter(fileName), "-");
            }
        }

        public static bool IsConsoleOutFileName(string fileName) {
            return string.IsNullOrWhiteSpace(fileName) || fileName == "-";
        }

        public static ItemType GetItemType(string definition) {
            IEnumerable<string> parts = definition.Split('(', ':', ')').Select(s => s.Trim()).Where(s => s != "");
            string name = parts.First();

            return ItemType.New(name, parts.Skip(1).ToArray());

            ////return ALL_READER_FACTORIES.SelectMany(f => f.GetDescriptors()).FirstOrDefault(d => d.Name == name)
            ////    ?? ALL_READER_FACTORIES.OfType<DotNetAssemblyDependencyReaderFactory>().First().GetOrCreateDotNetType(name, parts.Skip(1));
        }

        public void StartWebServer(Program program, string port, string fileDirectory) {
            if (_webServer != null) {
                throw new ApplicationException("Cannot start webserver if one is already running");
            }
            _webServer = new WebServer(program, this, port, fileDirectory);
            _webServer.Start();
        }

        public void StopWebServer() {
            _webServer?.Stop();
        }
    }
}