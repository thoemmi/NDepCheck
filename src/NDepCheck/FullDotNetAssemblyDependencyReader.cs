using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NDepCheck {
    public class FullDotNetAssemblyDependencyReader : AbstractDotNetAssemblyDependencyReader {
        private IEnumerable<RawDependency> _rawDependencies;

        //private static readonly List<RawDependency> _tempAll = new List<RawDependency>();

        public FullDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory factory, string filename, Options options)
            : base(factory, filename, options) {
        }

        protected override IEnumerable<Dependency> ReadDependencies(int depth) {
            return GetOrReadRawDependencies(depth).Where(d => d.UsedItem != null).Select(d => d.ToDependencyWithTail(_options, depth));
        }

        private IEnumerable<RawDependency> GetOrReadRawDependencies(int depth) {
            if (_rawDependencies == null) {
                _rawDependencies = ReadRawDependencies(depth).ToArray();
                //_tempAll.AddRange(_rawDependencies); // -------------------------------------------
            }
            return _rawDependencies;
        }

        protected override IEnumerable<RawUsingItem> ReadUsingItems(int depth) {
            return GetOrReadRawDependencies(depth).Select(d => d.UsingItem);
        }

        protected IEnumerable<RawDependency> ReadRawDependencies(int depth) {
            Log.WriteInfo(new string(' ', 2 * depth) + "Reading " + _filename);
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(_filename);

            try {
                assembly.MainModule.ReadSymbols();
            } catch (Exception ex) {
                Log.WriteWarning($"Loading symbols for assembly {_filename} failed - maybe .PDB file is missing. ({ex.Message})", _filename, 0);
            }

            ItemTail customSections = GetCustomSections(assembly.CustomAttributes, null);

            foreach (TypeDefinition type in assembly.MainModule.Types) {
                if (type.Name == "<Module>") {
                    continue;
                }

                foreach (var dependency in AnalyzeType(type, customSections)) {
                    yield return dependency;
                }
            }

            //AssemblyNameDefinition currentAssembly = assembly.Name;

            //foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences) {
            //    yield return
            //        RawDependency.New(DotNetAssemblyDependencyReaderFactory.DOTNETREF,
            //            new RawUsingItem(null, null, currentAssembly.Name, currentAssembly.Version.ToString(), currentAssembly.Culture, null, null),
            //            new RawUsedItem(null, null, reference.Name, reference.Version.ToString(), reference.Culture, null),
            //            null);
            //}
        }

        [NotNull]
        private IEnumerable<RawDependency> AnalyzeType([NotNull] TypeDefinition type, [CanBeNull] ItemTail parentCustomSections) {
            ItemTail typeCustomSections = GetCustomSections(type.CustomAttributes, parentCustomSections);
            {
                RawUsingItem usingItem = GetClassItem(type, typeCustomSections);
                ////??? yield return RawDependency.New(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, null, null);

                foreach (var dependency_ in AnalyzeCustomAttributes(usingItem, type.CustomAttributes)) {
                    yield return dependency_;
                }

                if (type.BaseType != null && !IsLinked(type.BaseType, type.DeclaringType)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, type.BaseType, sequencePoint: null)) {
                        yield return dependency_;
                    }
                }

                foreach (TypeReference interfaceRef in type.Interfaces) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, interfaceRef, sequencePoint: null)) {
                        yield return dependency_;
                    }
                }

                foreach (FieldDefinition field in type.Fields) {
                    //if (IsLinked(field.FieldType, type.DeclaringType)) {
                    ////ItemTail fieldCustomSections = GetCustomSections(field.CustomAttributes, typeCustomSections);
                    ////??yield return RawDependency.New(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, GetFullnameItem(type, field.Name, "", fieldCustomSections), null, null);

                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, field.FieldType, sequencePoint: null)) {
                        yield return dependency_;
                    }
                    //}
                }

                foreach (EventDefinition @event in type.Events) {
                    ////ItemTail eventCustomSections = GetCustomSections(@event.CustomAttributes, typeCustomSections);
                    ////??yield return RawDependency.New(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, GetFullnameItem(type, @event.Name, "", eventCustomSections), null, null);

                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, @event.EventType, sequencePoint: null)) {
                        yield return dependency_;
                    }
                }

                foreach (var property in type.Properties) {
                    //if (!IsLinked(property.PropertyType, type.DeclaringType)) {
                    foreach (var dependency_ in AnalyzeProperty(type, usingItem, property, typeCustomSections)) {
                        yield return dependency_;
                    }
                    //}
                }

            }

            foreach (MethodDefinition method in type.Methods) {
                ItemTail methodCustomSections = GetCustomSections(method.CustomAttributes, typeCustomSections);

                RawUsingItem usingItem = GetFullnameItem(type, method.Name, "", methodCustomSections);
                ////??yield return RawDependency.New(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, null, null);

                foreach (var dependency_ in AnalyzeMethod(type, usingItem, method)) {
                    yield return dependency_;
                }
            }

            foreach (TypeDefinition nestedType in type.NestedTypes) {
                foreach (var dependency_ in AnalyzeType(nestedType, typeCustomSections)) {
                    yield return dependency_;
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeProperty([NotNull] TypeDefinition owner, [NotNull] RawUsingItem usingItem,
                                                           [NotNull] PropertyDefinition property, [CanBeNull] ItemTail typeCustomSections) {
            ItemTail propertyCustomSections = GetCustomSections(property.CustomAttributes, typeCustomSections);

            foreach (var dependency_ in AnalyzeCustomAttributes(usingItem, property.CustomAttributes)) {
                yield return dependency_;
            }

            foreach (ParameterDefinition parameter in property.Parameters) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, parameter.ParameterType, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            if (property.PropertyType != null) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, property.PropertyType, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            foreach (var dependency in AnalyzeGetterSetter(owner, property, "get", propertyCustomSections, property.GetMethod)) {
                yield return dependency;
            }

            foreach (var dependency in AnalyzeGetterSetter(owner, property, "set", propertyCustomSections, property.SetMethod)) {
                yield return dependency;
            }
        }

        private IEnumerable<RawDependency> AnalyzeCustomAttributes([NotNull] RawUsingItem usingItem,
                                                                   [NotNull] IEnumerable<CustomAttribute> customAttributes) {
            foreach (CustomAttribute customAttribute in customAttributes) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, customAttribute.Constructor.DeclaringType,
                    sequencePoint: null)) {
                    yield return dependency_;
                }
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, customAttribute.Constructor.DeclaringType,
                    memberName: customAttribute.Constructor.Name, sequencePoint: null)) {
                    yield return dependency_;
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeGetterSetter([NotNull] TypeDefinition owner, [NotNull] PropertyDefinition property,
                                                               [NotNull] string sort, [CanBeNull] ItemTail propertyCustomSections,
                                                               [CanBeNull] MethodDefinition getterSetter) {
            if (getterSetter != null) {
                RawUsingItem usingItem = GetFullnameItem(property.DeclaringType, property.Name, sort, propertyCustomSections);
                ////??yield return RawDependency.New(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, null, null);

                foreach (var dependency in AnalyzeMethod(owner, usingItem, getterSetter)) {
                    yield return dependency;
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeMethod([NotNull] TypeDefinition owner, [NotNull] RawUsingItem usingItem,
                                                         [NotNull] MethodDefinition method) {
            foreach (var dependency_ in AnalyzeCustomAttributes(usingItem, method.CustomAttributes)) {
                yield return dependency_;
            }

            foreach (ParameterDefinition parameter in method.Parameters) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, parameter.ParameterType, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            if (method.ReturnType != null && !IsLinked(method.ReturnType, method.DeclaringType.DeclaringType)) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, method.ReturnType, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            if (method.HasBody) {
                foreach (var variable in method.Body.Variables) {
                    if (!IsLinked(variable.VariableType, method.DeclaringType.DeclaringType)) {
                        foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, variable.VariableType, sequencePoint: null)) {
                            yield return dependency_;
                        }
                    }
                }

                SequencePoint mostRecentSeqPoint = null;
                foreach (Instruction instruction in method.Body.Instructions) {
                    if (instruction.SequencePoint != null) {
                        mostRecentSeqPoint = instruction.SequencePoint;
                    }
                    foreach (var dependency_ in AnalyzeInstruction(owner, usingItem, instruction, mostRecentSeqPoint)) {
                        yield return dependency_;
                    }
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeInstruction([NotNull] TypeDefinition owner, [NotNull] RawUsingItem usingItem,
                                                              [NotNull] Instruction instruction, [CanBeNull] SequencePoint sequencePoint) {
            {
                var methodReference = instruction.Operand as MethodReference;
                // Durch die !IsLinked-Bedingung wird der Test MainTests.Exit0Aspects mit den Calls
                // zwischen Nested-Klassen in NDepCheck.TestAssembly.cs - Klasse Class14.Class13EInner2, Methode SpecialMethodOfInnerClass
                // rot: Weil die Calls zwischen der Class14 und der Nested Class wegen IsLinked==true hier einfach ausgefiltert
                // werden ...
                // WIESO SOLL DAS SO SEIN? - bitte um Erklärung! ==> SIehe nun temporäre Änderung an IsLinked - IST DAS OK?
                if (methodReference != null && !IsLinked(methodReference.DeclaringType, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, methodReference.DeclaringType,
                        memberName: methodReference.Name, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                    foreach (var dependency_ in
                            CreateTypeAndMethodDependencies(usingItem, methodReference.ReturnType, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                }
            }
            {
                var field = instruction.Operand as FieldDefinition;
                if (field != null && !IsLinked(field.DeclaringType, owner)) {
                    foreach (var dependency_ in
                        CreateTypeAndMethodDependencies(usingItem, field.FieldType, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, field.DeclaringType,
                        memberName: field.Name, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                }
            }
            {

                var property = instruction.Operand as PropertyDefinition;
                if (property != null && !IsLinked(property.DeclaringType, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, property.PropertyType, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, property.DeclaringType,
                                                                        memberName: property.Name, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                }

            }
            {
                var typeref = instruction.Operand as TypeReference;
                if (typeref != null && !IsLinked(typeref, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, typeref, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                }
            }
        }

        private static bool IsLinked([CanBeNull] TypeReference referringType, [CanBeNull] TypeReference referrer) {
            if (referrer == null || referringType == null) {
                return false;
            }
            if (referringType == referrer) {
                return true;
            }

            //// Ich habe hier die Suche über DeclaringType einmal rausgenommen; Tests funktionieren genausogut,
            //// und auch ein Durchlauf über das Framework schaut m.E. korrekt aus!
            //// Eigentlich glaube ich auch, dass das return true oben nicht ok ist: Auch wenn beide Typen gleich
            //// sind, muss eine Dependency_ entstehen und geprüft werden - wieso soll es hier eine Ausnahme geben???
            return false;
            ////return IsLinked(referringType, referrer.DeclaringType) || IsLinked(referringType.DeclaringType, referrer);
        }

        private RawUsingItem GetClassItem([NotNull] TypeReference typeReference, [CanBeNull] ItemTail customSections) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, "", "", customSections);
        }

        private RawUsedItem GetFullnameItem([NotNull] TypeReference typeReference, [NotNull] string memberName, string memberSort) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsedItem.New(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, memberSort);
        }

        private RawUsingItem GetFullnameItem([NotNull] TypeReference typeReference, [NotNull] string memberName, string memberSort, [CanBeNull] ItemTail customSections) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, memberSort, customSections);
        }

        /// <summary>
        /// Create a single dependency to the calledType or (if passed) calledType+method.
        /// Create additional dependencies for each generic parameter type of calledType.
        /// </summary>
        private IEnumerable<RawDependency> CreateTypeAndMethodDependencies([NotNull] RawUsingItem usingItem, [NotNull] TypeReference usedType,
                                                               [CanBeNull] SequencePoint sequencePoint, [NotNull] string memberName = "",
                                                               [NotNull] string memberSort = "") {
            if (usedType is TypeSpecification) {
                // E.g. the reference type System.Int32&, which is used for out parameters.
                // or an arraytype?!?
                usedType = ((TypeSpecification) usedType).ElementType;
            }
            if (!(usedType is GenericInstanceType) && !(usedType is GenericParameter)) {
                // Currently, we do not look at generic type parameters; we would have to
                // untangle the usage of an actual (non-type-parameter) type's member
                // to get a useful Dependency_ for the user.

                RawUsedItem usedItem = GetFullnameItem(usedType, memberName, memberSort);
                yield return RawDependency.New(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, usedItem, sequencePoint);
            }

            var genericInstanceType = usedType as GenericInstanceType;
            if (genericInstanceType != null) {
                foreach (TypeReference genericArgument in genericInstanceType.GenericArguments) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, genericArgument, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                }
            }
        }

        ////public Func<TypeReference, string, ItemTail> GetCustomSectionComputation() {
        ////    // Simple version - does not work for cyclic assembly dependencies!
        ////    IEnumerable<Dependency> dependencies = ReadOrGetDependencies();
        ////    IEnumerable<Item> items = dependencies.Select(d => d.UsingItem).Where(it => it.Type == DotNetAssemblyDependencyReaderFactory.DOTNETCALL).Distinct();

        ////    Dictionary<Tuple<string, string, string>, ItemTail> convertDict = items.ToDictionary(it => Tuple.Create(it.Values[0], it.Values[1], it.Values[5]), it => new ItemTail(it.Type, it.Values.Skip(6).ToArray()));
        ////    return (tr, m) => convertDict.ContainsKey(Tuple.Create(tr.Namespace, tr.Name, m)) ? convertDict[Tuple.Create(tr.Namespace, tr.Name, m)] : null;
        ////}

        ////public Item CloneWithTail(Item item) {
        ////    if (_needsOnlyItemTails) {

        ////    } else {

        ////    }
        ////}
    }
}