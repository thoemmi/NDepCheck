using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NDepCheck.Reading.AssemblyReading {
    public class FullDotNetAssemblyDependencyReader : AbstractDotNetAssemblyDependencyReader {
        private class MarkerGenerator<T> {
            public Func<T, bool> IsTrue { get; }
            public string Marker { get; }

            internal MarkerGenerator(Func<T, bool> isTrue, string marker) {
                IsTrue = isTrue;
                Marker = marker;
            }
        }

        private static readonly MarkerGenerator<MethodDefinition>[] _methodDefinitionMarkers = {
            CreateCheck<MethodDefinition>(t => t.IsAbstract, "_abstract"),
            CreateCheck<MethodDefinition>(t => t.IsConstructor, "_ctor"),
            CreateCheck<MethodDefinition>(t => t.IsFinal, "_sealed"),
            CreateCheck<MethodDefinition>(t => t.IsDefinition, "_definition"),
            CreateCheck<MethodDefinition>(t => t.IsPublic, "_public"),
            CreateCheck<MethodDefinition>(t => t.IsVirtual, "_virtual"),
            CreateCheck<MethodDefinition>(t => t.IsGetter, "_get"),
            CreateCheck<MethodDefinition>(t => t.IsSetter, "_set"),
            CreateCheck<MethodDefinition>(t => t.IsPrivate, "_private"),
            CreateCheck<MethodDefinition>(t => t.IsStatic, "_static"),
            // and maybe more, from other Mono.Cecil information
        };

        private readonly MarkerGenerator<PropertyDefinition>[] _propertyDefinitionMarkers;

        private readonly MarkerGenerator<FieldDefinition>[] _fieldDefinitionMarkers;

        private static readonly MarkerGenerator<EventDefinition>[] _eventDefinitionMarkers = {
            CreateCheck<EventDefinition>(t => t.IsDefinition, _definition),
            // and maybe more, from other Mono.Cecil information
        };

        private static readonly MarkerGenerator<ParameterDefinition>[] _parameterDefinitionMarkers = {
            CreateCheck<ParameterDefinition>(t => !(t.ParameterType is ByReferenceType) && !t.IsOut || t.IsIn, _in),
            CreateCheck<ParameterDefinition>(t => t.IsOptional, _optional),
            CreateCheck<ParameterDefinition>(t => t.IsOut, _out),
            CreateCheck<ParameterDefinition>(t => t.ParameterType is ByReferenceType, _ref),
            CreateCheck<ParameterDefinition>(t => t.IsReturnValue, _return),
            // and maybe more, from other Mono.Cecil information
        };

        private static readonly MarkerGenerator<VariableDefinition>[] _variableDefinitionMarkers = {
            CreateCheck<VariableDefinition>(t => t.IsPinned, _pinned),
            // and maybe more, from other Mono.Cecil information
        };

        private IDependencyReader[] _readerGang;

        private IEnumerable<RawDependency> _rawDependencies;

        public FullDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory readerFactory, string fileName)
            : base(readerFactory, fileName) {
            _propertyDefinitionMarkers = new[] {
                    CreateCheck<PropertyDefinition>(t => t.IsDefinition, _definition),
                    // and maybe more, from other Mono.Cecil information
                }.Concat(ForwardToTypeDefinition((PropertyDefinition p) => Resolve(p.PropertyType))).ToArray();

            _fieldDefinitionMarkers = new[] {
                    CreateCheck<FieldDefinition>(t => t.IsDefinition, _definition),
                    CreateCheck<FieldDefinition>(t => t.IsPublic, _public),
                    CreateCheck<FieldDefinition>(t => t.IsPrivate, _private),
                    CreateCheck<FieldDefinition>(t => t.IsStatic, _static),
                    CreateCheck<FieldDefinition>(t => t.IsLiteral, _const),
                    CreateCheck<FieldDefinition>(t => t.IsInitOnly, _readonly),
                    CreateCheck<FieldDefinition>(t => t.IsNotSerialized, _notserialized),
                    // and maybe more, from other Mono.Cecil information
                }.Concat(ForwardToTypeDefinition((FieldDefinition p) => Resolve(p.FieldType))).ToArray();
        }

        public override void SetReadersInSameReadFilesBeforeReadDependencies(IDependencyReader[] readerGang) {
            _readerGang = readerGang;
        }

        public override IEnumerable<Dependency> ReadDependencies(WorkingGraph readingGraph, int depth, bool ignoreCase) {
            return GetOrReadRawDependencies(depth, readingGraph).Where(d => d.UsedItem != null).Select(d => d.ToDependencyWithTail(readingGraph, depth, ContainerUri));
        }

        private IEnumerable<RawDependency> GetOrReadRawDependencies(int depth, WorkingGraph readingGraph) {
            // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
            if (_rawDependencies == null) {
                _rawDependencies = ReadRawDependencies(depth, readingGraph).ToArray();
            }
            return _rawDependencies;
        }

        protected override IEnumerable<RawUsingItem> ReadUsingItems(int depth, WorkingGraph readingGraph) {
            return GetOrReadRawDependencies(depth, readingGraph).Select(d => d.UsingItem);
        }

        protected IEnumerable<RawDependency> ReadRawDependencies(int depth, WorkingGraph readingGraph) {
            Log.WriteInfo(new string(' ', 2 * depth) + "Reading " + FullFileName);
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(FullFileName));
            // readingGraph: Additional search directories should be specifiable in options
            resolver.AddSearchDirectory(@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\Silverlight\v5.0");
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(FullFileName, new ReaderParameters {
                AssemblyResolver = resolver
            });

            try {
                assembly.MainModule.ReadSymbols();
            } catch (Exception ex) {
                Log.WriteWarning($"Loading symbols for assembly {FullFileName} failed - maybe .PDB file is missing. ({ex.Message})", FullFileName, 0);
            }

            ItemTail customSections = GetCustomSections(readingGraph, assembly.CustomAttributes, null);

            foreach (TypeDefinition type in assembly.MainModule.Types) {
                if (type.Name == "<Module>") {
                    continue;
                }

                foreach (var dependency in AnalyzeType(type, customSections, readingGraph)) {
                    yield return dependency;
                }
            }
        }

        private static MarkerGenerator<T> CreateCheck<T>(Func<T, bool> check, string marker) {
            return new MarkerGenerator<T>(check, marker);
        }

        private static readonly MarkerGenerator<TypeDefinition>[] _typeDefinitionMarkers = {
            CreateCheck<TypeDefinition>(t => t.IsAbstract, _abstract),
            CreateCheck<TypeDefinition>(t => t.IsArray, "_array"),
            CreateCheck<TypeDefinition>(t => t.IsClass, "_class"),
            CreateCheck<TypeDefinition>(t => t.IsEnum, "_enum"),
            CreateCheck<TypeDefinition>(t => t.IsInterface, "_interface"),
            CreateCheck<TypeDefinition>(t => t.IsPrimitive, "_primitive"),
            CreateCheck<TypeDefinition>(t => t.IsPublic || t.IsNestedPublic, "_public"),
            CreateCheck<TypeDefinition>(t => t.IsNestedPrivate, "_nestedprivate"),
            CreateCheck<TypeDefinition>(t => t.IsSealed, "_sealed"),
            CreateCheck<TypeDefinition>(t => t.IsValueType, "_struct"),
            CreateCheck<TypeDefinition>(t => t.IsNotPublic, "_notpublic"),
            CreateCheck<TypeDefinition>(t => t.IsArray, "_array"),
            CreateCheck<TypeDefinition>(t => t.IsNested, "_nested"),
            // and 1000s more
        };

        private static IEnumerable<MarkerGenerator<T>> ForwardToTypeDefinition<T>(Func<T, TypeDefinition> markerContributingType) where T : IMemberDefinition {
            return _typeDefinitionMarkers.Select(tdm => CreateCheck<T>(f => markerContributingType(f) != null && tdm.IsTrue(markerContributingType(f)), tdm.Marker));
        }

        private static string[] GetVariableMarkers(VariableDefinition t, MarkerGenerator<VariableDefinition>[] markerGenerators) {
            return GetMarkers(t, markerGenerators);
        }

        [CanBeNull]
        private static string[] GetParameterMarkers(ParameterDefinition t, MarkerGenerator<ParameterDefinition>[] markerGenerators) {
            return GetMarkers(t, markerGenerators);
        }

        [CanBeNull]
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
        private IEnumerable<RawDependency> AnalyzeType([NotNull] TypeDefinition type, [CanBeNull] ItemTail parentCustomSections, 
                                                       WorkingGraph readingGraph) {
            ItemTail typeCustomSections = GetCustomSections(readingGraph, type.CustomAttributes, parentCustomSections);
            {
                RawUsingItem usingItem = CreateUsingItem(DOTNETTYPE,
                    type, typeCustomSections, GetMemberMarkers(type, _typeDefinitionMarkers), readingGraph);
                // readingGraph: WHY???yield return new RawDependency(DOTNETITEM, usingItem, null, null);

                foreach (var dependency_ in AnalyzeCustomAttributes(usingItem, type.CustomAttributes, readingGraph)) {
                    yield return dependency_;
                }

                if (type.BaseType != null && !IsLinked(type.BaseType, type.DeclaringType)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETTYPE, 
                        GetMemberMarkers(type, _typeDefinitionMarkers), type.BaseType, 
                        usage: DotNetUsage._directlyderivedfrom, sequencePoint: null, memberName: "", readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                }

                foreach (TypeReference interfaceRef in type.Interfaces) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                        DOTNETTYPE, GetMemberMarkers(Resolve(interfaceRef), _typeDefinitionMarkers), interfaceRef, 
                        usage: DotNetUsage._directlyimplements, sequencePoint: null, memberName: "", readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                }

                foreach (FieldDefinition field in type.Fields) {
                    //if (IsLinked(field.FieldType, type.DeclaringType)) {
                    ItemTail fieldCustomSections = GetCustomSections(readingGraph, field.CustomAttributes, typeCustomSections);
                    // readingGraph: WHY??? yield return new RawDependency(DOTNETITEM, GetFullnameItem(type, field.Name, "", fieldCustomSections), null, null);

                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETFIELD,
                        GetMemberMarkers(field, _fieldDefinitionMarkers), field.FieldType, usage: DotNetUsage._declaresfield,
                        sequencePoint: null, memberName: field.Name, readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                    //}
                }

                foreach (EventDefinition @event in type.Events) {
                    ItemTail eventCustomSections = GetCustomSections(readingGraph, @event.CustomAttributes, typeCustomSections);
                    // readingGraph: WHY??? yield return new RawDependency(DOTNETITEM, GetFullnameItem(type, @event.Name, "", eventCustomSections), null, null);

                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETEVENT,
                        GetMemberMarkers(@event, _eventDefinitionMarkers), @event.EventType, 
                        usage: DotNetUsage._declaresevent, sequencePoint: null, memberName: @event.Name, readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                }

                foreach (var property in type.Properties) {
                    //if (!IsLinked(property.PropertyType, type.DeclaringType)) {
                    foreach (var dependency_ in AnalyzeProperty(type, usingItem, property, typeCustomSections, readingGraph)) {
                        yield return dependency_;
                    }
                    //}
                }
            }

            foreach (MethodDefinition method in type.Methods) {
                ItemTail methodCustomSections = GetCustomSections(readingGraph, method.CustomAttributes, typeCustomSections);

                RawUsingItem usingItem = CreateUsingItem(DOTNETMETHOD,
                    type, method.Name, GetMemberMarkers(method, _methodDefinitionMarkers), methodCustomSections, readingGraph);
                // readingGraph: WHY???yield return new RawDependency(DOTNETITEM, usingItem, null, null);

                foreach (var dependency_ in AnalyzeMethod(type, usingItem, method, readingGraph)) {
                    yield return dependency_;
                }
            }

            foreach (TypeDefinition nestedType in type.NestedTypes) {
                foreach (var dependency_ in AnalyzeType(nestedType, typeCustomSections, readingGraph)) {
                    yield return dependency_;
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeProperty([NotNull] TypeDefinition owner, [NotNull] RawUsingItem usingItem, [NotNull] PropertyDefinition property, [CanBeNull] ItemTail typeCustomSections, WorkingGraph readingGraph) {
            ItemTail propertyCustomSections = GetCustomSections(readingGraph, property.CustomAttributes, typeCustomSections);

            foreach (var dependency_ in AnalyzeCustomAttributes(usingItem, property.CustomAttributes, readingGraph)) {
                yield return dependency_;
            }

            foreach (ParameterDefinition parameter in property.Parameters) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETPARAMETER,
                    GetParameterMarkers(parameter, _parameterDefinitionMarkers), parameter.ParameterType, 
                    usage: DotNetUsage._declaresparameter, sequencePoint: null, memberName: parameter.Name, readingGraph: readingGraph)) {
                    yield return dependency_;
                }
            }

            if (property.PropertyType != null) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETTYPE, 
                    GetMemberMarkers(property, _propertyDefinitionMarkers), property.PropertyType, 
                    usage: DotNetUsage._declaresreturntype, sequencePoint: null, memberName: property.Name, 
                    readingGraph: readingGraph)) {
                    yield return dependency_;
                }
            }

            foreach (var dependency in AnalyzeGetterSetter(owner, property, GET_MARKER, propertyCustomSections, property.GetMethod, readingGraph)) {
                yield return dependency;
            }

            foreach (var dependency in AnalyzeGetterSetter(owner, property, SET_MARKER, propertyCustomSections, property.SetMethod, readingGraph)) {
                yield return dependency;
            }
        }

        private IEnumerable<RawDependency> AnalyzeCustomAttributes([NotNull] RawUsingItem usingItem,
                                                                   [NotNull] IEnumerable<CustomAttribute> customAttributes, WorkingGraph readingGraph) {
            foreach (CustomAttribute customAttribute in customAttributes) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, 
                    DOTNETITEM, null, customAttribute.Constructor.DeclaringType, usage: DotNetUsage._usesmember, 
                    sequencePoint: null, memberName: customAttribute.Constructor.Name, readingGraph: readingGraph)) {
                    yield return dependency_;
                }
                // See comment at Usage._usesmemberoftype on why this is commented out
                //foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, null, customAttribute.Constructor.DeclaringType,
                //         usage: Usage._usesmemberoftype, sequencePoint: null, memberName: "")) {
                //    yield return dependency_;
                //}
            }
        }

        private IEnumerable<RawDependency> AnalyzeGetterSetter([NotNull] TypeDefinition owner, [NotNull] PropertyDefinition property,
                                                               [NotNull] string[] markers, [CanBeNull] ItemTail propertyCustomSections,
                                                               [CanBeNull] MethodDefinition getterSetter, WorkingGraph readingGraph) {
            if (getterSetter != null) {
                RawUsingItem usingItem = CreateUsingItem(DOTNETPROPERTY, property.DeclaringType,
                    property.Name, markers, propertyCustomSections, readingGraph);
                // readingGraph: WHY???yield return new RawDependency(DOTNETITEM, usingItem, null, null);

                foreach (var dependency in AnalyzeMethod(owner, usingItem, getterSetter, readingGraph)) {
                    yield return dependency;
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeMethod([NotNull] TypeDefinition owner, [NotNull] RawUsingItem usingItem,
                                                         [NotNull] MethodDefinition method, WorkingGraph readingGraph) {
            foreach (var dependency_ in AnalyzeCustomAttributes(usingItem, method.CustomAttributes, readingGraph)) {
                yield return dependency_;
            }

            foreach (ParameterDefinition parameter in method.Parameters) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETPARAMETER,
                         GetParameterMarkers(parameter, _parameterDefinitionMarkers), parameter.ParameterType,
                         usage: DotNetUsage._declaresparameter, sequencePoint: null, memberName: parameter.Name, readingGraph: readingGraph)) {
                    yield return dependency_;
                }
            }

            if (method.ReturnType != null && !IsLinked(method.ReturnType, method.DeclaringType.DeclaringType)) {
                foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETTYPE,
                         GetMemberMarkers(Resolve(method.ReturnType), _typeDefinitionMarkers),
                         method.ReturnType, usage: DotNetUsage._declaresreturntype, sequencePoint: null, memberName: "", readingGraph: readingGraph)) {
                    yield return dependency_;
                }
            }

            if (method.HasBody) {
                foreach (var variable in method.Body.Variables) {
                    if (!IsLinked(variable.VariableType, method.DeclaringType.DeclaringType)) {
                        foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETVARIABLE,
                            GetVariableMarkers(variable, _variableDefinitionMarkers), variable.VariableType, 
                            usage: DotNetUsage._declaresvariable, sequencePoint: null, memberName: variable.Name,
                            readingGraph: readingGraph)) {
                            yield return dependency_;
                        }
                    }
                }

                SequencePoint mostRecentSeqPoint = null;
                foreach (Instruction instruction in method.Body.Instructions) {
                    if (instruction.SequencePoint != null) {
                        mostRecentSeqPoint = instruction.SequencePoint;
                    }
                    foreach (var dependency_ in AnalyzeInstruction(owner, usingItem, instruction, mostRecentSeqPoint, readingGraph)) {
                        yield return dependency_;
                    }
                }
            }
        }

        private IEnumerable<RawDependency> AnalyzeInstruction([NotNull] TypeDefinition owner, [NotNull] RawUsingItem usingItem,
                                                              [NotNull] Instruction instruction, [CanBeNull] SequencePoint sequencePoint, 
                                                              [NotNull] WorkingGraph readingGraph) {
            {
                var methodReference = instruction.Operand as MethodReference;
                // Durch die !IsLinked-Bedingung wird der Test MainTests.Exit0Aspects mit den Calls
                // zwischen Nested-Klassen in NDepCheck.TestAssembly.cs - Klasse Class14.Class13EInner2, Methode SpecialMethodOfInnerClass
                // rot: Weil die Calls zwischen der Class14 und der Nested Class wegen IsLinked==true hier einfach ausgefiltert
                // werden ...
                // WIESO SOLL DAS SO SEIN? - bitte um Erklärung! ==> SIehe nun temporäre Änderung an IsLinked - IST DAS OK?
                if (methodReference != null && !IsLinked(methodReference.DeclaringType, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETMETHOD,
                                GetMemberMarkers(Resolve(methodReference.DeclaringType), _typeDefinitionMarkers),
                                methodReference.DeclaringType, usage: DotNetUsage._usesmember, sequencePoint: sequencePoint,
                                memberName: methodReference.Name, readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                    // See comment at Usage._usesmemberoftype on why this is commented out
                    //foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem,
                    //            GetMemberMarkers(Resolve(methodReference.ReturnType), _typeDefinitionMarkers),
                    //            methodReference.ReturnType, usage: Usage._usesmemberoftype, sequencePoint: sequencePoint, memberName: "")) {
                    //    yield return dependency_;
                    //}
                }
            }
            {
                var field = instruction.Operand as FieldDefinition;
                if (field != null && !IsLinked(field.DeclaringType, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETFIELD,
                                GetMemberMarkers(field, _fieldDefinitionMarkers), field.FieldType, usage: DotNetUsage._usesmember,
                                sequencePoint: sequencePoint, memberName: field.Name, readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                    // See comment at Usage._usesmemberoftype on why this is commented out
                    //foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(field, _fieldDefinitionMarkers), 
                    //            field.DeclaringType, usage: Usage._usesmemberoftype, sequencePoint: sequencePoint, memberName: "")) {
                    //    yield return dependency_;
                    //}
                }
            }
            {
                var property = instruction.Operand as PropertyDefinition;
                if (property != null && !IsLinked(property.DeclaringType, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETPROPERTY, 
                        GetMemberMarkers(property, _propertyDefinitionMarkers), property.PropertyType, 
                        usage: DotNetUsage._usesmember, sequencePoint: sequencePoint, memberName: property.Name, readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                    // See comment at Usage._usesmemberoftype on why this is commented out
                    //foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, GetMemberMarkers(property, _propertyDefinitionMarkers), 
                    //            property.DeclaringType, usage: Usage._usesmemberoftype, sequencePoint: sequencePoint, memberName: "")) {
                    //    yield return dependency_;
                    //}
                }
            }
            {
                var typeref = instruction.Operand as TypeReference;
                if (typeref != null && !IsLinked(typeref, owner)) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETTYPE,
                                GetMemberMarkers(Resolve(typeref), _typeDefinitionMarkers), typeref, 
                                usage: DotNetUsage._usestype, sequencePoint: sequencePoint, memberName: "", readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                }
            }
        }

        private static bool IsLinked([CanBeNull] TypeReference referringType, [CanBeNull] TypeReference referrer) {
            // I no longer see the reason why not-linked items should be excluded - we know everything we know, so we use it!

            ////if (referrer == null || referringType == null) {
            ////    return false;
            ////}
            ////if (referringType == referrer) {
            ////    return true;
            ////}

            //// Ich habe hier die Suche über DeclaringType einmal rausgenommen; Tests funktionieren genausogut,
            //// und auch ein Durchlauf über das Framework schaut m.E. korrekt aus!
            //// Eigentlich glaube ich auch, dass das return true oben nicht ok ist: Auch wenn beide Typen gleich
            //// sind, muss eine Dependency_ entstehen und geprüft werden - wieso soll es hier eine Ausnahme geben???
            return false;
            ////return IsLinked(referringType, referrer.DeclaringType) || IsLinked(referringType.DeclaringType, referrer);
        }

        private RawUsingItem CreateUsingItem([NotNull] ItemType itemType, [NotNull] TypeReference typeReference, [CanBeNull] ItemTail customSections, [CanBeNull, ItemNotNull] string[] markers, WorkingGraph readingGraph) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(_rawUsingItemsCache, itemType, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, "", markers, customSections, readingGraph);
        }

        private RawUsedItem CreateUsedItem([NotNull] ItemType itemType, [NotNull] TypeReference typeReference, [NotNull] string memberName, [CanBeNull, ItemNotNull] string[] markers, WorkingGraph readingGraph) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsedItem.New(itemType, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, readingGraph);
        }

        private RawUsingItem CreateUsingItem([NotNull] ItemType itemType, [NotNull] TypeReference typeReference, [NotNull] string memberName, [CanBeNull, ItemNotNull] string[] markers, [CanBeNull] ItemTail customSections, WorkingGraph readingGraph) {
            string namespaceName, className, assemblyName, assemblyVersion, assemblyCulture;
            GetTypeInfo(typeReference, out namespaceName, out className, out assemblyName, out assemblyVersion, out assemblyCulture);
            return RawUsingItem.New(_rawUsingItemsCache, itemType, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, customSections, readingGraph);
        }

        /// <summary>
        /// Create a single dependency to the calledType or (if passed) calledType+method.
        /// Create additional dependencies for each generic parameter type of calledType.
        /// </summary>
        private IEnumerable<RawDependency> CreateTypeAndMethodDependencies([NotNull] RawUsingItem usingItem, 
            [NotNull] ItemType usedItemType, [CanBeNull, ItemNotNull] string[] usedMarkers, [NotNull] TypeReference usedType, 
            DotNetUsage usage, [CanBeNull] SequencePoint sequencePoint, [NotNull] string memberName, WorkingGraph readingGraph) {
            if (usedType is TypeSpecification) {
                // E.g. the reference type System.int&, which is used for out parameters.
                // or an arraytype?!?
                usedType = ((TypeSpecification)usedType).ElementType;
            }
            if (!(usedType is GenericInstanceType) && !(usedType is GenericParameter)) {
                // Currently, we do not look at generic type parameters; we would have to
                // untangle the usage of an actual (non-type-parameter) type's member
                // to get a useful Dependency_ for the user.

                RawUsedItem usedItem = CreateUsedItem(usedItemType, usedType, memberName, usedMarkers, readingGraph);

                yield return new RawDependency(usingItem, usedItem, usage, sequencePoint,
                    _readerGang.OfType<AbstractDotNetAssemblyDependencyReader>().FirstOrDefault(r => r.AssemblyName == usedItem.AssemblyName));
            }

            var genericInstanceType = usedType as GenericInstanceType;
            if (genericInstanceType != null) {
                foreach (TypeReference genericArgument in genericInstanceType.GenericArguments) {
                    foreach (var dependency_ in CreateTypeAndMethodDependencies(usingItem, DOTNETTYPE,
                             GetMemberMarkers(Resolve(genericArgument), _typeDefinitionMarkers), genericArgument,
                             usage: DotNetUsage._usesasgenericargument, sequencePoint: sequencePoint, memberName: genericArgument.Name,
                             readingGraph: readingGraph)) {
                        yield return dependency_;
                    }
                }
            }
        }

        ////public Func<TypeReference, string, ItemTail> GetCustomSectionComputation() {
        ////    // Simple version - does not work for cyclic assembly dependencies!
        ////    [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies = ReadOrGetDependencies();
        ////    IEnumerable<Item> items = dependencies.Select(d => d.UsingItem).Where(it => it.Type == DOTNETITEM).Distinct();

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