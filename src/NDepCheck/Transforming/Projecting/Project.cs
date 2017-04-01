using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Projecting {
    public class Project : AbstractTransformerWithConfigurationPerInputfile<ProjectionSet> {
        private ProjectionSet _orderedProjections;

        public override string GetHelp(bool detailedHelp) {
            return @"  Project ('reduce') items and dependencies with ! and % rules

Configuration options: [-f projectionfile | -p projections]

Transformer options: None
";
        }

        #region Configure

        public override void Configure(GlobalContext globalContext, string configureOptions) {
            Options.Parse(configureOptions,
                new OptionAction('f', (args, j) => {
                    string fullSourceName = Path.GetFullPath(Options.ExtractOptionValue(args, ref j));
                    _orderedProjections = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????");
                    return j;
                }),
                new OptionAction('p', (args, j) => {
                    // A trick is used: The first line, which contains all options, should be ignored; and
                    // also the last } (which is from the surrounding options braces). Thus, 
                    // * we add // to the beginning - this comments out the first line;
                    // * and trim } at the end.
                    _orderedProjections = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader("//" + configureOptions.Trim().TrimEnd('}')), "-p", globalContext.IgnoreCase, "????");
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
        }

        internal const string ABSTRACT_IT_LEFT = "<";
        internal const string ABSTRACT_IT_BOTH = "!";
        internal const string ABSTRACT_IT_RIGHT = ">";
        internal const string ABSTRACT_IT_LEFT_AS_INNER = "[";
        internal const string ABSTRACT_IT_BOTH_AS_INNER = "|";
        internal const string ABSTRACT_IT_RIGHT_AS_INNER = "]";
        internal const string MAP = "---%";

        protected override ProjectionSet CreateConfigurationFromText(GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack) {

            ItemType sourceItemType = null;
            ItemType targetItemType = null;

            string ruleSourceName = fullConfigFileName;

            var elements = new List<IProjectionSetElement>();

            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
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
                        bool leftInner = line.StartsWith(ABSTRACT_IT_LEFT_AS_INNER);
                        bool rightInner = line.StartsWith(ABSTRACT_IT_RIGHT_AS_INNER);
                        bool bothInner = line.StartsWith(ABSTRACT_IT_BOTH_AS_INNER);
                        if (left || both || right || leftInner || rightInner || bothInner) {
                            Projection p = CreateProjection(globalContext, sourceItemType, targetItemType,
                                isInner: leftInner || rightInner || bothInner, ruleFileName: ruleSourceName,
                                lineNo: lineNo, rule: line.Substring(1).Trim(), ignoreCase: ignoreCase,
                                forLeftSide: left || both || leftInner || bothInner,
                                forRightSide: both || right || bothInner || rightInner);
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
                                   [CanBeNull]ItemType targetItemType, bool isInner, [NotNull] string ruleFileName, 
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

                var p = new Projection(sourceItemType, targetItemType, pattern, targetSegments, isInner, ignoreCase, forLeftSide, forRightSide);

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

        public override int Transform(GlobalContext context, string dependenciesFileName, IEnumerable<Dependency> dependencies, string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            if (_orderedProjections != null) {
                Log.WriteInfo("Reducing graph " + dependencySourceForLogging);

                var localCollector = new Dictionary<FromTo, Dependency>();
                foreach (var d in dependencies) {
                    ReduceEdge(_orderedProjections.AllProjections, d, localCollector);
                }
                transformedDependencies.AddRange(localCollector.Values);
                return Program.OK_RESULT;
            } else {
                Log.WriteWarning("No rule set found for reducing " + dependencySourceForLogging);
                return Program.NO_RULE_SET_FOUND_FOR_FILE;
            }
        }

        private static void ReduceEdge(IEnumerable<Projection> orderedProjections, Dependency d, 
                                       Dictionary<FromTo, Dependency> localCollector) {

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
                Log.WriteInfo("No graph output pattern found for drawing " + d.UsingItem.AsString() + " - I ignore it");
            } else if (usedItem == null) {
                Log.WriteInfo("No graph output pattern found for drawing " + d.UsedItem.AsString() + " - I ignore it");
            } else if (usingItem.IsEmpty() || usedItem.IsEmpty()) {
                // ignore this edge!
            } else {
                new FromTo(usingItem, usedItem).AggregateEdge(d, localCollector);
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
            // empty
        }

        #endregion Transform
    }
}
