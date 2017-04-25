using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NDepCheck.Reading {
    public class FullDotNetAssemblyDependencyReader : AbstractDotNetAssemblyDependencyReader {
        private class MarkerGenerator<T> {
            public Func<T, bool> IsTrue { get; }
            public string Marker { get; }

            internal MarkerGenerator(Func<T, bool> isTrue, string marker) {
                IsTrue = isTrue;
                Marker = marker;
            }
        }

        private IEnumerable<RawDependency> _rawDependencies;

        public FullDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory factory, string fileName, GlobalContext globalContext)
            : base(factory, fileName, globalContext) {
        }

        protected override IEnumerable<Dependency> ReadDependencies(InputContext inputContext, int depth, bool ignoreCase) {
            return GetOrReadRawDependencies(depth).Where(d => d.UsedItem != null).Select(d => d.ToDependencyWithTail(depth, inputContext));
        }

        private IEnumerable<RawDependency> GetOrReadRawDependencies(int depth) {
            // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
            if (_rawDependencies == null) {
                _rawDependencies = ReadRawDependencies(depth).ToArray();
            }
            return _rawDependencies;
        }

        protected override IEnumerable<RawUsingItem> ReadUsingItems(int depth) {
            return GetOrReadRawDependencies(depth).Select(d => d.UsingItem);
        }

        protected IEnumerable<RawDependency> ReadRawDependencies(int depth) {
            Log.WriteInfo(new string(' ', 2 * depth) + "Reading " + _fullFileName);
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(_fullFileName));
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(_fullFileName, new ReaderParameters {
                AssemblyResolver = resolver
            });

            try {
                assembly.MainModule.ReadSymbols();
            } catch (Exception ex) {
                Log.WriteWarning($"Loading symbols for assembly {_fullFileName} failed - maybe .PDB file is missing. ({ex.Message})", _fullFileName, 0);
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
            //        new RawDependency(DotNetAssemblyDependencyReaderFactory.DOTNETREF,
            //            new RawUsingItem(null, null, currentAssembly.Name, currentAssembly.Version.ToString(), currentAssembly.Culture, null, null),
            //            new RawUsedItem(null, null, reference.Name, reference.Version.ToString(), reference.Culture, null),
            //            null);
            //}
        }

        private static MarkerGenerator<T> CreateCheck<T>(Func<T, bool> check, string marker) {
            return new MarkerGenerator<T>(check, marker);
        }

        private static readonly MarkerGenerator<TypeDefinition>[] _typeDefinitionMarkers = {
            CreateCheck<TypeDefinition>(t => t.IsAbstract, "abstract"),
            CreateCheck<TypeDefinition>(t => t.IsClass, "class"),
            CreateCheck<TypeDefinition>(t => t.IsEnum, "enum"),
            CreateCheck<TypeDefinition>(t => t.IsInterface, "interface"),
            CreateCheck<TypeDefinition>(t => t.IsPrimitive, "primitive"),
            CreateCheck<TypeDefinition>(t => t.IsPublic, "public"),
            CreateCheck<TypeDefinition>(t => t.IsSealed, "sealed"),
            CreateCheck<TypeDefinition>(t => t.IsValueType, "valuetype"),
            CreateCheck<TypeDefinition>(t => t.IsNotPublic, "notpublic"),
            CreateCheck<TypeDefinition>(t => t.IsArray, "array"),
            // and 1000s more
        };

        private static readonly MarkerGenerator<MethodDefinition>[] _methodDefinitionMarkers = {
            CreateCheck<MethodDefinition>(t => t.IsAbstract, "abstract"),
            CreateCheck<MethodDefinition>(t => t.IsConstructor, "ctor"),
            CreateCheck<MethodDefinition>(t => t.IsFinal, "sealed"),
            CreateCheck<MethodDefinition>(t => t.IsDefinition, "definition"),
            CreateCheck<MethodDefinition>(t => t.IsPublic, "public"),
            CreateCheck<MethodDefinition>(t => t.IsVirtual, "virtual"),
            CreateCheck<MethodDefinition>(t => t.IsGetter, "get"),
            CreateCheck<MethodDefinition>(t => t.IsSetter, "set"),
            CreateCheck<MethodDefinition>(t => t.IsPrivate, "private"),
            CreateCheck<MethodDefinition>(t => t.IsStatic, "static"),
            // and 1000s more
        };

        private static readonly MarkerGenerator<PropertyDefinition>[] _propertyDefinitionMarkers = {
            //CreateCheck<PropertyDefinition>(t => t.IsAbstract, "abstract"),
            //CreateCheck<PropertyDefinition>(t => t.IsConstructor, "ctor"),
            //CreateCheck<PropertyDefinition>(t => t.IsFinal, "sealed"),
            CreateCheck<PropertyDefinition>(t => t.IsDefinition, "definition"),
            //CreateCheck<PropertyDefinition>(t => t.IsPublic, "public"),
            //CreateCheck<PropertyDefinition>(t => t.IsVirtual, "virtual"),
            //CreateCheck<PropertyDefinition>(t => t.IsGetter, "get"),
            //CreateCheck<PropertyDefinition>(t => t.IsSetter, "set"),
            //CreateCheck<PropertyDefinition>(t => t.IsPrivate, "private"),
            //CreateCheck<PropertyDefinition>(t => t.IsStatic, "static"),
            // and 1000s more
        };

        private static readonly MarkerGenerator<FieldDefinition>[] _fieldDefinitionMarkers = {
            CreateCheck<FieldDefinition>(t => t.IsDefinition, "definition"),
            CreateCheck<FieldDefinition>(t => t.IsPublic, "public"),
            CreateCheck<FieldDefinition>(t => t.IsPrivate, "private"),
            CreateCheck<FieldDefinition>(t => t.IsStatic, "static"),
            CreateCheck<FieldDefinition>(t => t.IsLiteral, "const"),
            CreateCheck<FieldDefinition>(t => t.IsInitOnly, "readonly"),
            CreateCheck<FieldDefinition>(t => t.IsNotSerialized, "notserialized"),
            // and 1000s more
        };

        private static readonly MarkerGenerator<EventDefinition>[] _eventDefinitionMarkers = {
            CreateCheck<EventDefinition>(t => t.IsDefinition, "definition"),
            // and 1000s more
        };

        private static readonly MarkerGenerator<ParameterDefinition>[] _parameterDefinitionMarkers = {
            CreateCheck<ParameterDefinition>(t => t.IsIn, "in"),
            CreateCheck<ParameterDefinition>(t => t.IsOptional, "optional"),
            CreateCheck<ParameterDefinition>(t => t.IsOut, "out"),
            CreateCheck<ParameterDefinition>(t => t.IsReturnValue, "return"),
            // and 1000s more
        };

        private static readonly MarkerGenerator<VariableDefinition>[] _variableDefinitionMarkers = {
            CreateCheck<VariableDefinition>(t => t.IsPinned, "pinned"),
        };

        private static string[] GetVariableMarkers(VariableDefinition t, MarkerGenerator<VariableDefinition>[] markerGenerators) {
            return GetMarkers(t, markerGenerators);
        }

        private static string[] GetParameterMarkers(ParameterDefinition t, MarkerGenerator<ParameterDefinition>[] markerGenerators) {
            return GetMarkers(t, markerGenerators);
        }

        private static string[] GetMemberMarkers<T>(T t, MarkerGenerator<T>[] markerGenerators) where T : IMemberDefinition {
            return GetMarkers(t, markerGenerators);
        }

        [CanBeNull]
        private static string[] GetMarkers<T>(T t, MarkerGenerator<T>[] markerGenerators) {
            if (t == null) {
                return null;
            } else {
                IEnumerable<MarkerGenerator<T>> matchingGenerators = markerGenerators.Where(c => c.IsTrue(t)).ToArray();
                return matchingGenerators.Any() ? matchingGenerators.Select(c => c.Marker).ToArray() : null;
            }
        }

        [NotNull]
        private IEnumerable<RawDependency> AnalyzeType([NotNull] TypeDefinition type, [CanBeNull] ItemTail parentCustomSections) {
            ItemTail typeCustomSections = GetCustomSections(type.CustomAttributes, parentCustomSections);
            {
                RawUsingItem usingItem = GetClassItem(type, typeCustomSections, GetMemberMarkers(type, _typeDefinitionMarkers));
                // TODO: WHY???yield return new RawDependency(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, null, null);

                foreach (var dependency_ in AnalyzeCustomAttributes(usingItem, type.CustomAttributes)) {
                    yield return dependency_;
                }

                if (type.BaseType != null && !IsLinked(type.BaseType, type.DeclaringType)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                        GetMemberMarkers(type, _typeDefinitionMarkers), type.BaseType, usage: Usage.inherits, sequencePoint: null)) {
                        yield return dependency_;
                    }
                }

                foreach (TypeReference interfaceRef in type.Interfaces) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                        GetMemberMarkers(Resolve(interfaceRef), _typeDefinitionMarkers),
                        interfaceRef, usage: Usage.implements, sequencePoint: null)) {
                        yield return dependency_;
                    }
                }

                foreach (FieldDefinition field in type.Fields) {
                    //if (IsLinked(field.FieldType, type.DeclaringType)) {
                    ItemTail fieldCustomSections = GetCustomSections(field.CustomAttributes, typeCustomSections);
                    // TODO: WHY???yield return new RawDependency(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, GetFullnameItem(type, field.Name, "", fieldCustomSections), null, null);

                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(field, _fieldDefinitionMarkers),
                             field.FieldType, usage: Usage.declaresfield, sequencePoint: null)) {
                        yield return dependency_;
                    }
                    //}
                }

                foreach (EventDefinition @event in type.Events) {
                    ItemTail eventCustomSections = GetCustomSections(@event.CustomAttributes, typeCustomSections);
                    // TODO: WHY???yield return new RawDependency(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, GetFullnameItem(type, @event.Name, "", eventCustomSections), null, null);

                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(@event, _eventDefinitionMarkers),
                             @event.EventType, usage: Usage.declaresevent, sequencePoint: null)) {
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

                RawUsingItem usingItem = GetFullNameItem(type, method.Name, GetMemberMarkers(method, _methodDefinitionMarkers), methodCustomSections);
                // TODO: WHY???yield return new RawDependency(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, null, null);

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
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetParameterMarkers(parameter, _parameterDefinitionMarkers),
                         parameter.ParameterType, usage: Usage.declaresparameter, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            if (property.PropertyType != null) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(property, _propertyDefinitionMarkers),
                         property.PropertyType, usage: Usage.declaresreturntype, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            foreach (var dependency in AnalyzeGetterSetter(owner, property, GET_MARKER, propertyCustomSections, property.GetMethod)) {
                yield return dependency;
            }

            foreach (var dependency in AnalyzeGetterSetter(owner, property, SET_MARKER, propertyCustomSections, property.SetMethod)) {
                yield return dependency;
            }
        }

        private IEnumerable<RawDependency> AnalyzeCustomAttributes([NotNull] RawUsingItem usingItem,
                                                                   [NotNull] IEnumerable<CustomAttribute> customAttributes) {
            foreach (CustomAttribute customAttribute in customAttributes) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, null, customAttribute.Constructor.DeclaringType,
                         usage: Usage.declares, sequencePoint: null)) {
                    yield return dependency_;
                }
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, null, customAttribute.Constructor.DeclaringType,
                         usage: Usage.uses, sequencePoint: null, memberName: customAttribute.Constructor.Name)) {
                    yield return dependency_;
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeGetterSetter([NotNull] TypeDefinition owner, [NotNull] PropertyDefinition property,
                                                               [NotNull] string[] markers, [CanBeNull] ItemTail propertyCustomSections,
                                                               [CanBeNull] MethodDefinition getterSetter) {
            if (getterSetter != null) {
                RawUsingItem usingItem = GetFullNameItem(property.DeclaringType, property.Name, markers, propertyCustomSections);
                // TODO: WHY???yield return new RawDependency(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, null, null);

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
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                         GetParameterMarkers(parameter, _parameterDefinitionMarkers), parameter.ParameterType,
                         usage: Usage.declaresparameter, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            if (method.ReturnType != null && !IsLinked(method.ReturnType, method.DeclaringType.DeclaringType)) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                         GetMemberMarkers(Resolve(method.ReturnType), _typeDefinitionMarkers),
                         method.ReturnType, usage: Usage.declaresreturntype, sequencePoint: null)) {
                    yield return dependency_;
                }
            }

            if (method.HasBody) {
                foreach (var variable in method.Body.Variables) {
                    if (!IsLinked(variable.VariableType, method.DeclaringType.DeclaringType)) {
                        foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                                 GetVariableMarkers(variable, _variableDefinitionMarkers), variable.VariableType, usage: Usage.declaresvar, sequencePoint: null)) {
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
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                                GetMemberMarkers(Resolve(methodReference.DeclaringType), _typeDefinitionMarkers),
                                methodReference.DeclaringType, usage: Usage.uses, sequencePoint: sequencePoint,
                                memberName: methodReference.Name)) {
                        yield return dependency_;
                    }
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                                GetMemberMarkers(Resolve(methodReference.ReturnType), _typeDefinitionMarkers),
                                methodReference.ReturnType, usage: Usage.uses, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                }
            }
            {
                var field = instruction.Operand as FieldDefinition;
                if (field != null && !IsLinked(field.DeclaringType, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, 
                                GetMemberMarkers(field, _fieldDefinitionMarkers), field.FieldType, usage: Usage.uses, 
                                sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(field, _fieldDefinitionMarkers), 
                                field.DeclaringType, usage: Usage.uses, sequencePoint: sequencePoint, memberName: field.Name)) {
                        yield return dependency_;
                    }
                }
            }
            {
                var property = instruction.Operand as PropertyDefinition;
                if (property != null && !IsLinked(property.DeclaringType, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(property, _propertyDefinitionMarkers), 
                                property.PropertyType, usage: Usage.uses, sequencePoint: sequencePoint)) {
                        yield return dependency_;
                    }
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(property, _propertyDefinitionMarkers), 
                                property.DeclaringType, usage: Usage.uses, sequencePoint: sequencePoint, memberName: property.Name)) {
                        yield return dependency_;
                    }
                }
            }
            {
                var typeref = instruction.Operand as TypeReference;
                if (typeref != null && !IsLinked(typeref, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                                GetMemberMarkers(Resolve(typeref), _typeDefinitionMarkers),
                                typeref, usage: Usage.uses, sequencePoint: sequencePoint)) {
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

        private RawUsingItem GetClassItem([NotNull] TypeReference typeReference, [CanBeNull] ItemTail customSections, string[] markers) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, "", markers, customSections);
        }

        private RawUsedItem GetFullNameItem([NotNull] TypeReference typeReference, [NotNull] string memberName, string[] markers) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsedItem.New(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers);
        }

        private RawUsingItem GetFullNameItem([NotNull] TypeReference typeReference, [NotNull] string memberName, string[] markers, [CanBeNull] ItemTail customSections) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, customSections);
        }

        /// <summary>
        /// Create a single dependency to the calledType or (if passed) calledType+method.
        /// Create additional dependencies for each generic parameter type of calledType.
        /// </summary>
        private IEnumerable<RawDependency> CreateTypeAndMethodDependencies([NotNull] RawUsingItem usingItem, string[] usedMarkers, [NotNull] TypeReference usedType,
            Usage usage, [CanBeNull] SequencePoint sequencePoint, [NotNull] string memberName = "") {
            if (usedType is TypeSpecification) {
                // E.g. the reference type System.Int32&, which is used for out parameters.
                // or an arraytype?!?
                usedType = ((TypeSpecification)usedType).ElementType;
            }
            if (!(usedType is GenericInstanceType) && !(usedType is GenericParameter)) {
                // Currently, we do not look at generic type parameters; we would have to
                // untangle the usage of an actual (non-type-parameter) type's member
                // to get a useful Dependency_ for the user.

                RawUsedItem usedItem = GetFullNameItem(usedType, memberName, usedMarkers);
                yield return new RawDependency(DotNetAssemblyDependencyReaderFactory.DOTNETCALL, usingItem, usedItem, usage, sequencePoint, _globalContext);
            }

            var genericInstanceType = usedType as GenericInstanceType;
            if (genericInstanceType != null) {
                foreach (TypeReference genericArgument in genericInstanceType.GenericArguments) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                             GetMemberMarkers(Resolve(genericArgument), _typeDefinitionMarkers), genericArgument,
                             usage: Usage.usesasgenericargument, sequencePoint: sequencePoint)) {
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