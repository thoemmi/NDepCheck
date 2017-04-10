using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Reading;

namespace NDepCheck.Transforming.Projecting {
    public class ProjectItems : AbstractTransformerWithConfigurationPerInputfile<ProjectionSet> {
        internal const string ABSTRACT_IT_LEFT = "<";
        internal const string ABSTRACT_IT_BOTH = "!";
        internal const string ABSTRACT_IT_RIGHT = ">";
        internal const string MAP = "---%";

        public static readonly Option ProjectionFileOption = new Option("pf", "projection-file", "filename", "File containing projections", @default: "");
        public static readonly Option ProjectionsOption = new Option("pl", "projection-list", "projections", "Inline projections", orElse: ProjectionFileOption);

        private static readonly Option[] _configOptions = { ProjectionFileOption, ProjectionsOption };

        public static readonly Option BackProjectionDipFileOption = new Option("bp", "back-projection-input", "filename", "Do back projection of information in dipfile", @default: "no back projection");
        public static readonly Option BackProjectionTrimOption = new Option("bt", "back-projection-trim", "", "When back projecting, keep only projected edges", @default: false);

        private static readonly Option[] _transformOptions = { BackProjectionDipFileOption, BackProjectionTrimOption };

        private ProjectionSet _orderedProjections;
        private Dictionary<FromTo, Dependency> _dependenciesForBackProjection;
        public override string GetHelp(bool detailedHelp, string filter) {
            string result = $@"  Project ('reduce') items and dependencies

Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";

            if (detailedHelp) {
                result += @"

Configuration format:

Configuration files support the standard options + for include,
// for comments, macro definitions (see -help files).

Mandatory type transformation line:
$ type ---% type

Projections:
< pattern [---% result]
! pattern [---% result]
> pattern [---% result]

where

type     is an NDepCheck type or type definition (see -help types)
pattern  is an NDepCheck item pattern (see -help itempattern)
result   is element[:element...], where each element is a string or \1, \2, ...
         \1 etc. refers to a matched group in the itempattern.
If no result is provided, result is assumed to be \1:\2:..., with the number
of groups in the pattern.
result can also be a single -, in which case a matching item is not projected.
Also, if all matched groups are empty, the matching item is not projected.

< matches only using items
| matches both using and used items
> matches only used items

After projecting the items, all dependencies are removed where the using or
used item is not projected.

Examples:
::(**)::(**)                project to 3rd and 5th field; e.g., the result of
                            projecting a.1:b.2:c.3:d.4:e.5:f.6 is c.3:e.5
::(**)::(**) ---% \1:\2     same as above
::a.(**)::   ---% MOD_A:\1  Projection with fixed string
::()                        do not project item (single group is always empty)
::           ---% -         same as above
";
            }
            return result;
        }

        #region Configure

        public override void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            Option.Parse(globalContext, configureOptions,
                ProjectionFileOption.Action((args, j) => {
                    string fullSourceName = Path.GetFullPath(Option.ExtractRequiredOptionValue(args, ref j, "missing projections filename"));
                    _orderedProjections = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????", forceReload);
                    return j;
                }),
                ProjectionsOption.Action((args, j) => {
                    // A trick is used: The first line, which contains all options, should be ignored; and
                    // also the last } (which is from the surrounding options braces). Thus, 
                    // * we add // to the beginning - this comments out the first line;
                    // * and trim } at the end.
                    _orderedProjections = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader("//" + (configureOptions ?? "").Trim().TrimEnd('}')), ProjectionsOption.ShortName, 
                        globalContext.IgnoreCase, "????", forceReload);
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
        }

        protected override ProjectionSet CreateConfigurationFromText(GlobalContext globalContext, string fullConfigFileName, int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration) {

            ItemType sourceItemType = null;
            ItemType targetItemType = null;

            string ruleSourceName = fullConfigFileName;

            var elements = new List<IProjectionSetElement>();

            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                forceReloadConfiguration,
                onIncludedConfiguration: (e, n) => elements.Add(e),
                onLineWithLineNo: (line, lineNo) => {
                    if (line.StartsWith("$")) {
                        string typeLine = line.Substring(1).Trim();
                        int i = typeLine.IndexOf(MAP, StringComparison.Ordinal);
                        if (i < 0) {
                            Log.WriteError($"{line}: $-line must contain " + MAP, ruleSourceName, lineNo);
                        }
                        sourceItemType =
                            GlobalContext.GetItemType(globalContext.ExpandDefines(typeLine.Substring(0, i).Trim()));
                        targetItemType =
                            GlobalContext.GetItemType(globalContext.ExpandDefines(typeLine.Substring(i + MAP.Length).Trim()));
                        return true;
                    } else {
                        bool left = line.StartsWith(ABSTRACT_IT_LEFT);
                        bool right = line.StartsWith(ABSTRACT_IT_RIGHT);
                        bool both = line.StartsWith(ABSTRACT_IT_BOTH);
                        if (left || both || right) {
                            Projection p = CreateProjection(globalContext, sourceItemType, targetItemType,
                                ruleFileName: ruleSourceName,
                                lineNo: lineNo, rule: line.Substring(1).Trim(), ignoreCase: ignoreCase,
                                forLeftSide: left || both , forRightSide: both || right);
                            elements.Add(p);
                            return true;
                        } else {
                            return false;
                        }
                    }
                });
            return new ProjectionSet(elements);
        }

        private Projection CreateProjection([NotNull] GlobalContext globalContext, [CanBeNull] ItemType sourceItemType,
                                   [CanBeNull]ItemType targetItemType, [NotNull] string ruleFileName,
                                   int lineNo, [NotNull] string rule, bool ignoreCase, bool forLeftSide, bool forRightSide) {
            if (sourceItemType == null || targetItemType == null) {
                Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleFileName}, graph rules are ignored", ruleFileName, lineNo);
                throw new ApplicationException("Itemtypes not defined");
            } else {
                int i = rule.IndexOf(MAP, StringComparison.Ordinal);
                string pattern;
                string[] targetSegments;
                if (i >= 0) {
                    string rawPattern = rule.Substring(0, i).Trim();
                    pattern = globalContext.ExpandDefines(rawPattern);

                    string rawTargetSegments = rule.Substring(i + MAP.Length).Trim();
                    targetSegments = globalContext.ExpandDefines(rawTargetSegments).Split(':').Select(s => s.Trim()).ToArray();
                } else {
                    pattern = globalContext.ExpandDefines(rule.Trim());
                    targetSegments = null;
                }

                var p = new Projection(sourceItemType, targetItemType, pattern, targetSegments, 
                                       ignoreCase, forLeftSide, forRightSide);

                if (Log.IsChattyEnabled) {
                    Log.WriteInfo("Reg.exps used for projecting " + pattern +
                                  (targetSegments == null ? "" : " to " + string.Join(":", targetSegments)) + " (" + ruleFileName + ":" + lineNo + ")");
                    Log.WriteInfo(p.ToString());
                }

                return p;
            }
        }

        #endregion Configure

        #region Transform

        public override bool RunsPerInputContext => true;

        public override int Transform(GlobalContext globalContext, string dependenciesFileName, IEnumerable<Dependency> dependencies,
                            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {
            if (_orderedProjections != null) {
                string fullDipName = null;
                bool keepOnlyProjected = false;
                Option.Parse(globalContext, transformOptions, BackProjectionDipFileOption.Action((args, j) => {
                    fullDipName = Path.GetFullPath(Option.ExtractRequiredOptionValue(args, ref j, "missing back projection source filename"));
                    return j;
                }), BackProjectionTrimOption.Action((args, j) => {
                    keepOnlyProjected = true;
                    return j;
                }));

                if (fullDipName != null) {
                    // Back projection
                    if (_dependenciesForBackProjection == null) {
                        InputContext localContext = new DipReader(fullDipName).ReadDependencies(0);
                        if (localContext == null) {
                            throw new Exception("Internal Error: new DipReader() will always create new InputContext - cannot be null");
                        }
                        _dependenciesForBackProjection = localContext.Dependencies.ToDictionary(
                            d => new FromTo(d.UsingItem, d.UsedItem), d => d);
                    }

                    var localCollector = new Dictionary<FromTo, Dependency>();
                    var backProjected = new List<Dependency>();
                    int missingPatternCount = 0;
                    foreach (var d in dependencies) {
                        FromTo projectedEdgeFromTo = ReduceEdge(_orderedProjections.AllProjections, d, 
                                                                localCollector, () => OnMissingPattern(ref missingPatternCount));

                        if (projectedEdgeFromTo != null) {
                            // The edge was projected
                            Dependency replaceDataEdge;
                            if (_dependenciesForBackProjection.TryGetValue(projectedEdgeFromTo, out replaceDataEdge)) {
                                d.ResetBad();
                                d.ResetQuestionable();
                                d.AggregateCounts(replaceDataEdge);
                                backProjected.Add(d);
                            }
                            // else not back projected -> Warning?
                        }
                    }
                    transformedDependencies.AddRange(keepOnlyProjected ? backProjected : dependencies);
                } else {
                    // Forward projection
                    var localCollector = new Dictionary<FromTo, Dependency>();
                    int missingPatternCount = 0;
                    foreach (var d in dependencies) {
                        ReduceEdge(_orderedProjections.AllProjections, d, localCollector,
                                   () => OnMissingPattern(ref missingPatternCount));
                    }
                    transformedDependencies.AddRange(localCollector.Values);
                }
                return Program.OK_RESULT;
            } else {
                Log.WriteWarning("No rule set found for reducing " + dependencySourceForLogging);
                return Program.NO_RULE_SET_FOUND_FOR_FILE;
            }
        }

        private static bool OnMissingPattern(ref int missingPatternCount) {
            missingPatternCount++;
            if (missingPatternCount == 1000) {
                Log.WriteError("After 1000 missing patterns, no more missing patterns are logged");
            }
            return missingPatternCount < 1000;
        }

        private FromTo ReduceEdge(IEnumerable<Projection> orderedProjections, Dependency d,
                                       Dictionary<FromTo, Dependency> localCollector, Func< bool> onMissingPattern) {
            Item usingItem = orderedProjections
                                    //.Skip(GuaranteedNonMatching(d.UsingItem))
                                    //.SkipWhile(ga => ga != FirstPossibleAbstractionInCache(d.UsingItem, skipCache))
                                    .Select(ga => ga.Match(d.UsingItem, left: true))
                                    .FirstOrDefault(m => m != null);
            Item usedItem = orderedProjections
                                    //.Skip(GuaranteedNonMatching(d.UsedItem))
                                    //.SkipWhile(ga => ga != FirstPossibleAbstractionInCache(d.UsedItem, skipCache))
                                    .Select(ga => ga.Match(d.UsedItem, left: false))
                                    .FirstOrDefault(n => n != null);

            if (usingItem == null) {
                if (onMissingPattern()) {
                    Log.WriteWarning("No projection pattern found for " + d.UsingItem.AsString() + " - I ignore it");
                }
                return null;
            } else if (usedItem == null) {
                if (onMissingPattern()) {
                    Log.WriteWarning("No projection pattern found for " + d.UsedItem.AsString() + " - I ignore it");
                }
                return null;
            } else if (usingItem.IsEmpty() || usedItem.IsEmpty()) {
                // ignore this edge!
                return null;
            } else {
                return new FromTo(usingItem, usedItem).AggregateEdge(d, localCollector);
            }
        }

        public override IEnumerable<Dependency> GetTestDependencies() {
            ItemType abc = ItemType.New("AB:A:B");
            Item a1 = Item.New(abc, "a", "1");
            Item a2 = Item.New(abc, "a", "2");
            Item b = Item.New(abc, "b", "");


            return new[] {
                FromTo(a1, a1), FromTo(a1, a2), FromTo(a2, a1), FromTo(a2, a2), FromTo(a1, b)
            };
        }

        private Dependency FromTo(Item from, Item to) {
            return new Dependency(from, to, new TextFileSource("Test", 1), "Use", ct: 1);
        }

        public override void FinishTransform(GlobalContext context) {
            // reset cached back projection dependencies for next transform
            _dependenciesForBackProjection = null;
        }

        #endregion Transform
    }
}
