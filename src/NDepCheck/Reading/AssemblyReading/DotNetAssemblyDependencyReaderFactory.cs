using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Mono.Cecil;

namespace NDepCheck.Reading.AssemblyReading {
    public class DotNetAssemblyDependencyReaderFactory : AbstractReaderFactory {
        public static readonly ItemType DOTNETREF = ItemType.New(
            "DOTNETREF",
            new[] { "ASSEMBLY", "ASSEMBLY", "ASSEMBLY" },
            new[] { ".NAME", ".VERSION", ".CULTURE" }, ignoreCase: false, predefined: true);

        public static readonly ItemType DOTNETITEM = ItemType.New(
            "DOTNETITEM",
            new[] { "NAMESPACE", "CLASS", "ASSEMBLY", "ASSEMBLY", "ASSEMBLY", "MEMBER" },
            new[] { null, null, ".NAME", ".VERSION", ".CULTURE", ".NAME" }, ignoreCase: false, predefined: true);

        [UsedImplicitly]
        public DotNetAssemblyDependencyReaderFactory() {
            ItemType.ForceLoadingPredefinedType(DOTNETITEM);
            ItemType.ForceLoadingPredefinedType(DOTNETREF);
        }

        public override IDependencyReader CreateReader(string fileName, bool needsOnlyItemTails) {
            return needsOnlyItemTails
                ? (AbstractDependencyReader)new ItemsOnlyDotNetAssemblyDependencyReader(this, fileName)
                : new FullDotNetAssemblyDependencyReader(this, fileName);
        }

        [NotNull]
        public ItemType GetOrCreateDotNetType(string name, string[] keys, string[] subkeys) {
            return ItemType.New(name, keys, subkeys, ignoreCase: false);
        }

        public static void GetTypeAssemblyInfo(TypeReference reference, out string assemblyName, out string assemblyVersion, out string assemblyCulture) {
            switch (reference.Scope.MetadataScopeType) {
                case MetadataScopeType.AssemblyNameReference:
                    AssemblyNameReference r = (AssemblyNameReference)reference.Scope;
                    assemblyName = reference.Scope.Name;
                    assemblyVersion = r.Version.ToString();
                    assemblyCulture = r.Culture;
                    break;
                case MetadataScopeType.ModuleReference:
                    assemblyName = ((ModuleReference)reference.Scope).Name;
                    assemblyVersion = null;
                    assemblyCulture = null;
                    break;
                case MetadataScopeType.ModuleDefinition:
                    assemblyName = ((ModuleDefinition)reference.Scope).Assembly.Name.Name;
                    assemblyVersion = ((ModuleDefinition)reference.Scope).Assembly.Name.Version.ToString();
                    assemblyCulture = ((ModuleDefinition)reference.Scope).Assembly.Name.Culture;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            string result = @"Read data from .Net assembly file (.dll or .exe)

This reader returns items of two types:
    DOTNETITEM(NAMESPACE:CLASS:ASSEMBLY.NAME;ASSEMBLY.VERSION;ASSEMBLY.CULTURE:MEMBER.NAME)
    DOTNETREF(ASSEMBLY.NAME;ASSEMBLY.VERSION;ASSEMBLY.CULTURE)
";
            if (detailedHelp) {
                result += @"

The following constructs in .Net files are recognized:

___EXPLANATIONS MISSING___

...
* When a type N1.T1 in assembly A1 declares a field V of type N2.T2 in assembly A2, this yields 
** DOTNETITEM(N1:T1:A1:V) ---> DOTNETITEM(N2:T2:A2)
...
The files are read with Mono.Cecil.

";

            }
            return result;
        }

        private static readonly string[] _supportedFileExtensions = { ".dll", ".exe" };

        public override IEnumerable<string> SupportedFileExtensions => _supportedFileExtensions;
    }
}