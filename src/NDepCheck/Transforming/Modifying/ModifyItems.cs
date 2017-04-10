﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Modifying {
    public class ModifyItems : AbstractTransformerWithConfigurationPerInputfile<IEnumerable<ItemAction>> {
        public static readonly Option ModificationsFileOption = new Option("mf", "modifications-file", "filename", "File containing modifications", @default: "");
        public static readonly Option ModificationsOption = new Option("ml", "modifications-list", "modifications", "Inline modifications", orElse: ModificationsFileOption);

        private static readonly Option[] _configOptions = { ModificationsFileOption, ModificationsOption };

        public override string GetHelp(bool detailedHelp, string filter) {
            string result = $@"Modify counts and markers on items.

Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: None";
            if (detailedHelp) {
                result += @"

Configuration format:

Configuration files support the standard options + for include,
// for comments, macro definitions (see -help files).

Item modifications always have the format

    incomingMatch -> itemMatch -- outgoingMatch => itemAction

Each part can be empty, therefore the simplest modification (which does
nothing) is -> -- => or, equivalently, ->--=>.

An item is modified if
* at least one incoming dependency matches incomingMatch; or incomingMatch is empty;
* the item matches itemMatch; or itemMatch is empty; and
* at least one outgoing dependency matches outgoingMatch; or outgoingMatch is empty.

The four parts have the following syntax:
    incomingMatch, outgoingMatch
        dependency pattern (see -help dependency)

    itemMatch
        item pattern (see -help itempattern)

    itemAction
        a space- or comma-separated list of:
            ignore (or nothing)           is ignored
            +marker                       add marker to item
            -marker                       remove marker from item
            - or delete                   delete item and all its incoming and
                                          outgoing dependences

Examples:
   'From -> -- 'To => +FromTo      Add marker FromTo to items where at least one
                                   incoming dependency has marker From and at least
                                   one outgoing dependency has marker To
   -- 'OnCycle -> -                Delete all items having marker OnCycle";
            }
            return result;
        }

        public override bool RunsPerInputContext => false;

        private IEnumerable<ItemAction> _orderedActions;

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

        protected override IEnumerable<ItemAction> CreateConfigurationFromText(GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration) {

            var actions = new List<ItemAction>();
            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack, forceReloadConfiguration,
                onIncludedConfiguration: (e, n) => actions.AddRange(e),
                onLineWithLineNo: (line, lineNo) => {
                    actions.Add(new ItemAction(globalContext.GetExampleDependency(), line.Trim(), ignoreCase, fullConfigFileName, startLineNo));
                    return true;
                });
            return actions;
        }

        public override int Transform(GlobalContext globalContext, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            if (_orderedActions == null) {
                Log.WriteWarning($"No actions configured for {GetType().Name}");
            } else {

                Dictionary<Item, IEnumerable<Dependency>> items2incoming =
                    Item.CollectIncomingDependenciesMap(dependencies);
                Dictionary<Item, IEnumerable<Dependency>> items2outgoing =
                    Item.CollectOutgoingDependenciesMap(dependencies);

                var allItems = new HashSet<Item>(items2incoming.Keys.Concat(items2outgoing.Keys));

                foreach (var i in allItems.ToArray()) {
                    IEnumerable<Dependency> incoming;
                    items2incoming.TryGetValue(i, out incoming);
                    IEnumerable<Dependency> outgoing;
                    items2outgoing.TryGetValue(i, out outgoing);

                    ItemAction firstMatchingAction = _orderedActions.FirstOrDefault(a => a.Matches(incoming, i, outgoing));
                    if (firstMatchingAction == null) {
                        Log.WriteWarning("No match in actions for item " + i + " - item is deleted");
                        allItems.Remove(i);
                    } else {
                        if (!firstMatchingAction.Apply(i)) {
                            allItems.Remove(i);
                        }
                    }
                }

                transformedDependencies.AddRange(
                    dependencies.Where(d => allItems.Contains(d.UsingItem) && allItems.Contains(d.UsedItem)));
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