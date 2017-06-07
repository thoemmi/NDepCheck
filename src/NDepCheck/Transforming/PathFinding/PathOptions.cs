using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.PathFinding {
    public class PathOptions {
        public readonly Option PathItemAnchorOption = new Option("pi", "path-item", "itempattern",
            "item pattern to be matched by path", @default: "all items match", multiple: true);
        public readonly Option PathDependencyAnchorOption = new Option("pd", "path-dependency", "dependencypattern",
            "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public readonly Option CountItemAnchorOption = new Option("ci", "count-item", "itempattern",
            "item pattern to be matched by path", @default: "all items match", multiple: true);
        public readonly Option CountDependencyAnchorOption = new Option("cd", "count-dependency", "dependencypattern",
            "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public readonly Option MultipleItemAnchorOption = new Option("mi", "multiple-item", "itempattern",
            "item pattern to be matched by path", @default: "all items match", multiple: true);
        public readonly Option MultipleDependencyAnchorOption = new Option("md", "multiple-dependency",
            "dependencypattern", "dependency pattern to be matched by path", @default: "all dependencies match",
            multiple: true);
        public readonly Option NoSuchItemAnchorOption = new Option("ni", "no-such-item", "itempattern",
            "item pattern to be matched by path", @default: "all items match", multiple: true);
        public readonly Option NoSuchDependencyAnchorOption = new Option("nd", "no-such-dependency", "dependencypattern",
            "dependency pattern to be matched by path", @default: "all dependencies match", multiple: true);
        public readonly Option BackwardsOption = new Option("bw", "upwards", "",
            "Traverses dependencies in opposite direction", @default: false);
        public readonly Option MaxPathLengthOption = new Option("ml", "max-length", "#", "maximum length of path found",
            @default: "twice the number of provided anchors");

        public Option[] WithOptions(params Option[] moreOptions) {
            return
                new[] {
                    PathItemAnchorOption, PathDependencyAnchorOption, CountItemAnchorOption,
                    CountDependencyAnchorOption, MultipleItemAnchorOption, MultipleDependencyAnchorOption,
                    NoSuchItemAnchorOption, NoSuchDependencyAnchorOption, BackwardsOption, MaxPathLengthOption
                }.Concat(
                    moreOptions).ToArray();
        }

        public void Parse([NotNull] GlobalContext globalContext, [CanBeNull] string argsAsString, 
            out bool backwardsX, out ItemMatch pathAnchorX, out bool pathAnchorIsCountMatchX,
            List<AbstractPathMatch<Dependency, Item>> expectedPathMatches,
            out AbstractPathMatch<Dependency, Item> countMatchX, out int? maxPathLengthX,
            [NotNull] [ItemNotNull] params OptionAction[] moreOptions) {
            bool backwards = false;
            ItemMatch pathAnchor = null;
            bool pathAnchorIsCountMatch = false;
            ////var expectedPathMatches = new List<AbstractPathMatch<Dependency, Item>>();
            var dontMatchesBeforeNextPathMatch = new List<AbstractPathMatch<Dependency, Item>>();
            AbstractPathMatch<Dependency, Item> countMatch = null;
            int? maxPathLength = null;

            Option.Parse(globalContext, argsAsString, new[] {
                BackwardsOption.Action((args, j) => {
                    backwards = true;
                    return j;
                }),
                PathItemAnchorOption.Action((args, j) => {
                    if (pathAnchor == null) {
                        pathAnchor =
                            new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item pattern"),
                                globalContext.IgnoreCase, anyWhereMatcherOk: true);
                    } else {
                        expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j,
                            multipleOccurrencesAllowed: false, mayContinue: true,
                            dontMatches: dontMatchesBeforeNextPathMatch));
                    }
                    return j;
                }),
                PathDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j,
                        multipleOccurrencesAllowed: false, mayContinue: true,
                        dontMatchesBeforeThis: dontMatchesBeforeNextPathMatch));
                    return j;
                }),
                CountItemAnchorOption.Action((args, j) => {
                    if (pathAnchor == null) {
                        pathAnchor =
                            new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "Missing item pattern"),
                                globalContext.IgnoreCase, anyWhereMatcherOk: true);
                        pathAnchorIsCountMatch = true;
                    } else {
                        expectedPathMatches.Add(
                            countMatch =
                                CreateItemPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false,
                                    mayContinue: true, dontMatches: dontMatchesBeforeNextPathMatch));
                    }
                    return j;
                }),
                CountDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(
                        countMatch =
                            CreateDependencyPathMatch(globalContext, args, ref j, multipleOccurrencesAllowed: false,
                                mayContinue: true, dontMatchesBeforeThis: dontMatchesBeforeNextPathMatch));
                    return j;
                }),
                MultipleItemAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateItemPathMatch(globalContext, args, ref j,
                        multipleOccurrencesAllowed: true, mayContinue: true, dontMatches: dontMatchesBeforeNextPathMatch));
                    return j;
                }),
                MultipleDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    expectedPathMatches.Add(CreateDependencyPathMatch(globalContext, args, ref j,
                        multipleOccurrencesAllowed: true, mayContinue: true,
                        dontMatchesBeforeThis: dontMatchesBeforeNextPathMatch));
                    return j;
                }),
                NoSuchItemAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    dontMatchesBeforeNextPathMatch.Add(CreateItemPathMatch(globalContext, args, ref j,
                        multipleOccurrencesAllowed: false, mayContinue: false,
                        dontMatches: new AbstractPathMatch<Dependency, Item>[0]));
                    return j;
                }),
                NoSuchDependencyAnchorOption.Action((args, j) => {
                    CheckPathAnchorSet(pathAnchor);
                    dontMatchesBeforeNextPathMatch.Add(CreateDependencyPathMatch(globalContext, args, ref j,
                        multipleOccurrencesAllowed: false, mayContinue: false,
                        dontMatchesBeforeThis: new AbstractPathMatch<Dependency, Item>[0]));
                    return j;
                }),
                MaxPathLengthOption.Action((args, j) => {
                    maxPathLength = Option.ExtractIntOptionValue(args, ref j, "Invalid maximum path length");
                    return j;
                })
            }.Concat(moreOptions).ToArray());
            backwardsX = backwards;
            pathAnchorX = pathAnchor;
            pathAnchorIsCountMatchX = pathAnchorIsCountMatch;
            countMatchX = countMatch;
            maxPathLengthX = maxPathLength;
        }

        // ReSharper disable once UnusedParameter.Local -- method is just a precondition check
        private void CheckPathAnchorSet(ItemMatch pathAnchor) {
            if (pathAnchor == null) {
                throw new ArgumentException($"First path pattern must be specified with {PathItemAnchorOption}");
            }
        }

        private static DependencyPathMatch<Dependency, Item> CreateDependencyPathMatch([NotNull] GlobalContext globalContext,
            string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue,
            IEnumerable<AbstractPathMatch<Dependency, Item>> dontMatchesBeforeThis) {
            return new DependencyPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"),
                globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed,
                mayContinue: mayContinue, dontMatchesBeforeThis: dontMatchesBeforeThis);
        }

        private static ItemPathMatch<Dependency, Item> CreateItemPathMatch([NotNull] GlobalContext globalContext,
            string[] args, ref int j, bool multipleOccurrencesAllowed, bool mayContinue,
            IEnumerable<AbstractPathMatch<Dependency, Item>> dontMatches) {
            return new ItemPathMatch<Dependency, Item>(Option.ExtractRequiredOptionValue(args, ref j, "missing anchor name"),
                globalContext.IgnoreCase, multipleOccurrencesAllowed: multipleOccurrencesAllowed, mayContinue: mayContinue,
                dontMatchesBeforeThis: dontMatches, anyWhereMatcherOk: true);
        }
    }
}