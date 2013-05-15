using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NDepCheck {
    public class Options {
        private readonly List<DirectoryOption> _directories = new List<DirectoryOption>();
        private readonly List<AssemblyOption> _assemblies = new List<AssemblyOption>();

        /// <summary>
        /// Set output file name. If set to <c>null</c> (or left
        /// at <c>null</c>), no DOT output is created.
        /// </summary>
        /// <value>The dot filename.</value>
        public string DotFilename { get; set; }

        /// <value>
        /// Show transitive edges. If set to <c>null</c> (or left 
        /// at <c>null</c>), transitive edges are heuristically
        /// removed.
        /// </value>
        public bool ShowTransitiveEdges { get; set; }

        /// <value>
        /// If not null, show a concrete dependency 
        /// for each illegal edge.
        /// </value>
        public int? StringLengthForIllegalEdges { get; set; }

        public bool ShowUnusedQuestionableRules { get; set; }

        /// <value>
        /// Mark output of <c>DependencyGrapher</c>
        /// as verbose.
        /// </value>
        public bool Verbose { get; set; }

        public bool Debug { get; set; }

        public string DefaultRuleSetFile { get; set; }

        public List<AssemblyOption> Assemblies {
            get { return _assemblies; }
        }

        public List<DirectoryOption> Directories {
            get { return _directories; }
        }

        public Options() {
            ShowUnusedQuestionableRules = true;
        }

        public int ParseCommandLine(string[] args) {
            if (args.Length == 0) {
                return UsageAndExit("No options or files specified");
            }

            int i;
            for (i = 0; i < args.Length; i++) {
                string arg = args[i];
                if (arg == "-debug" || arg == "/debug") {
                    Debugger.Launch();
                } else if (arg.StartsWith("-d") || arg.StartsWith("/d")) {
                    CreateDirectoryOption(arg, false);
                } else if (arg.StartsWith("-s") || arg.StartsWith("/s")) {
                    CreateDirectoryOption(arg, true);
                } else if (arg.StartsWith("-x") || arg.StartsWith("/x")) {
                    string filename = ExtractOptionValue(arg);
                    if (filename == null) {
                        return UsageAndExit("Missing =filename after " + arg);
                    }
                    if (!String.IsNullOrEmpty(DefaultRuleSetFile)) {
                        return UsageAndExit("Only one default rule set can be specified with " + arg);
                    }
                    if (!File.Exists(filename)) {
                        UsageAndExit("Cannot find the default rule set file " + filename);
                        return 2;
                    }
                    DefaultRuleSetFile = filename;
                } else if (arg == "-v" || arg == "/v") {
                    Verbose = true;
                    WriteVersion();
                } else if (arg == "-y" || arg == "/y") {
                    Debug = true;
                    WriteVersion();
                } else if (arg.StartsWith("-g") || arg.StartsWith("/g")) {
                    string filename = ExtractOptionValue(arg);
                    if (filename == null) {
                        return UsageAndExit("Missing =filename after " + arg);
                    }
                    DotFilename = filename;
                } else if (arg == "-q" || arg == "/q") {
                    ShowUnusedQuestionableRules = false;
                } else if (arg == "-t" || arg == "/t") {
                    ShowTransitiveEdges = true;
                } else if (arg.StartsWith("-i") || arg.StartsWith("/i")) {
                    string lg = ExtractOptionValue(arg);
                    StringLengthForIllegalEdges = lg == null ? 80 : Int32.Parse(lg);
                } else if (arg == "-h" || arg == "/h") {
                    return UsageAndExit(null);
                } else if (!arg.StartsWith("/") && !arg.StartsWith("-")) {
                    // We are done with the options.
                    break;
                } else {
                    return UsageAndExit("Unexpected option " + arg);
                }
            }

            // remaining arguments are assemblies
            for (; i < args.Length; i++) {
                string positive = args[i];
                string negative =
                    i + 2 < args.Length && (args[i+1] == "/e" | args[i+1] == "-e")
                    ? args[i += 2]
                    : null;
                Assemblies.Add(new AssemblyOption(positive, negative));
            }

            // We are past the arguments - now, we process the input files.)
            if (Assemblies.Count == 0) {
                return UsageAndExit("No assemblies specified");
            }

            return 0;
        }

        private static void WriteVersion() {
            Log.WriteInfo("NDepCheck V." + typeof(Program).Assembly.GetName().Version.ToString(2) +
                      " (c) HMMüller, Th.Freudenberg 2006...2010");
        }

        private static int UsageAndExit(string message) {
            if (message != null) {
                Log.WriteInfo(message);
            }
            WriteVersion();
            Console.Out.WriteLine(
                @"
Usage:
   NDepCheck [<option> ...] [<assemblyfilespec> ...]

Typical uses:

* Check dependencies in My.DLL; My.dll.dep is somewhere below SourceDir:
      NDepCheck /s=SourceDir My.dll

* Produce graph of dependencies in My.DLL:
      NDepCheck /s=SourceDir My.DLL /g=My.dot
      dot -Tgif -oMy.gif My.dot

All messages of NDepCheck are written to Console.Out.

Options:
   /d=<directory>    For each assembly file A.dll, look for corresponding 
         rule file A.dll.dep in this directory (multiple /d options are 
         supported). This is especially useful with + lines.

   /s=<directory>    Like /d, but also look in all subdirectories. Mixing
         /s and /d options is supported.

   /x=<rule file>    Use this rule file if no matching rule file is found
         via /s and /d options. This is also useful if no /s and /d options
         are specified.

   /g=<dot file>   Create output of dependencies in AT&T DOT format.
         By default, NDepCheck tries to remove transitive
         edges - i.e., if a uses b, b uses c, but also a uses c, then
         the last edge is not shown. The algorithm for this will
         sometimes choose funny edges for removal ...

   /t    Show also transitive edges in DOT graph.

   /i[=<N>]        For each illegal edge (i.e., edge not allowed by 
         the dependency file), show an example of a concrete illegal 
         dependency in the DOT graph. N is the maximum width of strings 
         used; the default is 80. Graphs can become quite cluttered 
         with this option.

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

Rules files:
         Rule files contain rule definition commands.
         The following commands are supported:

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

         A pattern can be specified in three ways:

           ^regexp$              ... matched against a declaration
                                     (""declarations"" see below)

           ^regexp               ... the regexp is expanded to up to four
                                     different forms, all of which are
                                     matched against declarations:
               ^regexp$                   - for matching a class name
               ^regexp(/<ident>)*$        - for matching nested classes
                                            (if regexp contains no / )
                                            <ident> is the pattern
                                            matching an identifier.
               ^regexp::<ident>$          - for matching methods
                                            (if regexp contains no ::)
               ^regexp(/<ident>)*::ident$ - for methods of nested classes
                                            (if regexp contains no / and no ::)

           wildcardpath          ... first, the following replacements are done:

               .       is replaced with the reg.exp. [.] (matches single period)

               *       is replaced with the reg.exp. for an <ident> (a ""name"")

               **      is usually replaced with <ident>(?:.<ident>)* (a 
                       ""path"").
                            (?: in a reg.exp.means that the parentheses do not 
                            count as numbered group when matching \1, \2, etc.)
                       However, if there is a slash (/) somewhere to the left 
                       of the **, it is replaced with <ident>(?:/<ident>)*, 
                       i.e., the idents are separated by /. This can be used
                       to match inner class hierarchies.

               After the wildcard replacemants, suffixes are added as for 
               ^regexp.


Exit codes:
   0    All dependencies ok (including questionable rules).
   1    Usage error.
   2    Cannot load dependency file (syntax error or file not found).
   3    Dependencies not ok.
   4    Assembly file specified as argument not found.
   5    Other exception.
   6    No dependency file found for an assembly in /d and /s 
        directories, and /x not specified.


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
            return 1;
        }

        private void CreateDirectoryOption(string arg, bool recurse) {
            string path = ExtractOptionValue(arg);
            if (Directory.Exists(path)) {
                Directories.Add(new DirectoryOption(path, recurse));
            } else {
                Log.WriteWarning("Directory " + path + " not found - ignored in dep-File");
            }
        }

        /// <summary>
        /// Helper method to get option value
        /// of /x= option.
        /// </summary>
        private static string ExtractOptionValue(string arg) {
            string filename;
            if (arg.Length <= 2) {
                return null;
            } else if (arg[2] == '=') {
                filename = arg.Substring(3);
            } else {
                filename = arg.Substring(2);
            }
            return filename;
        }
    }
}