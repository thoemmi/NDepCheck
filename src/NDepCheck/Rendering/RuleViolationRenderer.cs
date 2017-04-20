using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public class RuleViolationRenderer : IRenderer {
        public static readonly Option XmlOutputOption = new Option("xo", "xml-output", "", "Write output to XML file", @default: false);

        private static readonly Option[] _allOptions = { XmlOutputOption };

        public void Render([NotNull] GlobalContext globalContext, IEnumerable<Dependency> dependencies, int? dependenciesCount, string argsAsString, string baseFileName, bool ignoreCase) {
            bool xmlOutput = ParseArgs(globalContext, argsAsString);

            if (baseFileName == null || GlobalContext.IsConsoleOutFileName(baseFileName)) {
                var consoleLogger = new ConsoleLogger();
                foreach (var d in dependencies.Where(d => d.QuestionableCt > 0 && d.BadCt == 0)) {
                    consoleLogger.WriteViolation(d);
                }
                foreach (var d in dependencies.Where(d => d.BadCt > 0)) {
                    consoleLogger.WriteViolation(d);
                }
            } else if (xmlOutput) {
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
                string fileName = GetXmlFileName(baseFileName);
                Log.WriteInfo("Writing violations to " + fileName);
                using (var xmlWriter = XmlWriter.Create(fileName, settings)) {
                    document.Save(xmlWriter);
                }
            } else {
                string fileName = GetTextFileName(baseFileName);
                Log.WriteInfo("Writing violations to " + fileName);
                using (var sw = new StreamWriter(fileName)) {
                    RenderToStreamWriter(dependencies, sw);
                }
            }
        }

        private static string GetTextFileName(string baseFileName) {
            return GlobalContext.CreateFullFileName(baseFileName, null);
        }

        private static string GetXmlFileName(string baseFileName) {
            return Path.ChangeExtension(baseFileName, ".xml");
        }

        private static bool ParseArgs([NotNull] GlobalContext globalContext, [CanBeNull] string argsAsString) {
            bool xmlOutput = false;
            Option.Parse(globalContext, argsAsString,
                XmlOutputOption.Action((args, j) => {
                    xmlOutput = true;
                    return j;
                }));
            return xmlOutput;
        }

        private static void RenderToStreamWriter(IEnumerable<Dependency> dependencies, StreamWriter sw) {
            foreach (var d in dependencies.Where(d => d.NotOkCt > 0)) {
                sw.WriteLine(d.NotOkMessage());
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Dependency> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                RenderToStreamWriter(dependencies, sw);
            }
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType simple = ItemType.New("Simple:Name");
            Item root = Item.New(simple, "root");
            Item ok = Item.New(simple, "ok");
            Item questionable = Item.New(simple, "questionable");
            Item bad = Item.New(simple, "bad");
            items = new[] { root, ok, questionable, bad };
            dependencies = new[] {
                new Dependency(root, ok, new TextFileSource("Test", 1), "Use", 4, 0, 0, "to root"),
                new Dependency(root, questionable, new TextFileSource("Test", 1), "Use", 4, 1, 0, "to questionable"),
                new Dependency(root, bad, new TextFileSource("Test", 1), "Use", 4, 2, 1, "to bad")

                };
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Writes dependency rule violations to file in text or xml format.
  This is the output for the primary reason of NDepCheck: Checking rules.

{Option.CreateHelp(_allOptions, detailedHelp, filter)}";
        }

        public string GetMasterFileName(GlobalContext globalContext, string argsAsString, string baseFileName) {
            bool xmlOutput = ParseArgs(globalContext, argsAsString);
            return xmlOutput ? GetXmlFileName(baseFileName) : GetTextFileName(baseFileName);
        }
    }
}
