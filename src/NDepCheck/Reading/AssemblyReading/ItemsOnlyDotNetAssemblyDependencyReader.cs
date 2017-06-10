using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Mono.Cecil;

namespace NDepCheck.Reading.AssemblyReading {
    public class ItemsOnlyDotNetAssemblyDependencyReader : AbstractDotNetAssemblyDependencyReader {
        public ItemsOnlyDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory readerFactory, string fileName)
            : base(readerFactory, fileName) {
        }

        public override IEnumerable<Dependency> ReadDependencies(WorkingGraph readingGraph, int depth, bool ignoreCase) {
            throw new NotImplementedException(); // TODO: gehört da eigentlich raus!
        }

        protected override IEnumerable<RawUsingItem> ReadUsingItems(int depth, WorkingGraph readingGraph) {
            Log.WriteInfo("Reading " + FullFileName);
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(FullFileName);

            try {
                assembly.MainModule.ReadSymbols();
            } catch (Exception ex) {
                Log.WriteWarning(
                        $"Loading symbols for assembly {FullFileName} failed - maybe .PDB file is missing. ({ex.Message})", FullFileName, 0);
            }

            ItemTail customSections = GetCustomSections(readingGraph, assembly.CustomAttributes, null);

            foreach (TypeDefinition type in assembly.MainModule.Types) {
                if (type.Name == "<Module>") {
                    continue;
                }

                foreach (var usingItem in AnalyzeType(type, customSections, readingGraph)) {
                    yield return usingItem;
                }
            }

            AssemblyNameDefinition currentAssembly = assembly.Name;
            yield return RawUsingItem.New(_rawUsingItemsCache, "", "", currentAssembly.Name, currentAssembly.Version.ToString(), currentAssembly.Culture, memberName: "", markers: null, tail: null, readingGraph: readingGraph);
        }

        private IEnumerable<RawUsingItem> AnalyzeType(TypeDefinition type, ItemTail parentCustomSections, WorkingGraph readingGraph) {
            ItemTail typeCustomSections = GetCustomSections(readingGraph, type.CustomAttributes, parentCustomSections);

            yield return GetClassItem(type, typeCustomSections, readingGraph);

            foreach (PropertyDefinition property in type.Properties) {
                foreach (var usingItem in AnalyzeProperty(property, typeCustomSections, readingGraph)) {
                    yield return usingItem;
                }
            }

            foreach (MethodDefinition method in type.Methods) {
                ItemTail methodCustomSections = GetCustomSections(readingGraph, method.CustomAttributes, typeCustomSections);
                yield return GetFullNameItem(type, method.Name, markers: null, customSections: methodCustomSections, readingGraph: readingGraph);
            }

            foreach (TypeDefinition nestedType in type.NestedTypes) {
                foreach (var usingItem in AnalyzeType(nestedType, typeCustomSections, readingGraph)) {
                    yield return usingItem;
                }
            }
        }

        private IEnumerable<RawUsingItem> AnalyzeProperty(PropertyDefinition property, ItemTail typeCustomSections, WorkingGraph readingGraph) {
            ItemTail propertyCustomSections = GetCustomSections(readingGraph, property.CustomAttributes, typeCustomSections);

            yield return GetFullNameItem(property.DeclaringType, property.Name, GET_MARKER, propertyCustomSections, readingGraph);
            yield return GetFullNameItem(property.DeclaringType, property.Name, SET_MARKER, propertyCustomSections, readingGraph);
        }

        [NotNull]
        private RawUsingItem GetClassItem(TypeReference typeReference, ItemTail customSections, WorkingGraph readingGraph) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);

            return RawUsingItem.New(_rawUsingItemsCache, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName: "", markers: null, tail: customSections, readingGraph: readingGraph);
        }

        [NotNull]
        private RawUsingItem GetFullNameItem(TypeReference typeReference, string memberName, string[] markers, ItemTail customSections, WorkingGraph readingGraph) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(_rawUsingItemsCache, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, customSections, readingGraph: readingGraph);
        }

        public override void SetReadersInSameReadFilesBeforeReadDependencies(IDependencyReader[] readerGang) {
            // empty
        }
    }
}