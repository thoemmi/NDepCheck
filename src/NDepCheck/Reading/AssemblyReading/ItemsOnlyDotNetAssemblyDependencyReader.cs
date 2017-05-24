using System;
using System.Collections.Generic;
using Gibraltar;
using JetBrains.Annotations;
using Mono.Cecil;

namespace NDepCheck.Reading.AssemblyReading {
    public class ItemsOnlyDotNetAssemblyDependencyReader : AbstractDotNetAssemblyDependencyReader {
        public ItemsOnlyDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory factory, string fileName)
            : base(factory, fileName) {
        }

        public override IEnumerable<Dependency> ReadDependencies(Environment readingEnvironment, int depth, bool ignoreCase) {
            throw new NotImplementedException(); // TODO: gehört da eigentlich raus!
        }

        protected override IEnumerable<RawUsingItem> ReadUsingItems(int depth, Intern<ItemTail> itemTailCache) {
            Log.WriteInfo("Reading " + FullFileName);
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(FullFileName);

            try {
                assembly.MainModule.ReadSymbols();
            } catch (Exception ex) {
                Log.WriteWarning(
                        $"Loading symbols for assembly {FullFileName} failed - maybe .PDB file is missing. ({ex.Message})", FullFileName, 0);
            }

            ItemTail customSections = GetCustomSections(itemTailCache, assembly.CustomAttributes, null);

            foreach (TypeDefinition type in assembly.MainModule.Types) {
                if (type.Name == "<Module>") {
                    continue;
                }

                foreach (var usingItem in AnalyzeType(type, customSections, itemTailCache)) {
                    yield return usingItem;
                }
            }

            AssemblyNameDefinition currentAssembly = assembly.Name;
            yield return RawUsingItem.New(_rawUsingItemsCache, "", "", currentAssembly.Name, currentAssembly.Version.ToString(), currentAssembly.Culture, memberName: "", markers: null, tail: null);
        }

        private IEnumerable<RawUsingItem> AnalyzeType(TypeDefinition type, ItemTail parentCustomSections, Intern<ItemTail> itemTailCache) {
            ItemTail typeCustomSections = GetCustomSections(itemTailCache, type.CustomAttributes, parentCustomSections);

            yield return GetClassItem(type, typeCustomSections);

            foreach (PropertyDefinition property in type.Properties) {
                foreach (var usingItem in AnalyzeProperty(property, typeCustomSections, itemTailCache)) {
                    yield return usingItem;
                }
            }

            foreach (MethodDefinition method in type.Methods) {
                ItemTail methodCustomSections = GetCustomSections(itemTailCache, method.CustomAttributes, typeCustomSections);
                yield return GetFullNameItem(type, method.Name, markers: null, customSections: methodCustomSections);
            }

            foreach (TypeDefinition nestedType in type.NestedTypes) {
                foreach (var usingItem in AnalyzeType(nestedType, typeCustomSections, itemTailCache)) {
                    yield return usingItem;
                }
            }
        }

        private IEnumerable<RawUsingItem> AnalyzeProperty(PropertyDefinition property, ItemTail typeCustomSections, Intern<ItemTail> itemTailCache) {
            ItemTail propertyCustomSections = GetCustomSections(itemTailCache, property.CustomAttributes, typeCustomSections);

            yield return GetFullNameItem(property.DeclaringType, property.Name, GET_MARKER, propertyCustomSections);
            yield return GetFullNameItem(property.DeclaringType, property.Name, SET_MARKER, propertyCustomSections);
        }

        [NotNull]
        private RawUsingItem GetClassItem(TypeReference typeReference, ItemTail customSections) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);

            return RawUsingItem.New(_rawUsingItemsCache, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName: "", markers: null, tail: customSections);
        }

        [NotNull]
        private RawUsingItem GetFullNameItem(TypeReference typeReference, string memberName, string[] markers, ItemTail customSections) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(_rawUsingItemsCache, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, customSections);
        }

        public override void SetReadersInSameReadFilesBeforeReadDependencies(IDependencyReader[] readerGang) {
            // empty
        }
    }
}