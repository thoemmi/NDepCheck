using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gibraltar;
using JetBrains.Annotations;
using NDepCheck.Reading;
using NDepCheck.Rendering;
using NDepCheck.Transforming;
using NDepCheck.Transforming.ViolationChecking;

namespace NDepCheck {
    /// <remarks>
    /// Main class of NDepCheck.
    /// All static methods may run in parallel.
    /// </remarks>
    public class Program {
        private const string VERSION = "V.3.3";

        public const int OK_RESULT = 0;
        public const int OPTIONS_PROBLEM = 1;
        public const int DEPENDENCIES_NOT_OK = 3; // Used in InputContext.CheckDependencies - mhm - TODO: Concept for Transformers that check ...
        public const int FILE_NOT_FOUND_RESULT = 4;
        public const int NO_RULE_GROUPS_FOUND = 5;
        public const int NO_RULE_SET_FOUND_FOR_FILE = 6;
        public const int EXCEPTION_RESULT = 7;

        public int Run(string[] args, GlobalContext globalContext, [CanBeNull] List<string> writtenMasterFiles) {
            Log.SetLevel(Log.Level.Standard);

            if (args.Length == 0) {
                return UsageAndExit("No options or files specified");
            }

            bool ranAsWebServer = false;
            int result = OK_RESULT;

            try {
                for (int i = 0; i < args.Length; i++) {
                    string arg = args[i];

                    if (Options.ArgMatches(arg, '?')) {
                        // -?                      Write help
                        return UsageAndExit(null, extensiveHelp: false);
                    } else if (Options.ArgMatches(arg, 'a')) {
                        // -a                      Write extensive help
                        return UsageAndExit(null, extensiveHelp: true);
                    } else if (Options.ArgMatches(arg, 'b')) {
                        // -b        Stop execution here; useful in -o file
                        Log.WriteInfo("---- Stop reading options (-b)");
                        goto DONE;
                    } else if (Options.ArgMatches(arg, 'c')) {
                        // -c                      Ignore case in rules
                        globalContext.IgnoreCase = true;
                    } else if (arg == "-debug" || arg == "/debug") {
                        // -debug                  Start .Net debugger
                        Debugger.Launch();
                    } else if (Options.ArgMatches(arg, 'e', 'f')) {
                        // -e     assembly transformer { options }
                        // -f              transformer { options }
                        string assembly, rendererClass;
                        if (ExtractAssemblyAndClass(args, new[] { 'e' }, 'f', out assembly, out rendererClass, ref i)) {
                            globalContext.ShowAllPluginsAndTheirHelp<IDependencyRenderer>(assembly);
                        } else {
                            string transformerOptions = Options.ExtractNextValue(args, ref i);
                            globalContext.ConfigureTransformer(assembly, rendererClass, transformerOptions);
                        }
                    } else if (Options.ArgMatches(arg, 'h', 'i')) {
                        // -h     assembly reader filepattern [- filepattern]
                        // -i              reader filepattern [- filepattern]
                        string assembly, readerClass;
                        if (ExtractAssemblyAndClass(args, assemblyAndClassOpt: new[] { 'h' }, classOnlyOpt: 'i',
                            assembly: out assembly, @class: out readerClass, i: ref i)) {
                            globalContext.ShowAllPluginsAndTheirHelp<IReaderFactory>(assembly);
                        } else {
                            string filePattern = Options.ExtractNextValue(args, ref i);
                            if (Options.ArgMatches(filePattern, '?')) {
                                globalContext.ShowDetailedHelp<IDependencyRenderer>(assembly, readerClass);
                                i++;
                            } else {
                                globalContext.CreateInputOption(args, ref i, filePattern, assembly, readerClass);
                            }
                        }
                    } else if (Options.ArgMatches(arg, 'j')) {
                        // -j      filepattern [- filepattern]
                        string filePattern = Options.ExtractOptionValue(args, ref i);
                        globalContext.CreateInputOption(args, ref i, filePattern, assembly: "",
                            readerClass: IsDllOrExeFile(filePattern)
                                ? typeof(DotNetAssemblyDependencyReaderFactory).FullName
                                : IsDipFile(filePattern) ? typeof(DipReaderFactory).FullName : null);
                    } else if (Options.ArgMatches(arg, 'k')) {
                        string cmd = Options.ExtractOptionValue(args, ref i);
                        try {
                            if (new Process {StartInfo = new ProcessStartInfo(cmd)}.Start()) {
                                Log.WriteInfo($"Started process '{cmd}'");
                            } else {
                                Log.WriteError($"Could not start process '{cmd}'");
                            }
                        } catch (Exception ex) {
                            Log.WriteError($"Could not start process '{cmd}'; reason: {ex.Message}");
                            result = EXCEPTION_RESULT;
                        }
                    } else if (Options.ArgMatches(arg, 'l')) {
                        // -l                      Execute readers and transformers lazily 
                        //                         (lazy reading and transforming NOT YET IMPLEMENTED)
                        globalContext.WorkLazily = true;
                    } else if (Options.ArgMatches(arg, 'm')) {
                        // -m name value           Define name as value; a redefinition with a different value is not possible
                        string varname = Options.ExtractOptionValue(args, ref i);
                        string varvalue = Options.ExtractNextValue(args, ref i);
                        globalContext.SetDefine(varname, varvalue, "after -m option");

                        globalContext.GlobalVars[varname] = varvalue;
                    } else if (Options.ArgMatches(arg, 'o')) {
                        // -o fileName             Read options from file
                        string fileName = Options.ExtractOptionValue(args, ref i);
                        result = RunFrom(fileName, globalContext, writtenMasterFiles);

                        // file is also an input file - and if there are no input files in -o, the error will come up there.
                        globalContext.InputFilesOrTestDataSpecified = true;
                    } else if (Options.ArgMatches(arg, 'p', 'q', 'r')) {
                        // -p     assembly renderer [{ options }] fileName
                        // -q     assembly renderer [{ options }] fileName
                        // -r              renderer [{ options }] fileName

                        string assembly, rendererClass;
                        if (ExtractAssemblyAndClass(args, new[] { 'p', 'q' }, 'r', out assembly, out rendererClass, ref i)) {
                            globalContext.ShowAllPluginsAndTheirHelp<IDependencyRenderer>(assembly);
                        } else {
                            string classOptions, fileName;
                            if (ExtractClassOptions(args, out classOptions, out fileName, ref i)) {
                                globalContext.ShowDetailedHelp<IDependencyRenderer>(assembly, rendererClass);
                                i++;
                            } else if (Options.ArgMatches(arg, 'p')) {
                                string fn = globalContext.RenderTestData(assembly, rendererClass, classOptions, fileName);
                                writtenMasterFiles?.Add(fn);
                                globalContext.InputFilesOrTestDataSpecified = true;
                            } else {
                                string fn = globalContext.RenderToFile(assembly, rendererClass, classOptions, fileName);
                                writtenMasterFiles?.Add(fn);
                            }
                        }
                    } else if (Options.ArgMatches(arg, 's', 't', 'u')) {
                        // -s     assembly transformer [{ options }]
                        // -t     assembly transformer [{ options }]
                        // -u              transformer [{ options }]

                        string assembly, transformerClass;
                        if (ExtractAssemblyAndClass(args, new[] { 's', 't' }, 'u', out assembly, out transformerClass, ref i)) {
                            globalContext.ShowAllPluginsAndTheirHelp<ITransformer>(assembly);
                        } else {
                            if (i + 1 >= args.Length || Options.ArgMatches(args[i + 1], '?')) {
                                globalContext.ShowDetailedHelp<ITransformer>(assembly, transformerClass);
                                i++;
                            } else {
                                string transformerOptions = i < args.Length - 1 && args[i + 1].StartsWith("{")
                                    ? Options.ExtractNextValue(args, ref i)
                                    : "";
                                if (Options.ArgMatches(arg, 's')) {
                                    globalContext.TransformTestData(assembly, transformerClass, transformerOptions);
                                    globalContext.InputFilesOrTestDataSpecified = true;
                                } else {
                                    result = globalContext.Transform(assembly, transformerClass, transformerOptions);
                                }
                            }
                        }
                    } else if (Options.ArgMatches(arg, 'v')) {
                        // -v        verbose output
                        Log.SetLevel(Log.Level.Verbose);
                        WriteVersion();
                    } else if (Options.ArgMatches(arg, 'w')) {
                        // -w        chatty output
                        Log.SetLevel(Log.Level.Chatty);
                        WriteVersion();
                    } else if (Options.ArgMatches(arg, 'x')) {
                        // -x port fileDirectory      Run as webserver
                        string port = Options.ExtractOptionValue(args, ref i);
                        string fileDirectory = Options.ExtractNextValue(args, ref i);
                        globalContext.StartWebServer(this, port, fileDirectory);
                        ranAsWebServer = true;
                    } else if (Options.ArgMatches(arg, 'y')) {
                        // -y                         Stop webserver
                        globalContext.StopWebServer();
                    } else if (Options.ArgMatches(arg, 'z')) {
                        // -z        Remove all dependencies and graphs and caches
                        Log.WriteInfo("---- Reset of input options (-z)");

                        Intern<ItemType>.Reset();
                        Intern<ItemTail>.Reset();
                        Intern<Item>.Reset();
                        AbstractDotNetAssemblyDependencyReader.Reset();
                        globalContext.Reset();
                    } else if (IsDllOrExeFile(arg)) {
                        globalContext.CreateInputOption(args, ref i, arg, "", typeof(DotNetAssemblyDependencyReaderFactory).FullName);
                    } else if (IsDipFile(arg)) {
                        globalContext.CreateInputOption(args, ref i, arg, "", typeof(DipReaderFactory).FullName);
                    } else {
                        return UsageAndExit("Unsupported option '" + arg + "'");
                    }
                }
            } catch (ArgumentException ex) {
                return UsageAndExit(ex.Message);
            }

            if (!globalContext.InputFilesOrTestDataSpecified && !ranAsWebServer && !globalContext.HelpShown) {
                return UsageAndExit("No input files specified");
            }

            if (result == OK_RESULT && !globalContext.TransformingDone && !globalContext.RenderingDone) {
                // Default action at end if nothing was done
                globalContext.ReadAllNotYetReadIn();
                result = globalContext.Transform("", typeof(ViolationsChecker).FullName, "");
                globalContext.RenderToFile(".", typeof(RuleViolationRenderer).FullName, "", null);
            }

            DONE:

            if (Log.IsVerboseEnabled) {
                Log.WriteInfo("Completed with exitcode " + result);
            }

            return result;
        }

        private static bool IsDipFile(string arg) {
            return arg.EndsWith(".dip", StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool IsDllOrExeFile(string arg) {
            return arg.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                   arg.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns><c>true</c> if classOptions is /? for 'help'</returns>
        private static bool ExtractClassOptions(string[] args, out string classOptions, out string fileName, ref int i) {
            string o = Options.ExtractNextValue(args, ref i);
            if (o == null || Options.ArgMatches(o, '?')) {
                classOptions = "";
                fileName = "";
                return true;
            } else if (o.StartsWith("{")) {
                classOptions = o;
                fileName = Options.ExtractNextValue(args, ref i);
                return false;
            } else {
                classOptions = "";
                fileName = o;
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns><c>true</c> if option after assembly is /? for 'help'</returns>
        private static bool ExtractAssemblyAndClass([NotNull] string[] args, char[] assemblyAndClassOpt, char classOnlyOpt,
                                                    [NotNull] out string assembly, out string @class, ref int i) {
            string arg = args[i];
            if (Options.ArgMatches(arg, assemblyAndClassOpt)) {
                assembly = Options.ExtractOptionValue(args, ref i);
                @class = Options.ExtractNextValue(args, ref i);
            } else if (Options.ArgMatches(arg, classOnlyOpt)) {
                assembly = ".";
                @class = Options.ExtractOptionValue(args, ref i);
            } else {
                assembly = ".";
                @class = null;
            }
            return @class == null || Options.ArgMatches(@class, '?', 'h');
        }

        private int RunFrom([NotNull] string fileName, [NotNull] GlobalContext state, [CanBeNull] List<string> writtenMasterFiles) {
            int lineNo = 0;
            try {
                var args = new List<string>();
                bool inBraces = false;
                using (var sr = new StreamReader(fileName)) {
                    for (;;) {
                        lineNo++;
                        string line = sr.ReadLine();
                        if (line == null) {
                            break;
                        }
                        string trimmedLine = Regex.Replace(line, "//.*$", "").Trim();
                        IEnumerable<string> splitLine = trimmedLine.Split(' ', '\t').Select(s => s.Trim()).Where(s => s != "");

                        if (splitLine.Any() && splitLine.Last() == "{") {
                            args.AddRange(splitLine);
                            inBraces = true;
                        } else if (splitLine.Any() && splitLine.First() == "}") {
                            inBraces = false;
                            args.AddRange(splitLine.Select(state.ExpandDefines));
                        } else if (!inBraces) {
                            args.AddRange(splitLine.Select(state.ExpandDefines));
                        } else {
                            args.Add(line);
                        }
                    }
                }

                var locallyWrittenFiles = new List<string>();
                string previousCurrentDirectory = Environment.CurrentDirectory;
                try {
                    Environment.CurrentDirectory = Path.GetDirectoryName(Path.GetFullPath(fileName));
                    return Run(args.ToArray(), state, locallyWrittenFiles);
                } finally {
                    writtenMasterFiles?.AddRange(locallyWrittenFiles.Select(Path.GetFullPath));
                    Environment.CurrentDirectory = previousCurrentDirectory;
                }
            } catch (Exception ex) {
                Log.WriteError("Cannot run commands in " + fileName + " (" + ex.Message + ")", fileName, lineNo);
                return EXCEPTION_RESULT;
            }
        }

        private static int UsageAndExit(string message, int exitValue = OPTIONS_PROBLEM, bool extensiveHelp = false) {
            WriteVersion();
            Console.Out.WriteLine(
                @"
Usage:
   NDepCheck <option>...

Typical uses:

* Check dependencies in My.DLL; My.dll.dep is somewhere below SourceDir:
      NDepCheck /d=SourceDir My.dll

* Produce graph of dependencies in My.DLL via built-in renderer:
      NDepCheck /d=SourceDir My.DLL _______ (UNDER WORK)


* Produce graph of dependencies in My.DLL via dot (graphviz):
      NDepCheck /d=SourceDir My.DLL _______ (UNDER WORK)
      dot -Tgif -oMy.gif My.dot

All messages of NDepCheck are written to Console.Out.

Options overview:
    Options can be written with leading - or /

     READ
-h     assembly reader filepattern [- filepattern]
-i              reader filepattern [- filepattern]
[-j]                   filepattern [- filepattern]

       Built-in readers:
        - DipReader      (.dip)
        - AssemblyReader (.dll, .exe)
       
     HELP FOR ALL READERS
-h     assembly -?
-i              -?

     HELP FOR A SINGLE READER
-h     assembly reader -?
-i              reader -?

     CONFIGURE TRANSFORMER
-e     assembly transformer { options }
-f              transformer { options }

     TRANSFORM
-t     assembly transformer [{ options }]
-u              transformer [{ options }]
     
       Built-in transformers:  
       - CheckDependencies [-q | -u]
           Copies dependency's count to bad count or 
           questionable count depending on matched rules
                -q show questionable rules
                -u show all unused rules
       - Projector
           Replaces all edges by edges computed from
           projections (! or % rules)
       - (AssociativeHull - UNDER WORK)
            adds all associative edges
            cts are set to 1;0;0
       - (RemoveTransitiveEdges - UNDER WORK)
            Hide edges
       - (RemoveLocalLoops - UNDER WORK)
            Hide edges
       - (KeepOnlyCycleEdges - UNDER WORK)
            Hide edges

     TRANSFORM TESTDATA
-s     assembly transformer [{ options }]

     HELP FOR ALL TRANSFORMERS
-t     assembly -?
-u              -?
     
     HELP FOR A SINGLE TRANSFORMER
-t     assembly transformer -?
-u              transformer -?
     
     RENDER
-q     assembly renderer [{ options }] file
-r              renderer [{ options }] file

                Built-in renderers:
                - DipWriter
                - DotRenderer [-e edgelength]
                    -e
                - MatrixRenderer1
                - MatrixRenderer2
                - MatrixGraphicsRenderer  [-x] [-y] [-b regex] [-t text] [-w width] [-h height] [-f height]
                    -t text
                    -w width
                    -h height
                    -f height
                    -x
                    -y
                    -b regex
                - ModuleAndInterfaceGraphicsRenderer [-t text] [-w width] [-h height] [-f height]
                - RuleViolationRenderer [-x]
                        -x as XML output
     
     RENDER TESTDATA
-p     assembly renderer [{ options }] file

     HELP FOR ALL RENDERERS
-q     assembly -?
-r              -?

     HELP FOR A SINGLE RENDERER
-q     assembly renderer -?
-r              renderer -?

     OTHER
-?                Write help
-a                Write extensive help  
-b                Stop execution here; useful in -o file
-c                Ignore case in rules
-debug            Start .Net debugger 
-k cmd            Run command; useful for opening an HTML file after creating it
-l                Execute readers and transformers lazily (lazy reading and transforming NOT YET IMPLEMENTED)
-m name value     Define name as value; a redefinition with a different value is not possible
-o file           Read options from file
-v                Verbose
-w                Chatty
-x port directory Start web server
-y                Stop web server
-z                Reset state
");

            if (message != null) {
                Log.WriteError(message);
                Log.WriteInfo("");
            }

            if (extensiveHelp) {
                Console.Out.WriteLine(@"

############# NOT YET UPDATED ##################

   /_=<directory>    For each assembly file A.dll, look for corresponding 
         rule file A.dll.dep in this directory (multiple /d options are 
         supported). This is especially useful with + lines.

   /d=<directory>    Like /_, but also look in all subdirectories. Mixing
         /_ and /_ options is supported.

   /f=<rule file>    Use this rule file if no matching rule file is found
         via /_ and /d.  This is espeically useful if no /s and /d options
         are specified. __________________-

   /i[=<N>]        For each illegal edge (i.e., edge not allowed by 
         the dependency file), show an example of a concrete illegal 
         dependency in the DOT graph. N is the maximum width of strings 
         used; the default is 80. Graphs can become quite cluttered 
         with this option.

   /m[=N]   Specifies the maximum number of concurrent threads to use. 
         If you don't include this switch, the default value is 1. If
         you include this switch without specifying a value, NDepCheck
         will use up to the number of processors in the computer.

############# UPDATED ##################

/v    Verbose. Shows regular expressions used for checking and 
         all checked dependencies. Attention: Place /v BEFORE any
         /d, /s, or /x option to see the regular expressions.
         Produces lots of output.

   /y    Even more debugging output.

   /debug   Start with debugger.

Assemblyspecs - one of the following:
    
    simplefileName      the assembly is checked.
                        e.g. ProjectDir\bin\MyProject.Main.dll

    filepattern         all matching assemblies are checked.
                        e.g. bin\MyProject.*.dll 

    directory           all .DLL and .EXE files in the directory are checked.
                        e.g. MyProject\bin\Debug

    @fileName           lines are read as assembly fileNames and checked.
                        The file may contain empty lines, which are ignored.
                        e.g. @MyListOfFiles.txt

    <one of the above> /e <one of the above>            
                        The files after the /e are excluded from checking.
                        e.g. MyProject.*.dll /e *.vshost.*

Dependecies:_

A dependency describes that some 'using item' uses another 'used item'.

Standard .Net dependencies:

    A standard dependency as read from a .Net assembly has the following
    format:

namespace:class:assembly_name;assembly_version;assembly_culture:member_name;member_sort

    where member_sort is usually empty; but for properties, it is either
    'get' or 'set' on the using side.

Rules files:
    Rule files contain rule definition commands. Here is a simple example

        $ DOTNETCALL   ---> DOTNETCALL 

        // Each assembly can use .Net
        ::**           --->  ::mscorlib
        ::**           --->  ::(System|Microsoft).**

        // Each assembly can use everything in itself (a coarse architecture)
        ::(Module*)**  --->  ::\1

        // Module2 can use Module1
        ::Module2**    --->  ::Module1**

        // Test assemblies can use anything
        ::*Test*.dll   --->  ::**


    The following commands are supported in rule files:

           empty line            ... ignored
           // comment            ... ignored
           # comment             ... ignored

           + filepath            ... include rules from that file. The path
                                     is interpreted relative to the current
                                     rule file.

           NAME := pattern       ... define abbreviation which is replaced
                                     in patterns before processing. NAME
                                     must be uppercase only (but it can
                                     contain digits, underscores etc.).
                                     Longer names are preferred to shorter
                                     ones during replacement. The pattern
                                     on the right side can in turn use 
                                     abbreviations. Abbreviation processing
                                     is done before all reg.exp. replacements
                                     described below.
                                     If an abbreviation definition for the 
                                     same name is encountered twice, it must
                                     define exactly the same value.

           pattern ---> pattern  ... allowed dependency. The second
                                     pattern may contain back-references
                                     of the form \1, \2 etc. that are
                                     matched against corresponding (...)
                                     groups in the first pattern.

           pattern ---! pattern  ... forbidden dependency. This can be used
                                     to exclude certain possibilities for
                                     specific cases instead of writing many
                                     ""allowed"" rules.

           pattern ---? pattern  ... questionable dependency. If a dependency
                                     matches such a rule, a warning will be
                                     emitted. This is useful for rules that
                                     should be removed, but have to remain
                                     in place for pragmatic reasons (only
                                     for some time, it is hoped).

           pattern {             ... aspect rule set. All dependencies whose
               --->,                 left side matches the pattern must
               ---?, and             additionally match one of the rules.
               ---! rules            This is very useful for defining
           }                         partial rule sets that are orthogonal to
                                     the global rules (which must describe
                                     all dependencies in the checked
                                     assemblies).

           NAME :=
               <arbitrary lines except =:>
           =:                    ... definition of a rule macro. The
                                     arbitrary lines can contain the strings
                                     \L and \R, which are replaced with the
                                     corresponding patterns from the macro 
                                     use. NAME need not consist of letters
                                     only; also names like ===>, :::>, +++>
                                     etc. are allowed and quite useful.
                                     However, names must not be ""too
                                     similar"": If repeated characters are
                                     are replaced with a single one, they must
                                     still be different; hence, ===> and ====>
                                     are ""too similar"" and lead to an error.
                                     As with abbreviations, if a macro 
                                     definition for the same name is 
                                     encountered twice, it must define 
                                     exactly the same value.

           pattern NAME pattern  ... Use of a defined macro.

           % pattern (with at least one group) 
                                 ... Define output in DAG graph (substring
                                     matching first group is used as label).
                                     If the group is empty, the dependency
                                     is not shown in the graph.
                                     Useful only with /d option.

         For an example of a dependency file, see near end of this help text.

         A pattern is a list of subpatterns separated by colons
           subpattern:subpattern:...
         where a subpattern can be a list of basepatterns separated by semicolons:
           basepattern;subpattern;...
         A basepattern, finally, can be one of the following:
           ^regexp$
           ^regexp
           regexp$
           fixedstring
           wildcardpath, which contains . (or /), * and ** with the following
                         meanings:
               .       is replaced with the reg.exp. [.] (matches single period)
               *       is replaced with the reg.exp. for an <ident> (a ""name"")
               **      is usually replaced with <ident>(?:.<ident>)* (a 
                       ""path"").

Exit codes:
   0    All dependencies ok (including questionable rules).
   1    Usage error.
   2    Cannot load dependency file (syntax error or file not found).
   3    Dependencies not ok.
   4    Assembly file specified as argument not found.
   5    Other exception.
   6    No dependency file found for an assembly in /d and /s 
        directories, and /x not specified.

############# REST NOT YET UPDATED ##################

Example of a dependency file with some important dependencies (all
using the wildcardpath syntax):

   // Every class may use all classes from its own namespace.
        (**).* ---> \1.*

   // Special dependency for class names without namespace
   // (the pattern above will not work, because it contains a
   // period): A class from the global namespace may use
   // all classes from that namespace.
        * ---> *

   // Every class may use all classes from child namespaces
   // of its own namespace.
        (**).* ---> \1.**.*

   // Every class may use all of System.
        ** ---> System.**

   // Use ALL as abbreviation for MyProgram.**
        ALL := MyProgram.**

   // All MyProgram classes must not use Windows Forms
   // (even though in principle, all classes may use all of 
   // System according to the previous ---> rule).
        ALL ---! System.Windows.Forms.**

   // All MyProgram classes may use classes from antlr.
        ALL ---> antlr.**

   // Special methods must only call special methods
   // and getters and setters.
   **::*SpecialMethod* {
      ** ---> **::*SpecialMethod*
      ** ---> **::get_*
      ** ---> **::set_
   }

   // In DAG output, identify each object by its path (i.e.
   // namespace).
        ! (**).*

   // Classes without namespace are identified by their class name:
        ! (*)

   // Classes in System.* are identified by the empty group, i.e.,
   // they (and arrows reaching them) are not shown at all.
        ! ()System.**

   // Using % instead of ! puts the node in the 'outer layer', where
   // only edges to the inner layer are drawn.
            ");

            }
            return exitValue;
        }

        private static void WriteVersion() {
            Log.WriteInfo("NDepCheck " + VERSION + " (c) HMMüller, Th.Freudenberg 2006...2017");
        }

        /// <summary>
        /// The static Main method.
        /// </summary>
        public static int Main(string[] args) {
            Log.Logger = new ConsoleLogger();

            var program = new Program();
            try {
                return program.Run(args, new GlobalContext(), null);
            } catch (FileNotFoundException ex) {
                Log.WriteWarning(ex.Message);
                return FILE_NOT_FOUND_RESULT;
            } catch (Exception ex) {
                Log.WriteError("Exception occurred: " + ex.Message + " (" + ex.GetType().FullName + ")");
                if (Log.IsChattyEnabled) {
                    Console.WriteLine(ex);
                }
                return EXCEPTION_RESULT;
            } finally {
                // Main may be called multiple times; therefore we clear all caches
                Intern.ResetAll();
            }
        }
    }
}
