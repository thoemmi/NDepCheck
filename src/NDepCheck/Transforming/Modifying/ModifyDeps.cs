using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Modifying {
    public class ModifyDeps : AbstractTransformerWithConfigurationPerInputfile<IEnumerable<DependencyAction>> {
        public static readonly Option ModificationsFileOption = new Option("mf", "modifications-file", "filename", "File containing modifications", @default: "");
        public static readonly Option ModificationsOption = new Option("ml", "modifications-list", "modifications", "Inline modifications", orElse: ModificationsFileOption);

        private static readonly Option[] _configOptions = { ModificationsFileOption, ModificationsOption };

        public override string GetHelp(bool detailedHelp, string filter) {
            string result = $@"Modify counts and markers on dependencies.

Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: None";
            if (detailedHelp) {
                result += @"

Configuration format:

Configuration files support the standard options + for include,
// for comments, macro definitions (see -help files).

Dependency modifications always have the format

    usingItemMatch -- dependencyMatch -> usedItemMatch => dependencyAction

Each part can be empty, therefore the simplest modification (which does
nothing) is -- -> => or, equivalently, --->=>.

A dependency is modified if all three matches match it; empty matches
match always.

The four parts have the following syntax:
    usingItemMatch, usedItemMatch
        item pattern (see -help itempattern)

    dependencyMatch
        dependency pattern (see -help dependency)

    dependencyAction
        a space- or comma-separated list of:
            -? or reset-questionable		  reset questionable count to 0
            +? or mark-questionable       set questionable count to dependency count
            ? or increment-questionable   increment questionable count by 1
            -! or reset-bad               reset bad count to 0             
            +! or mark-bad                set bad count to dependency count
            ! or increment-bad            increment bad count by 1         
            ignore (or nothing)           is ignored
            +marker                       add marker to dependency
            -marker                       remove marker from dependency
            - or delete                   delete dependency

Examples:
   'From -- -> 'To => +FromTo      Add marker FromTo to dependencies where using item
                                   has marker From and used item has marker To
   -- 'OnCycle -> +!, -OnCycle     Set bad count for all dependencies with marker
                                   OnCycle and remove the marker";
            }
            return result;
        }

        public override bool RunsPerInputContext => false;

        private IEnumerable<DependencyAction> _orderedActions;

        public override void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            Option.Parse(globalContext, configureOptions,
                ModificationsFileOption.Action((args, j) => {
                    string fullSourceName = Path.GetFullPath(Option.ExtractRequiredOptionValue(args, ref j, "missing modifications filename"));
                    _orderedActions = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????", forceReload);
                    return j;
                }),
                ModificationsOption.Action((args, j) => {
                    // A trick is used: The first line, which contains all options, should be ignored; and
                    // also the last } (which is from the surrounding options braces). Thus,
                    // * we add // to the beginning - this comments out the first line;
                    // * and trim } at the end.
                    _orderedActions = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader("//" + (configureOptions ?? "").Trim().TrimEnd('}')), 
                        ModificationsOption.ShortName, globalContext.IgnoreCase, "????", forceReload);
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
        }

        protected override IEnumerable<DependencyAction> CreateConfigurationFromText(GlobalContext globalContext, 
            string fullConfigFileName, int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, 
            bool forceReloadConfiguration) {

            var actions = new List<DependencyAction>();
            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                forceReloadConfiguration,
                onIncludedConfiguration: (e, n) => actions.AddRange(e),
                onLineWithLineNo: (line, lineNo) => {
                    actions.Add(new DependencyAction(line.Trim(), ignoreCase, fullConfigFileName, startLineNo));
                    return true;
                });
            return actions;
        }

        public override int Transform(GlobalContext globalContext, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            if (_orderedActions == null) {
                Log.WriteWarning($"No actions configured for {GetType().Name}");
            } else {
                foreach (var d in dependencies) {
                    DependencyAction firstMatchingAction = _orderedActions.FirstOrDefault(a => a.Match(d));
                    if (firstMatchingAction == null) {
                        Log.WriteWarning("No match in actions for dependency " + d);
                    } else {
                        if (firstMatchingAction.Apply(d)) {
                            transformedDependencies.Add(d);
                        }
                    }
                }
            }
            return Program.OK_RESULT;
        }

        public override void FinishTransform(GlobalContext context) {
            // empty
        }

        public override IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source: null, markers: "", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source: null, markers: "use+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source: null, markers: "define", ct:5, questionableCt:0, badCt:2),
            };
        }
    }
}
