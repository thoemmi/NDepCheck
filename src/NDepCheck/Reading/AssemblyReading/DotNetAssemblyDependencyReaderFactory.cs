using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Mono.Cecil;

namespace NDepCheck.Reading.AssemblyReading {
    public class DotNetAssemblyDependencyReaderFactory : AbstractReaderFactory {
        public static readonly ItemType DOTNETASSEMBLY = ItemType.New(
            "DOTNETASSEMBLY",
            new[] { "Assembly", "Assembly", "Assembly" },
            new[] { ".Name", ".Version", ".Culture" }, ignoreCase: false, predefined: true);

        private static readonly string[] _dotNetItemKeys = { "Namespace", "Class", "Assembly", "Assembly", "Assembly", "Member" };
        private static readonly string[] _dotNetItemSubkeys = { null, null, ".Name", ".Version", ".Culture", ".Name" };

        public static readonly ItemType DOTNETITEM = ItemType.New("DOTNETITEM", _dotNetItemKeys, _dotNetItemSubkeys,
                                                                  ignoreCase: false, predefined: true);
        public static readonly ItemType DOTNETTYPE = ItemType.New("DOTNETTYPE", _dotNetItemKeys, _dotNetItemSubkeys,
                                                                  ignoreCase: false, predefined: true);
        public static readonly ItemType DOTNETFIELD = ItemType.New("DOTNETFIELD", _dotNetItemKeys, _dotNetItemSubkeys,
                                                                  ignoreCase: false, predefined: true);
        public static readonly ItemType DOTNETMETHOD = ItemType.New("DOTNETMETHOD", _dotNetItemKeys, _dotNetItemSubkeys,
                                                                  ignoreCase: false, predefined: true);
        public static readonly ItemType DOTNETPROPERTY = ItemType.New("DOTNETPROPERTY", _dotNetItemKeys, _dotNetItemSubkeys,
                                                                  ignoreCase: false, predefined: true);
        public static readonly ItemType DOTNETEVENT = ItemType.New("DOTNETEVENT", _dotNetItemKeys, _dotNetItemSubkeys,
                                                                  ignoreCase: false, predefined: true);
        public static readonly ItemType DOTNETVARIABLE = ItemType.New("DOTNETVARIABLE", _dotNetItemKeys, _dotNetItemSubkeys,
                                                                  ignoreCase: false, predefined: true);
        public static readonly ItemType DOTNETPARAMETER = ItemType.New("DOTNETPARAMETER", _dotNetItemKeys, _dotNetItemSubkeys,
                                                          ignoreCase: false, predefined: true);

        private static readonly string[] _supportedFileExtensions = { ".dll", ".exe" };



        [UsedImplicitly]
        public DotNetAssemblyDependencyReaderFactory() {
            ItemType.ForceLoadingPredefinedType(DOTNETASSEMBLY);
            ItemType.ForceLoadingPredefinedType(DOTNETITEM);
            ItemType.ForceLoadingPredefinedType(DOTNETEVENT);
            ItemType.ForceLoadingPredefinedType(DOTNETFIELD);
            ItemType.ForceLoadingPredefinedType(DOTNETITEM);
            ItemType.ForceLoadingPredefinedType(DOTNETMETHOD);
            ItemType.ForceLoadingPredefinedType(DOTNETPARAMETER);
            ItemType.ForceLoadingPredefinedType(DOTNETPROPERTY);
            ItemType.ForceLoadingPredefinedType(DOTNETTYPE);
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

The files are read with Mono.Cecil.
This reader returns items of the various specific types, all of which have the 
following fields:
    (NAMESPACE:CLASS:Assembly.Name;Assembly.VERSION;Assembly.CULTURE:MEMBER.Name)
";
            if (detailedHelp) {
                result += @"

The reader creates items of the following types:
    DOTNETTYPE (member name is always empty)
    DOTNETEVENT, DOTNETFIELD, DOTNETMETHOD, DOTNETPARAMETER, DOTNETPROPERTY, and
    DOTNETVARIABLE.

Moreover, ___IN WHICH CASES ??__ items for assemblies are returned:
    DOTNETASSEMBLY(Assembly.Name;Assembly.VERSION;Assembly.CULTURE)

Created items are marked with the following markers, where appropriate:

                                           DOTNET...
                      TYPE   METHOD EVENT  PARAMETER  VARIABLE  FIELD  PROPERTY
                                  (untested) (buggy)  ......(untested).........
_abstract               x      x
_array                  x
_class                  x
_const                  
_ctor                          x
_definition             x      x      x                           x      x
_delegate               
_enum                   x
_get                           x
_in                                          x
_interface              x
_nested                 x
_notpublic              x
_optional               x                    x
_out                                         x
_pinned                 x                               x
_primitive              x
_private                       x                                  x
_public                 x      x                                  x
_readonly                                                         x
_returnvalue                                 x
_sealed                 x      x
_set                           x
_static                        x                                  x
_struct                 x
_virtual                       x

Dependencies from one item to another are marked with the following markers, where appropriate:

                      DOTNET...
_declaresevent        TYPE -> EVENT
_declaresfield        TYPE -> FIELD
_declaresparameter    METHOD -> PARAMETER
_declaresreturntype   METHOD -> TYPE
_declaresvariable     METHOD -> VARIABLE
_directlyimplements   TYPE -> TYPE
_directlyderivedfrom  TYPE -> TYPE
_usesmember           TYPE -> all, METHOD -> all, PROPERTY -> all
_usestype             all -> TYPE
";
            }
            return result;
        }

        public override IEnumerable<string> SupportedFileExtensions => _supportedFileExtensions;
    }
}