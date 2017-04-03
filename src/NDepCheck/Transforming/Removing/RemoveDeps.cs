using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NDepCheck.Transforming.Removing {
    public class RemoveDeps : ITransformer {
        public string GetHelp(bool detailedHelp) {
            return @"Remove edges.

Configuration options: None

Transformer options: [-m &] [-i] [-o] [-q] [-b]
  -m &    Regular expression matching usage of edges to remove; default: match none
  -i      Remove single cycles
  -o      Remove edges with both bad and questionable edge count equal to zero
  -q      Remove edges with questionable edge count larger than zero
  -b      Remove edges with bad edge count larger than zero  
";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            // empty
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            // Only items are changed (Order is added)

            Regex match = null;
            bool removeSingleLoop = false;
            bool removeOk = false;
            bool removeQuestionable = false;
            bool removeBad = false;

            Options.Parse(transformOptions,
                new OptionAction('m', (args, j) => {
                    match = new Regex(Options.ExtractOptionValue(args, ref j));
                    return j;
                }), new OptionAction('i', (args, j) => {
                    removeSingleLoop = true;
                    return j;
                }), new OptionAction('o', (args, j) => {
                    removeOk = true;
                    return j;
                }), new OptionAction('q', (args, j) => {
                    removeQuestionable = true;
                    return j;
                }), new OptionAction('b', (args, j) => {
                    removeBad = true;
                    return j;
                }));

            foreach (var d in dependencies) {
                if (match != null && match.IsMatch(d.Usage ?? "") || removeSingleLoop && Equals(d.UsingItem, d.UsedItem) ||
                    removeOk && d.QuestionableCt == 0 && d.BadCt == 0 || removeQuestionable && d.QuestionableCt > 0 ||
                    removeBad && d.BadCt > 0) {
                    // remove
                } else {
                    transformedDependencies.Add(d);
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
                new Dependency(a, a, source:null, usage: "inherit", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source:null, usage: "inherit+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source:null, usage: "define", ct:5, questionableCt:0, badCt:2),
                new Dependency(b, b, source:null, usage: null, ct: 5, questionableCt:0, badCt:2),
            };
        }
    }
}
