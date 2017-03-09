using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gibraltar;

namespace NDepCheck {
    /// <remarks>
    /// Main class of NDepCheck.
    /// All static methods may run in parallel.
    /// </remarks>
    public class Program {
        private const string VERSION = "V.3.0a";

        ////private class ThreadLoopData_ {
        ////    public CheckerContext Context { get; set; }
        ////    public int MaxErrorCode { get; set; }
        ////}

        public int Run(string[] args) {
            Log.SetLevel(Log.Level.Standard);

            Options options = new Options();
            GlobalContext state = new GlobalContext(this);

            if (args.Length == 0) {
                return UsageAndExit("No options or files specified");
            }

            int result = 0;

            int i;
            for (i = 0; i < args.Length; i++) {
                string arg = args[i];

                if (ArgMatches(arg, '?')) {
                    // -?        Do write help
                    return UsageAndExit(null, completeHelp: false);
                } else if (ArgMatches(arg, '@')) {
                    // -@ &      Do read options from file
                    string filename = ExtractOptionValue(args, ref i);
                    result = RunFrom(filename);

                    // @ file is also an input file - and if there are no input files in @, the error will come up there.
                    options.InputFilesSpecified = true;
                } else if (ArgMatches(arg, 'b')) {
                    // -b        Break execution here; useful in -@ file
                    goto DONE;
                } else if (ArgMatches(arg, 'c')) {
                    // -c &      Write dip output (after lazy reading; after lazy dep->graph run)
                    string filename = ExtractOptionValue(args, ref i);
                    if (string.IsNullOrWhiteSpace(filename)) {
                        return UsageAndExit("Missing filename after " + arg);
                    }
                    state.ReadAll(options).ReduceGraph(options, false).WriteDipFile(options, filename);
                } else if (arg == "-debug" || arg == "/debug") {
                    // -debug    Do start .Net debugger
                    Debugger.Launch();
                } else if (ArgMatches(arg, 'd')) {
                    // -d &      Set directory search locations for rule files
                    string path = ExtractOptionValue(args, ref i);
                    options.CreateDirectoryOption(path, recurse: false);
                } else if (ArgMatches(arg, 'e')) {
                    // -e $ &    Set file location with defined reader $ (currently supported: dip, dll, exe)
                    string extension = ExtractOptionValue(args, ref i);
                    CreateInputOption(args, ref i, extension, options, readOnlyItems: false);
                } else if (ArgMatches(arg, 'f')) {
                    // -f &      Set file location with reader defined by file extension
                    CreateInputOption(args, ref i, null, options, readOnlyItems: false);
                } else if (ArgMatches(arg, 'g')) {
                    // -g &      Write graph output to file (after lazy reading; after lazy dep->graph run)
                    string filename = ExtractOptionValue(args, ref i);
                    if (string.IsNullOrWhiteSpace(filename)) {
                        return UsageAndExit("Missing filename after " + arg);
                    }
                    state.ReadAll(options).ReduceGraph(options, false).WriteDotFile(options, filename);
                } else if (arg == "-h" || arg == "/h") {
                    // -h        Do write extensive help
                    return UsageAndExit(null, completeHelp: true);
                } else if (arg == "-i" || arg == "/i") {
                    // -i        Set ignorecase option
                    options.IgnoreCase = true;
                } else if (ArgMatches(arg, 'j')) {
                    // -j #      Set edge length for graph output
                    string optionValue = ExtractOptionValue(args, ref i);
                    int lg;
                    if (int.TryParse(optionValue, out lg) && lg > 0) {
                        options.StringLength = lg;
                    } else {
                        return UsageAndExit("Cannot parse value for edge text '" + optionValue + "' as number");
                    }
                } else if (ArgMatches(arg, 'k')) {
                    // -k $ &    Set file location with defined reader $ (currently supported: dip, dll, exe)
                    string extension = ExtractOptionValue(args, ref i);
                    CreateInputOption(args, ref i, extension, options, readOnlyItems: true);
                } else if (ArgMatches(arg, 'l')) {
                    // -l &      Set file location with reader defined by file extension
                    CreateInputOption(args, ref i, null, options, readOnlyItems: true);
                } else if (ArgMatches(arg, 'm')) {
                    // -m &      Write matrix output to file (after lazy reading; after lazy dep->graph run)

                    char format = '1';
                    if (arg.Length >= 3) {
                        switch (arg[2]) {
                            case '=':
                                // Use default
                                break;
                            case '1':
                            case '2':
                                format = arg[2];
                                break;
                            default:
                                return UsageAndExit("Unsupported matrix format option in " + arg);
                        }
                    }

                    string filename = ExtractOptionValue(args, ref i);
                    if (string.IsNullOrWhiteSpace(filename)) {
                        return UsageAndExit("Missing filename after " + arg);
                    }
                    state.ReadAll(options).ReduceGraph(options, false).WriteMatrixFile(options, format, filename);
                } else if (ArgMatches(arg, 'n')) {
                    // -n #|all  Set cpu count (currently no-op)
                    string ms = ExtractOptionValue(args, ref i);
                    if (ms == "all" || ms == "*") {
                        options.MaxCpuCount = Environment.ProcessorCount;
                    } else {
                        int m;
                        if (int.TryParse(ms, out m)) {
                            options.MaxCpuCount = m;
                        } else {
                            return UsageAndExit("/n value " + ms + " is neither * nor a number", completeHelp: false);
                        }
                    }
                } else if (ArgMatches(arg, 'o')) {
                    // -o &      Do write xml depcheck output (after lazy reading; after lazy depcheck)
                    string xmlfile = ExtractOptionValue(args, ref i);
                    if (xmlfile == null) {
                        return UsageAndExit("Missing =filename after " + arg);
                    }

                    result = state.ReadAll(options).ComputeViolations(options);
                    state.WriteViolations(xmlfile);
                } else if (ArgMatches(arg, 'p')) {
                    // -p        Do write standard depcheck output (after lazy reading; after lazy depcheck)
                    result = state.ReadAll(options).ComputeViolations(options);
                    state.WriteViolations(null);
                } else if (ArgMatches(arg, 'q')) {
                    // -q        Set option to show unused questionable rules
                    options.ShowUnusedQuestionableRules = true;
                } else if (ArgMatches(arg, 'r')) {
                    // -r *      Do graph transformation (after lazy reading; after lazy depcheck; and lazy dep->graph run)
                    string transformationOption = ExtractOptionValue(args, ref i);
                    state.ReadAll(options).ReduceGraph(options, true).TransformGraph(transformationOption);
                } else if (ArgMatches(arg, 's')) {
                    // -s &      Set directory tree search location for rule files
                    string path = ExtractOptionValue(args, ref i);
                    options.CreateDirectoryOption(path, recurse: true);
                } else if (ArgMatches(arg, 't')) {
                    // -t &      Set rule file extension (default .dep)
                    options.RuleFileExtension = "." + ExtractOptionValue(args, ref i).TrimStart('.');
                } else if (ArgMatches(arg, 'u')) {
                    // -u        Set option to show unused rules
                    options.ShowUnusedRules = true;
                } else if (ArgMatches(arg, 'v')) {
                    // -v        Set verbose option
                    Log.SetLevel(Log.Level.Verbose);
                    WriteVersion();
                } else if (ArgMatches(arg, 'w')) {
                    // -w        Set chatty option
                    Log.SetLevel(Log.Level.Chatty);
                    WriteVersion();
                } else if (ArgMatches(arg, 'x')) {
                    // -x &      Set search location for default rule file
                    string filename = ExtractOptionValue(args, ref i);
                    if (!string.IsNullOrEmpty(options.DefaultRuleSetFile)) {
                        return UsageAndExit("Only one default rule set can be specified with " + arg);
                    }
                    if (!File.Exists(filename)) {
                        return UsageAndExit("Cannot find the default rule set file '" + filename + "'", exitValue: 2);
                    }
                    options.DefaultRuleSetFile = filename;
                } else if (ArgMatches(arg, 'y')) {
                    // -y <type> <type>        Set itemtypes for reductions
                    string usingFormat = ExtractOptionValue(args, ref i);
                    string usedFormat = args[++i];
                    options.UsingItemType = ItemType.New(usingFormat);
                    options.UsedItemType = ItemType.New(usedFormat);
                } else if (ArgMatches(arg, 'z')) {
                    // -z        Remove all dependencies and graphs and caches
                    Intern<ItemType>.Reset();
                    Intern<ItemTail>.Reset();
                    Intern<Item>.Reset();
                    AbstractDotNetAssemblyDependencyReader.Reset();
                    state.Reset();
                    options.Reset();
                } else if (arg.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)
                        || arg.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase)
                        || arg.EndsWith(".dip", StringComparison.InvariantCultureIgnoreCase)
                        || arg.Contains("*")) {
                    // &         If & ends with .dll, .exe, or. dip, or contains *: Remember 
                    //           reading file - heuristics; should be removed.
                    options.InputFilesSpecified |= CreateInputOption(args, ref i, null, arg, options, false);
                } else {
                    return UsageAndExit("Unsupported option '" + arg + "'");
                }
            }

            if (!options.GraphingDone && !options.CheckingDone) {
                // Default action at end if nothing was done
                result = state.ReadAll(options).ComputeViolations(options);
                state.WriteViolations(null);
            }

            if (!options.InputFilesSpecified) {
                return UsageAndExit("No input files specified");
            }

        DONE:

            if (Log.IsVerboseEnabled) {
                Log.WriteInfo("Completed with exitcode " + result);
            }

            return result;
        }

        private bool ArgMatches(string arg, char option) {
            return arg.ToLowerInvariant().StartsWith("/" + option) || arg.ToLowerInvariant().StartsWith("-" + option);
        }

        private int RunFrom(string filename) {
            int lineNo = 0;
            try {
                var args = new List<string>();
                using (var sr = new StreamReader(filename)) {
                    for (;;) {
                        lineNo++;
                        string line = sr.ReadLine();
                        if (line == null) {
                            break;
                        }
                        line = Regex.Replace(line, "//.*$", "").Trim();
                        if (line == "") {
                            continue;
                        }
                        args.AddRange(line.Split(' ', '\t').Select(s => s.Trim()).Where(s => s != ""));
                    }
                }

                string previousCurrentDirectory = Environment.CurrentDirectory;
                try {
                    Environment.CurrentDirectory = Path.GetDirectoryName(filename);
                    return Run(args.ToArray());
                } finally {
                    Environment.CurrentDirectory = previousCurrentDirectory;
                }

            } catch (Exception ex) {
                Log.WriteError("Cannot run commands in " + filename + " (" + ex.Message + ")", filename, lineNo);
                return 7;
            }
        }

        private void CreateInputOption(string[] args, ref int i, string extension, Options options, bool readOnlyItems) {
            string filePattern = ExtractOptionValue(args, ref i);
            options.InputFilesSpecified |= CreateInputOption(args, ref i, extension, filePattern, options, readOnlyItems);
        }

        private bool CreateInputOption(string[] args, ref int i, string extension, string filePattern, Options options, bool readOnlyItems) {
            options.CreateInputOption(extension, filePattern, negativeFilePattern: i + 2 < args.Length && args[i + 1] == "-" ? args[i += 2] : null, readOnlyItems: readOnlyItems);

            return true;
        }

        private static int UsageAndExit(string message, int exitValue = 1, bool completeHelp = false) {
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
      NDepCheck /s=SourceDir /f=My.dll

* Produce graph of dependencies in My.DLL:
      NDepCheck /s=SourceDir /f=My.DLL /g=My.dot
      dot -Tgif -oMy.gif My.dot

All messages of NDepCheck are written to Console.Out.

Options overview:
    & in the following is a filename.
    # in the following is a positive integer number.
    Options can be written with leading - or /

    -?        Do write help
    -@ &      Do read options from file
    -b        Ignore all remaining options (""break""); useful in -@ file
    -c &      Write dependencies to .dip file (after lazy reading; after lazy dep->graph run)
    -d &      Set directory search locations for rule files
    -debug    Do start .Net debugger
    -e $ &    Set file location with defined reader $ (currently supported: dip, dll, exe)
    -f &      Set file location with reader defined by file extension
    -g &      Write graph output to file (after lazy reading; after lazy dep->graph run)
    -h        Do write extensive help
    -i        Set ignorecase option
    -j #      Set edge length for graph output
    -m &      Write matrix output to file (after lazy reading; after lazy dep->graph run)
    -n #|all  Set cpu count (currently no-op)
    -o &      Do write xml depcheck output (after lazy reading; after lazy depcheck)
    -p        Do write standard depcheck output (after lazy reading; after lazy depcheck)
    -q        Set option to show unused questionable rules
    -r *      Do graph transformation (after lazy reading; after lazy depcheck; and lazy dep->graph run)
    -s &      Set directory tree search location for rule files
    -t &      Set rule file extension (default .dep; specify before -s!)
    -u        Set option to show unused rules
    -v        Set verbose option
    -w        Set chatty option
    -x &      Set search location for default rule file
    -y <type> <type>     Set itemtypes for reductions; type=NAME:KEY1:KEY2:...
    -z        Remove all dependencies and graphs, clear search options (-d, -s, -x)
    &         If & ends with .dll, .exe, or. dip, or contains *: Remember 
              reading file - heuristics; should be removed.


");
            if (completeHelp) {
                Console.Out.WriteLine(@"

############# NOT YET UPDATED ##################

   /d=<directory>    For each assembly file A.dll, look for corresponding 
         rule file A.dll.dep in this directory (multiple /d options are 
         supported). This is especially useful with + lines.

   /s=<directory>    Like /d, but also look in all subdirectories. Mixing
         /s and /d options is supported.

   /x=<rule file>    Use this rule file if no matching rule file is found
         via /s and /d  This is also useful if no /s and /d options
         are specified.

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
        % (**).*

   // Classes without namespace are identified by their class name:
        % (*)

   // Classes in System.* are identified by the empty group, i.e.,
   // they (and arrows reaching them) are not shown at all.
        % ()System.**
            ");

            }
            return exitValue;
        }

        private static void WriteVersion() {
            Log.WriteInfo("NDepCheck " + VERSION + " (c) HMMüller, Th.Freudenberg 2006...2017");
        }

        /// <summary>
        /// Helper method to get option value of value 
        /// </summary>
        private static string ExtractOptionValue(string[] args, ref int i) {
            string optionValue;
            string arg = args[i];
            string[] argparts = arg.Split(new[] { '=' }, 2);
            if (argparts.Length > 1) {
                optionValue = argparts[1];
            } else if (i < args.Length - 1 && (arg.StartsWith("/") || arg.StartsWith("-"))) {
                optionValue = args[++i];
            } else if (arg.Length <= 2) {
                return null;
            } else if (arg[2] == '=') {
                optionValue = arg.Length == 3 ? args[++i] : arg.Substring(3);
            } else {
                optionValue = arg.Length == 2 ? args[++i] : arg.Substring(2);
            }
            return optionValue;
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

            DateTime start = DateTime.Now;

            var program = new Program();
            try {
                return program.Run(args);
            } catch (FileNotFoundException ex) {
                Log.WriteWarning(ex.Message);
                return 4;
            } catch (Exception ex) {
                Log.WriteError("Exception occurred: " + ex.Message);
                return 5;
            } finally {
                DateTime end = DateTime.Now;
                TimeSpan runtime = end.Subtract(start);
                if (runtime < new TimeSpan(0, 0, 1)) {
                    Log.WriteInfo("DC took " + runtime.Milliseconds + " ms.");
                } else if (runtime < new TimeSpan(0, 1, 0)) {
                    Log.WriteInfo("DC took " + runtime.TotalSeconds + " s.");
                } else if (runtime < new TimeSpan(1, 0, 0)) {
                    Log.WriteInfo("DC took " + runtime.Minutes + " min and " + runtime.Seconds + " s.");
                } else {
                    Log.WriteInfo("DC took " + runtime.TotalHours + " hours.");
                }
            }
        }
    }
}
