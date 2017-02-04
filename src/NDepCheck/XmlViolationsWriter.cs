using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public static class XmlViolationsWriter {
        public static void WriteXmlOutput([NotNull]string path, [NotNull]IEnumerable<IInputContext> inputContexts) {
            var document = new XDocument(
                new XElement("Assemblies", // Should become "input file"
                    from ctx in inputContexts
                    select new XElement("Assembly",
                        new XElement("Filename", ctx.Filename),
                        new XElement("ErrorCount", ctx.ErrorCount),
                        new XElement("WarningCount", ctx.WarningCount),
                        new XElement("Violations",
                            from violation in ctx.RuleViolations
                            select new XElement(
                                "Violation",
                                new XElement("Type", violation.ViolationType),
                                new XElement("UsingItem", violation.Dependency.UsingItemAsString), // NACH VORN GELEGT - OK? (wir haben kein XSD :-) )
                                //new XElement("UsingNamespace", violation.Dependency.UsingNamespace),
                                new XElement("UsedItem", violation.Dependency.UsedItemAsString),
                                //new XElement("UsedNamespace", violation.Dependency.UsedNamespace),
                                new XElement("FileName", violation.Dependency.FileName),
                                new XElement("StartLine", violation.Dependency.StartLine),
                                new XElement("StartColumn", violation.Dependency.StartColumn),
                                new XElement("EndLine", violation.Dependency.EndLine),
                                new XElement("EndColumn", violation.Dependency.EndColumn)
                                ))
                        )
                    ));
            var settings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true };
            using (var xmlWriter = XmlWriter.Create(path, settings)) {
                document.Save(xmlWriter);
            }
        }
    }
}