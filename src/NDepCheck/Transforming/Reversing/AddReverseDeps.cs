using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NDepCheck.Transforming.Reversing {
    public class AddReverseDeps : ITransformer {
        public string GetHelp(bool detailedHelp) {
            return @"Add reverse edges.

Configuration options: None

Transformer options: [-m &] [-u &] [-r]
  -m &    Regular expression matching usage of edges to reverse; default: match all
  -u &    Set usage of created edges; default: copy from reversed edge
  -r      Remove original edge
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
            string newUsage = null;
            bool removeOriginal = false;

            Options.Parse(transformOptions,
                new OptionAction('m', (args, j) => {
                    match = new Regex(Options.ExtractOptionValue(args, ref j));
                    return j;
                }), new OptionAction('u', (args, j) => {
                    newUsage = Options.ExtractOptionValue(args, ref j);
                    return j;
                }), new OptionAction('r', (args, j) => {
                    removeOriginal = true;
                    return j;
                }));

            foreach (var d in dependencies) {
                if (!removeOriginal) {
                    transformedDependencies.Add(d);
                }
                if (match == null || match.IsMatch(d.Usage ?? "")) {
                    transformedDependencies.Add(new Dependency(d.UsedItem, d.UsingItem, d.Source, newUsage ?? d.Usage,
                        d.Ct, d.QuestionableCt, d.BadCt, d.ExampleInfo, d.InputContext));
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
