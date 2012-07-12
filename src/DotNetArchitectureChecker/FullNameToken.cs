// (c) HMMüller 2006...2010

using System;

namespace DotNetArchitectureChecker {
    /// <remarks>
    /// A token representing a complex name. This is used
    /// either to represent a class name (including a -
    /// possibly empty - namespace name); or a nested class
    /// name (i.e. a class name like before and a list of
    /// names of nested classes); or a method name of a
    /// class; or a method name of a nested class.
    /// </remarks>
    internal class FullNameToken {
        private readonly string _className;
        private readonly string _methodName;
        private readonly string _namespaceName;
        private readonly string _nestedName;

        internal FullNameToken(string namespaceName, string className, string nestedName, string methodName) {
            _namespaceName = namespaceName;
            _className = className;
            _nestedName = nestedName;
            _methodName = methodName;
            // Dont forget to set the type! Otherwise, it remains 0, which means "undefined"
            // and will usually be mapped to EOF.
            //Type = LexerTokenTypes.FULLNAME;
        }

        /// <summary>
        /// Constructor to create a method fullname from a class name (or nested 
        /// class name) and a bare method name.
        /// </summary>
        internal FullNameToken(FullNameToken parent, string methodName)
            : this(parent.NamespaceName, parent.ClassName, parent.NestedName, "::" + methodName) {
            if (parent._methodName != "") {
                throw new ApplicationException("Only FullNameToken without method name can be combined with a methodName");
            }
        }

        public string NamespaceName {
            get { return _namespaceName; }
        }

        public string ClassName {
            get { return _className; }
        }

        public string NestedName {
            get { return _nestedName; }
        }

        public string MethodName {
            get { return _methodName; }
        }

        public override string ToString() {
            return _namespaceName + _className + _nestedName + _methodName;
        }
    }
}