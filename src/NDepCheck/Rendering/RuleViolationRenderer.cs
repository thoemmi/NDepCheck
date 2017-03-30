using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NDepCheck.Rendering {
    public class RuleViolationRenderer : IDependencyRenderer {
        public string Render(IEnumerable<Dependency> dependencies, string argsAsString, string baseFilename) {
            bool xmlOutput = false;
            Options.Parse(argsAsString,
                new OptionAction('x', (args, j) => {
                    xmlOutput = true;
                    return j;
                }));

            if (baseFilename == null || GlobalContext.IsConsoleOutFileName(baseFilename)) {
                var consoleLogger = new ConsoleLogger();
                foreach (var d in dependencies.Where(d => d.QuestionableCt > 0 && d.BadCt == 0)) {
                    consoleLogger.WriteViolation(d);
                }
                foreach (var d in dependencies.Where(d => d.BadCt > 0)) {
                    consoleLogger.WriteViolation(d);
                }
                return "-";
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
                string fileName = Path.ChangeExtension(baseFilename, ".xml");
                using (var xmlWriter = XmlWriter.Create(fileName, settings)) {
                    document.Save(xmlWriter);
                }
                return fileName;
            } else {
                using (var sw = new StreamWriter(baseFilename)) {
                    RenderToStreamWriter(dependencies, sw);
                }
                return baseFilename;
            }
        }

        private static void RenderToStreamWriter(IEnumerable<Dependency> dependencies, StreamWriter sw) {
            foreach (var d in dependencies.Where(d => d.BadCt > 0)) {
                sw.WriteLine(d.BadDependencyMessage());
            }
            foreach (var d in dependencies.Where(d => d.QuestionableCt > 0 && d.BadCt == 0)) {
                sw.WriteLine(d.QuestionableDependencyMessage());
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

        public string GetHelp(bool detailedHelp) {
            return
@"  Writes dependency rule violations to file in text or xml format.
  This is the output for the primary reason of NDepCheck: Checking rules.

  Options: [-x]
    -x            write XML output (not possible with fileName '-', i.e. standard output)";
        }
    }
}
