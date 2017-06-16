using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Rendering.TextWriting {
    public class RuleViolationWriter : RendererWithOptions<RuleViolationWriter.Options> {
        public class Options {
            public bool XmlOutput;
            public bool SimpleRuleOutput;
            public bool NewLine;
        }

        public static readonly Option XmlOutputOption = new Option("xo", "xml-output", "", "Write output to XML file", @default: false);
        public static readonly Option RuleOutputOption = new Option("ro", "rule-output", "", "Write output in rule format", @default: false);
        public static readonly Option NewlineOption = new Option("nl", "newline", "", "Write violations on three lines", @default: false);

        private static readonly Option[] _allOptions = { XmlOutputOption, NewlineOption };

        protected override Options CreateRenderOptions(GlobalContext globalContext, string options) {
            return ParseArgs(globalContext, options);
        }

        public override void Render([NotNull] GlobalContext globalContext,
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Options options,
            [NotNull] WriteTarget target, bool ignoreCase) {

            int violationsCount = dependencies.Count(d => d.NotOkCt > 0);

            if (target.IsConsoleOut) {
                var consoleLogger = new ConsoleLogger();
                foreach (var d in dependencies.Where(d => d.QuestionableCt > 0 && d.BadCt == 0)) {
                    consoleLogger.WriteViolation(d, options.SimpleRuleOutput);
                }
                foreach (var d in dependencies.Where(d => d.BadCt > 0)) {
                    consoleLogger.WriteViolation(d, options.SimpleRuleOutput);
                }
            } else if (options.XmlOutput) {
                var document = new XDocument(
                new XElement("Violations",
                    from dependency in dependencies where dependency.NotOkCt > 0
                    select new XElement(
                        "Violation",
                        new XElement("Type", dependency.BadCt > 0 ? "Bad" : "Questionable"),
                        new XElement("UsingItem", dependency.UsingItemAsString), // NACH VORN GELEGT - OK? (wir haben kein XSD :-) )
                                                                                 //new XElement("UsingNamespace", violation.Dependency.UsingNamespace),
                        new XElement("UsedItem", dependency.UsedItemAsString),
                        //new XElement("UsedNamespace", violation.Dependency.UsedNamespace),
                        new XElement("FileName", dependency.Source)
                        ))
                    );
                var settings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true };
                WriteTarget writeTarget = GetXmlFile(target);
                Log.WriteInfo($"Writing {violationsCount} violations to {writeTarget}");
                using (var xmlWriter = XmlWriter.Create(writeTarget.FullFileName, settings)) {
                    document.Save(xmlWriter);
                }
            } else {
                WriteTarget writeTarget = GetTextFile(target);
                Log.WriteInfo($"Writing {violationsCount} violations to {writeTarget }");
                using (var sw = writeTarget.CreateWriter()) {
                    RenderToTextWriter(dependencies, sw, options.SimpleRuleOutput, options.NewLine);
                }
            }
        }

        private static WriteTarget GetTextFile(WriteTarget target) {
            return GlobalContext.CreateFullFileName(target, null);
        }

        private static WriteTarget GetXmlFile(WriteTarget target) {
            return target.ChangeExtension(".xml");
        }

        private static Options ParseArgs([NotNull] GlobalContext globalContext, [CanBeNull] string options) {
            var result = new Options();
            Option.Parse(globalContext, options,
                XmlOutputOption.Action((args, j) => {
                    result.XmlOutput = true;
                    return j;
                }),
                RuleOutputOption.Action((args, j) => {
                    result.SimpleRuleOutput = true;
                    return j;
                }),
                NewlineOption.Action((args, j) => {
                    result.NewLine = true;
                    return j;
                }));
            return result;
        }

        private static void RenderToTextWriter([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, ITargetWriter sw, bool simpleRuleOutput, bool newLine) {
            sw.WriteLine($"// Written {DateTime.Now} by {typeof(RuleViolationWriter).Name} in NDepCheck {Program.VERSION}");
            foreach (var d in dependencies.Where(d => d.NotOkCt > 0)) {
                sw.WriteLine(d.NotOkMessage(simpleRuleOutput: simpleRuleOutput, newLine: newLine));
            }
        }

        public override void RenderToStreamForUnitTests([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            Stream stream, string testOption) {
            using (var sw = new TargetStreamWriter(stream)) {
                RenderToTextWriter(dependencies, sw, false, false);
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph renderingGraph) {
            ItemType simple = ItemType.New("SIMPLE(Name)");
            Item root = renderingGraph.CreateItem(simple, "root");
            Item ok = renderingGraph.CreateItem(simple, "ok");
            Item questionable = renderingGraph.CreateItem(simple, "questionable");
            Item bad = renderingGraph.CreateItem(simple, "bad");
            return new[] {
                renderingGraph.CreateDependency(root, ok, new TextFileSourceLocation("Test", 1), "Use", 4, 0, 0, "to root"),
                renderingGraph.CreateDependency(root, questionable, new TextFileSourceLocation("Test", 1), "Use", 4, 1, 0, "to questionable"),
                renderingGraph.CreateDependency(root, bad, new TextFileSourceLocation("Test", 1), "Use", 4, 2, 1, "to bad")
            };
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes dependency rule violations to file in text or xml format.
  This is the output for the primary reason of NDepCheck: Checking rules.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public override WriteTarget GetMasterFileName([NotNull] GlobalContext globalContext, Options options, WriteTarget baseTarget) {
            return options.XmlOutput ? GetXmlFile(baseTarget) : GetTextFile(baseTarget);
        }
    }
}
