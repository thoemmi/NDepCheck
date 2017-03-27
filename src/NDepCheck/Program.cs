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
    // TODO: The following things should be more beautiful:
    // TODO: - The option "commands" are not orthogonal - e.g., there is no separate command for "CheckViolations"
    // TODO: - Output is arbitrary - "Analyzing" writes a time (in ms) afterwards, reducing does not (even though it is often slower)

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

        public int Run(string[] args, GlobalContext globalContext) {
            Log.SetLevel(Log.Level.Standard);

            if (args.Length == 0) {
                return UsageAndExit("No options or files specified");
            }

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
                            result = globalContext.ShowAllPluginsAndTheirHelp<IDependencyRenderer>(assembly);
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
                            } else {
                                globalContext.CreateInputOption(args, ref i, filePattern, assembly, readerClass);
                            }
                        }
                    } else if (Options.ArgMatches(arg, 'j')) {
                        // -j      filepattern [- filepattern]
                        string filePattern = Options.ExtractNextValue(args, ref i);
                        globalContext.CreateInputOption(args, ref i, filePattern, assembly: "",
                            readerClass: IsDllOrExeFile(filePattern)
                                ? typeof(DotNetAssemblyDependencyReaderFactory).FullName
                                : IsDipFile(filePattern) ? typeof(DipReaderFactory).FullName : null);
                    } else if (Options.ArgMatches(arg, 'l')) {
                        // -l                      Execute readers and transformers lazily 
                        //                         (lazy reading and transforming NOT YET IMPLEMENTED)
                        globalContext.WorkLazily = true;
                    } else if (Options.ArgMatches(arg, 'm')) {
                        // -m name value           Define name as value; a redefinition with a different value is not possible
                        string varname = Options.ExtractOptionValue(args, ref i);
                        string varvalue = Options.ExtractNextValue(args, ref i);
                        globalContext.GlobalVars[varname] = varvalue;
                    } else if (Options.ArgMatches(arg, 'o')) {
                        // -o filename             Read options from file
                        string filename = Options.ExtractOptionValue(args, ref i);
                        result = RunFrom(filename, globalContext);

                        // file is also an input file - and if there are no input files in -o, the error will come up there.
                        globalContext.InputFilesSpecified = true;
                    } else if (Options.ArgMatches(arg, 'p', 'q', 'r')) {
                        // -p     assembly renderer [{ options }] filename
                        // -q     assembly renderer [{ options }] filename
                        // -r              renderer [{ options }] filename

                        string assembly, rendererClass;
                        if (ExtractAssemblyAndClass(args, new[] { 'p', 'q' }, 'r', out assembly, out rendererClass, ref i)) {
                            result = globalContext.ShowAllPluginsAndTheirHelp<IDependencyRenderer>(assembly);
                        } else {
                            string classOptions, filename;
                            if (ExtractClassOptions(args, out classOptions, out filename, ref i)) {
                                globalContext.ShowDetailedHelp<IDependencyRenderer>(assembly, rendererClass);
                            } else if (Options.ArgMatches(arg, 'p')) {
                                globalContext.RenderTestData(assembly, rendererClass, classOptions, filename);
                            } else {
                                globalContext.RenderToFile(assembly, rendererClass, classOptions, filename);
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
                            if (Options.ArgMatches(args[i], '?')) {
                                globalContext.ShowDetailedHelp<IDependencyRenderer>(assembly, transformerClass);
                            } else {
                                string transformerOptions = i < args.Length - 1 && args[i + 1].StartsWith("{")
                                    ? Options.ExtractNextValue(args, ref i)
                                    : "";
                                if (Options.ArgMatches(arg, 's')) {
                                    globalContext.TransformTestData(assembly, transformerClass, transformerOptions);
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

            if (!globalContext.InputFilesSpecified) {
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
        private static bool ExtractClassOptions(string[] args, out string classOptions, out string filename, ref int i) {
            string o = Options.ExtractNextValue(args, ref i);
            if (Options.ArgMatches(o, '?')) {
                classOptions = "";
                filename = "";
                return true;
            } else if (o.StartsWith("{")) {
                classOptions = o;
                filename = Options.ExtractNextValue(args, ref i);
                return false;
            } else {
                classOptions = "";
                filename = o;
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

        private int RunFrom(string filename, GlobalContext state) {
            int lineNo = 0;
            try {
                var args = new List<string>();
                bool splitLines = true;
                using (var sr = new StreamReader(filename)) {
                    for (;;) {
                        lineNo++;
                        string line = sr.ReadLine();
                        if (line == null) {
                            break;
                        }
                        string trimmedLine = Regex.Replace(line, "//.*$", "").Trim();
                        IEnumerable<string> a = trimmedLine.Split(' ', '\t').Select(s => s.Trim()).Where(s => s != "");

                        if (a.Any() && a.Last() == "{") {
                            args.AddRange(a);
                            splitLines = false;
                        } else if (a.Any() && a.First() == "}") {
                            splitLines = true;
                            args.AddRange(a);
                        } else if (splitLines) {
                            args.AddRange(a);
                        } else {
                            args.Add(line);
                        }
                    }
                }

                string previousCurrentDirectory = Environment.CurrentDirectory;
                try {
                    Environment.CurrentDirectory = Path.GetDirectoryName(filename);
                    return Run(args.ToArray(), state);
                } finally {
                    Environment.CurrentDirectory = previousCurrentDirectory;
                }

            } catch (Exception ex) {
                Log.WriteError("Cannot run commands in " + filename + " (" + ex.Message + ")", filename, lineNo);
                return EXCEPTION_RESULT;
            }
        }

        private static int UsageAndExit(string message, int exitValue = OPTIONS_PROBLEM, bool extensiveHelp = false) {
            if (message != null) {
                Log.WriteInfo("**** " + message);
                Log.WriteInfo("");
            }

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
       
     CONFIGURE TRANSFORMER
-e     assembly transformer { options }
-f              transformer { options }

     TRANSFORM TESTDATA
-s     assembly transformer [{ options }]

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

     RENDER TESTDATA
-p     assembly renderer [{ options }] filename

     RENDER
-q     assembly renderer [{ options }] filename
-r              renderer [{ options }] filename 

                Built-in renderers:
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
     
     OTHER
-?              Write help
-a              Write extensive help  
-l              Execute readers and transformers lazily (lazy reading and transforming NOT YET IMPLEMENTED)
-m name value   Define name as value; a redefinition with a different value is not possible
-z              Reset state
-b              Stop execution here; useful in -o file
-debug          Start .Net debugger 
-c              Ignore case in rules
-v              Verbose
-w              Chatty
-o filename     Read options from file
");
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
    
    simplefilename      the assembly is checked.
                        e.g. ProjectDir\bin\MyProject.Main.dll

    filepattern         all matching assemblies are checked.
                        e.g. bin\MyProject.*.dll 

    directory           all .DLL and .EXE files in the directory are checked.
                        e.g. MyProject\bin\Debug

    @filename           lines are read as assembly filenames and checked.
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

        ///// <summary>
        ///// Main method. See <c>UsageAndExit</c> for the 
        ///// accepted arguments. 
        ///// </summary>
        //public int Run() {
        //    Regex.CacheSize = 1024;

        //    int returnValue = 0;

        //    var contexts = new List<IInputContext>();
        //    bool collectViolations = !string.IsNullOrWhiteSpace(_XmlOutput);
        //    Parallel.ForEach(
        //        _Assemblies.SelectMany(filePattern => filePattern.ExpandFilename()).Where(IsAssembly),
        //        new ParallelOptions { MaxDegreeOfParallelism = _MaxCpuCount },
        //        () => new ThreadLoopData_ { Context = new CheckerContext(collectViolations), MaxErrorCode = 0 },
        //        (assemblyFilename, state, loopData) => {
        //            int result = AnalyzeAssemblyMayRunInParallel(loopData.Context, assemblyFilename);
        //            loopData.MaxErrorCode = Math.Max(loopData.MaxErrorCode, result);
        //            return loopData;
        //        },
        //        loopData => {
        //            contexts.AddRange(loopData.Context.AssemblyContexts);
        //            returnValue = Math.Max(returnValue, loopData.MaxErrorCode);
        //        });

        //    if (collectViolations) {
        //        WriteXmlOutput(_XmlOutput, contexts);
        //    }

        //    LogSummary(contexts);

        //    return returnValue;
        //}

        /// <summary>
        /// The static Main method.
        /// </summary>
        public static int Main(string[] args) {
            Log.Logger = new ConsoleLogger();

            var program = new Program();
            try {
                return program.Run(args, new GlobalContext());
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
