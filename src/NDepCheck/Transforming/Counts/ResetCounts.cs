
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NDepCheck.Transforming.Counts {
    public class ResetCounts : ITransformer {
        public string GetHelp(bool detailedHelp) {
            return @"Reset counts on edges.

Configuration options: None

Transformer options: [-m &] [-q] [-b]
  -m &    Regular expression matching usage of edges to clear; default: match all
  -q      Reset questionable count to zero
  -b      Reset bad count to zero
";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            // empty
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            // Only items are changed (Order is added)
            transformedDependencies.AddRange(dependencies);

            bool resetQuestionable = false;
            bool resetBad = false;
            Regex match = null;

            Options.Parse(transformOptions,
                new OptionAction('m', (args, j) => {
                    match = new Regex(Options.ExtractOptionValue(args, ref j));
                    return j;
                }), new OptionAction('q', (args, j) => {
                    resetQuestionable = true;
                    return j;
                }), new OptionAction('b', (args, j) => {
                    resetBad = true;
                    return j;
                }));

            foreach (var d in dependencies) {
                if (match == null || match.IsMatch(d.Usage ?? "")) {
                    if (resetQuestionable) {
                        d.ResetQuestionableCt();
                    } else if (resetBad) {
                        d.ResetBadCt();
                    }
                }
            }

            return Program.OK_RESULT;
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source:null, usage: null, ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source:null, usage: "use+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source:null, usage: "define", ct:5, questionableCt:0, badCt:2),
            };
        }
    }
}
