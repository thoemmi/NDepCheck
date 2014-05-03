using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;

namespace NDepCheck {
    public static class DependencyReader {
        public const string ASSEMBLY_PREFIX = "assembly:";

        internal static void Init() {
#pragma warning disable 168
            // the only purpose of this instruction is to create a reference to Mono.Cecil.Pdb.
            // Otherwise Visual Studio won't copy that assembly to the output path.
            var readerProvider = new PdbReaderProvider();
#pragma warning restore 168
        }

        public static IEnumerable<Dependency> GetDependencies<T>() {
            return GetDependencies(typeof(T));
        }

        public static IEnumerable<Dependency> GetDependencies(Type t) {
            return GetDependencies(t.Assembly.Location, td => td.Name == t.Name && td.Namespace == t.Namespace);
        }

        public static IEnumerable<Dependency> GetDependencies(string filename) {
            return GetDependencies(filename, null);
        }

        public static IEnumerable<Dependency> GetDependencies(string filename, Predicate<TypeDefinition> typeFilter) {
            var sw = new Stopwatch();
            sw.Start();
            Log.WriteInfo("Reading " + filename);

            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(filename);
            try {
                assembly.MainModule.ReadSymbols();
            } catch (Exception ex) {
                Log.WriteWarning(String.Format("Loading symbols for assembly {0} failed - maybe .PDB file is missing. ({1})", filename, ex.Message), filename, 0);
            }

            foreach (TypeDefinition type in assembly.MainModule.Types) {
                if (type.Name == "<Module>") {
                    continue;
                }

                if (typeFilter != null && !typeFilter(type)) {
                    continue;
                }

                foreach (Dependency dependency in AnalyzeType(type)) {
                    yield return dependency;
                }
            }

            foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences) {
                // Repräsentationen der Assembly-Abhängigkeiten erzeugen
                yield return new Dependency(
                    new FullNameToken("", ASSEMBLY_PREFIX + assembly.Name.Name, null, null),
                    new FullNameToken("", ASSEMBLY_PREFIX + reference.Name, null, null),
                    filename, 0, 0, 0, 0);
            }

            Log.WriteInfo(String.Format("Analyzing {0} took {1} ms", filename, (int)sw.Elapsed.TotalMilliseconds));
            sw.Stop();
        }

        private static IEnumerable<Dependency> AnalyzeType(TypeDefinition type) {
            FullNameToken callingToken = GetFullnameToken(type, null);

            if (type.BaseType != null && !IsLinked(type.BaseType, type.DeclaringType)) {
                foreach (Dependency dependency in GetDependencies(callingToken, type.BaseType, null, null)) {
                    yield return dependency;
                }
            }

            foreach (TypeReference interfaceRef in type.Interfaces) {
                foreach (Dependency dependency in GetDependencies(callingToken, interfaceRef, null, null)) {
                    yield return dependency;
                }
            }

            foreach (FieldDefinition field in type.Fields) {
                if (IsLinked(field.FieldType, type.DeclaringType)) {
                    foreach (Dependency dependency in GetDependencies(callingToken, field.FieldType, null, null)) {
                        yield return dependency;
                    }
                }
            }

            foreach (EventDefinition @event in type.Events) {
                foreach (Dependency dependency in GetDependencies(callingToken, @event.EventType, null, null)) {
                    yield return dependency;
                }
            }

            foreach (PropertyDefinition property in type.Properties) {
                if (!IsLinked(property.PropertyType, type.DeclaringType)) {
                    foreach (Dependency dependency in GetDependencies(callingToken, property.PropertyType, null, null)) {
                        yield return dependency;
                    }
                }
            }

            foreach (CustomAttribute customAttribute in type.CustomAttributes) {
                foreach (
                    Dependency dependency in
                        GetDependencies(callingToken, customAttribute.Constructor.DeclaringType, null, null)) {
                    yield return dependency;
                }
            }

            foreach (MethodDefinition method in type.Methods) {
                callingToken = GetFullnameToken(type, method.Name);
                foreach (Dependency dependency in AnalyzeMethod(type, callingToken, method)) {
                    yield return dependency;
                }
            }

            foreach (TypeDefinition nestedType in type.NestedTypes) {
                foreach (Dependency dependency in AnalyzeType(nestedType)) {
                    yield return dependency;
                }
            }
        }

        private static IEnumerable<Dependency> AnalyzeMethod(TypeDefinition owner, FullNameToken callingToken,
                                                             MethodDefinition method) {
            foreach (CustomAttribute customAttribute in method.CustomAttributes) {
                foreach (
                    Dependency dependency in
                        GetDependencies(callingToken, customAttribute.Constructor.DeclaringType, null, null)) {
                    yield return dependency;
                }
                foreach (
                    Dependency dependency in
                        GetDependencies(callingToken, customAttribute.Constructor.DeclaringType,
                                        customAttribute.Constructor.Name, null)) {
                    yield return dependency;
                }
            }

            foreach (ParameterDefinition parameter in method.Parameters) {
                foreach (Dependency dependency in GetDependencies(callingToken, parameter.ParameterType, null, null)) {
                    yield return dependency;
                }
            }

            if (method.ReturnType != null && !IsLinked(method.ReturnType, method.DeclaringType.DeclaringType)) {
                foreach (
                    Dependency dependency in GetDependencies(callingToken, method.ReturnType, null, null)) {
                    yield return dependency;
                }
            }

            if (method.HasBody) {
                foreach (VariableDefinition variable in method.Body.Variables) {
                    if (!IsLinked(variable.VariableType, method.DeclaringType.DeclaringType)) {
                        foreach (
                            Dependency dependency in GetDependencies(callingToken, variable.VariableType, null, null)) {
                            yield return dependency;
                        }
                    }
                }

                SequencePoint mostRecentSeqPoint = null;
                foreach (Instruction instruction in method.Body.Instructions) {
                    if (instruction.SequencePoint != null) {
                        mostRecentSeqPoint = instruction.SequencePoint;
                    }
                    foreach (
                        Dependency dependency in
                            AnalyzeInstruction(owner, callingToken, instruction, mostRecentSeqPoint)) {
                        yield return dependency;
                    }
                }
            }
        }

        private static IEnumerable<Dependency> AnalyzeInstruction(TypeDefinition owner, FullNameToken callingToken,
                                                                  Instruction instruction, SequencePoint sequencePoint) {
            var methodReference = instruction.Operand as MethodReference;

            // Durch die !IsLinked-Bedingung wird der Test MainTests.Exit0Aspects mit den Calls
            // zwischen Nested-Klassen in NDepCheck.TestAssembly.cs - Klasse Class14.Class13EInner2, Methode SpecialMethodOfInnerClass
            // rot: Weil die Calls zwischen der Class14 und der Nested Class wegen IsLinked==true hier einfach ausgefiltert
            // werden ...
            // WIESO SOLL DAS SO SEIN? - bitte um Erklärung! ==> SIehe nun temporäre Änderung an IsLinked - IST DAS OK?
            if (methodReference != null && !IsLinked(methodReference.DeclaringType, owner)) {
                foreach (
                    Dependency dependency in
                        GetDependencies(callingToken, methodReference.DeclaringType, methodReference.Name, sequencePoint)
                    ) {
                    yield return dependency;
                }
                foreach (
                    Dependency dependency in
                        GetDependencies(callingToken, methodReference.ReturnType, null, sequencePoint)) {
                    yield return dependency;
                }
            }

            var field = instruction.Operand as FieldDefinition;
            if (field != null && !IsLinked(field.DeclaringType, owner)) {
                foreach (Dependency dependency in GetDependencies(callingToken, field.FieldType, null, sequencePoint)) {
                    yield return dependency;
                }
                foreach (
                    Dependency dependency in
                        GetDependencies(callingToken, field.DeclaringType, field.Name, sequencePoint)) {
                    yield return dependency;
                }
            }

            var property = instruction.Operand as PropertyDefinition;
            if (property != null && !IsLinked(property.DeclaringType, owner)) {
                foreach (
                    Dependency dependency in GetDependencies(callingToken, property.PropertyType, null, sequencePoint)) {
                    yield return dependency;
                }
                foreach (
                    Dependency dependency in
                        GetDependencies(callingToken, property.DeclaringType, property.Name, sequencePoint)) {
                    yield return dependency;
                }
            }
        }

        private static bool IsLinked(TypeReference referringType, TypeReference referrer) {
            if (referrer == null || referringType == null) {
                return false;
            }
            if (referringType == referrer) {
                return true;
            }

            // Ich habe hier die Suche über DeclaringType einmal rausgenommen; Tests funktionieren genausogut,
            // und auch ein Durchlauf über das Framework schaut m.E. korrekt aus!
            // Eigentlich glaube ich auch, dass das return true oben nicht ok ist: Auch wenn beide Typen gleich
            // sind, muss eine Dependency entstehen und geprüft werden - wieso soll es hier eine Ausnahme geben???
            return false;
            //return IsLinked(referringType, referrer.DeclaringType) || IsLinked(referringType.DeclaringType, referrer);
        }

        private static TypeInfo GetTypeInfo(TypeReference reference) {
            var ti = new TypeInfo();
            if (reference.DeclaringType != null) {
                TypeInfo parent = GetTypeInfo(reference.DeclaringType);
                ti.NamespaceName = parent.NamespaceName;
                ti.ClassName = parent.ClassName;
                ti.NestedClassName = parent.NestedClassName + "/" + CleanClassName(reference.Name);
            } else {
                ti.NamespaceName = reference.Namespace;
                ti.ClassName = CleanClassName(reference.Name);
                ti.NestedClassName = null;
            }

            return ti;
        }

        private static FullNameToken GetFullnameToken(TypeReference typeReference, string methodNameOrNull) {
            TypeInfo ti = GetTypeInfo(typeReference);
            string namespaceName = ti.NamespaceName;
            string className = ti.ClassName;
            string nestedName = ti.NestedClassName;

            if (!String.IsNullOrEmpty(namespaceName)) {
                namespaceName = namespaceName + ".";
            }

            if (!String.IsNullOrEmpty(methodNameOrNull)) {
                methodNameOrNull = "::" + methodNameOrNull;
            }

            return new FullNameToken(namespaceName, className, nestedName, methodNameOrNull);
        }

        private static string CleanClassName(string className) {
            if (!String.IsNullOrEmpty(className)) {
                while (className.EndsWith("[]")) {
                    className = className.Substring(0, className.Length - 2);
                }
                int pos = className.LastIndexOf('`');
                if (pos > 0) {
                    className = className.Substring(0, pos);
                }
            }
            return className;
        }

        private static IEnumerable<Dependency> GetDependencies(FullNameToken callingToken, TypeReference calledType,
                                                               string methodName,
                                                               SequencePoint sequencePoint) {
            if (calledType is TypeSpecification) {
                // E.g. the reference type System.Int32&, which is used for out parameters.
                // or an arraytype?!?
                calledType = ((TypeSpecification)calledType).ElementType;
            }
            if (!(calledType is GenericInstanceType) && !(calledType is GenericParameter)) {
                // Currently, we do not look at generic type parameters; we would have to
                // untangle the usage of an actual (non-type-parameter) type's member
                // to get a useful dependency for the user.
                FullNameToken calledToken = GetFullnameToken(calledType, methodName);
                string fileName = null;
                uint startLine = 0;
                uint startColumn = 0;
                uint endLine = 0;
                uint endColumn = 0;
                if (sequencePoint != null) {
                    fileName = sequencePoint.Document.Url;
                    startLine = (uint)sequencePoint.StartLine;
                    startColumn = (uint)sequencePoint.StartColumn;
                    endLine = (uint)sequencePoint.EndLine;
                    endColumn = (uint)sequencePoint.EndColumn;
                }
                yield return new Dependency(callingToken, calledToken, fileName, startLine, startColumn, endLine, endColumn);
            }

            var genericInstanceType = calledType as GenericInstanceType;
            if (genericInstanceType != null) {
                foreach (TypeReference genericArgument in genericInstanceType.GenericArguments) {
                    foreach (
                        Dependency dependency in GetDependencies(callingToken, genericArgument, null, sequencePoint)) {
                        yield return dependency;
                    }
                }
            }
        }

        #region Nested type: TypeInfo

        private class TypeInfo {
            public string ClassName;
            public string NamespaceName;
            public string NestedClassName;
        }

        #endregion
    }
}