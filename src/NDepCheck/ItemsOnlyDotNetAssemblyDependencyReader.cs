using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Mono.Cecil;

namespace NDepCheck {
    public class ItemsOnlyDotNetAssemblyDependencyReader : AbstractDotNetAssemblyDependencyReader {
        public ItemsOnlyDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory factory, string filename, Options options)
            : base(factory, filename, options) {
        }

        protected override IEnumerable<Dependency> ReadDependencies() {
            throw new NotImplementedException(); // TODO: gehört da eigentlich raus!
        }

        protected override IEnumerable<RawUsingItem> ReadUsingItems() {

            Log.WriteInfo("Reading " + _filename);
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(_filename);

            try {
                assembly.MainModule.ReadSymbols();
            } catch (Exception ex) {
                Log.WriteWarning(
                        $"Loading symbols for assembly {_filename} failed - maybe .PDB file is missing. ({ex.Message})", _filename, 0);
            }

            ItemTail customSections = GetCustomSections(assembly.CustomAttributes, null);

            foreach (TypeDefinition type in assembly.MainModule.Types) {
                if (type.Name == "<Module>") {
                    continue;
                }

                foreach (var usingItem in AnalyzeType(type, customSections)) {
                    yield return usingItem;
                }
            }

            AssemblyNameDefinition currentAssembly = assembly.Name;
            yield return new RawUsingItem("", "", currentAssembly.Name, currentAssembly.Version.ToString(), currentAssembly.Culture, "", "", null);
        }

        private IEnumerable<RawUsingItem> AnalyzeType(TypeDefinition type, ItemTail parentCustomSections) {
            ItemTail typeCustomSections = GetCustomSections(type.CustomAttributes, parentCustomSections);

            yield return GetClassItem(type, typeCustomSections);

            foreach (PropertyDefinition property in type.Properties) {
                foreach (var usingItem in AnalyzeProperty(property, typeCustomSections)) {
                    yield return usingItem;
                }
            }

            foreach (MethodDefinition method in type.Methods) {
                ItemTail methodCustomSections = GetCustomSections(method.CustomAttributes, typeCustomSections);
                yield return GetFullnameItem(type, method.Name, "", methodCustomSections);
            }

            foreach (TypeDefinition nestedType in type.NestedTypes) {
                foreach (var usingItem in AnalyzeType(nestedType, typeCustomSections)) {
                    yield return usingItem;
                }
            }
        }

        private IEnumerable<RawUsingItem> AnalyzeProperty(PropertyDefinition property, ItemTail typeCustomSections) {
            ItemTail propertyCustomSections = GetCustomSections(property.CustomAttributes, typeCustomSections);

            yield return GetFullnameItem(property.DeclaringType, property.Name,"get", propertyCustomSections);
            yield return GetFullnameItem(property.DeclaringType, property.Name, "set", propertyCustomSections);
        }

        [NotNull]
        private RawUsingItem GetClassItem(TypeReference typeReference, ItemTail customSections) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);

            return new RawUsingItem(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, "", "", customSections);
        }

        [NotNull]
        private RawUsingItem GetFullnameItem(TypeReference typeReference, string memberName, string memberSort, ItemTail customSections) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return new RawUsingItem(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, memberSort, customSections);
        }
    }
}