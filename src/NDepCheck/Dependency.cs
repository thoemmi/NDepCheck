// (c) HMMüller 2006...2010

using System;

namespace NDepCheck {
    /// <remarks>Class <c>Dependency</c> stores
    /// knowledge about a concrete dependency
    /// (one "using item" uses one "used item").
    /// </remarks>
    public class Dependency {
        private readonly uint _endColumn;
        private readonly uint _endLine;
        private readonly string _fileName;
        private readonly uint _startColumn;
        private readonly uint _startLine;
        private readonly string _usedItem;
        private readonly string _usedNamespace;
        private readonly string _usingItem;
        private readonly string _usingNamespace;

        internal Dependency(FullNameToken usingItem, FullNameToken usedItem, string fileName, uint startLine, uint startColumn,
                            uint endLine, uint endColumn)
            : this(usingItem.ToString(),
                   usingItem.NamespaceName,
                   usedItem.ToString(),
                   usedItem.NamespaceName,
                   fileName,
                   startLine, startColumn, endLine, endColumn) {
            // empty
        }

        /// <summary>
        /// Create a dependency.
        /// </summary>
        /// <param name="usingItem">The using item.</param>
        /// <param name="usingItemNamespace">The using item namespace.</param>
        /// <param name="usedItem">The used item.</param>
        /// <param name="usedItemNamespace">The used item namespace.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="startLine">The start line.</param>
        /// <param name="startColumn">The start column.</param>
        /// <param name="endLine">The end line.</param>
        /// <param name="endColumn">The end column.</param>
        public Dependency(string usingItem, string usingItemNamespace, string usedItem, string usedItemNamespace, string fileName,
                          uint startLine, uint startColumn, uint endLine, uint endColumn) {
            if (usingItem == null)
                throw new ArgumentNullException("usingItem");
            if (usedItem == null)
                throw new ArgumentNullException("usedItem");
            _usingItem = usingItem;
            _usedItem = usedItem;
            _usingNamespace = usingItemNamespace; // != null ? string.Intern(usingItemNamespace) : null;
            _usedNamespace = usedItemNamespace; // != null ? string.Intern(usedItemNamespace) : null;
            _fileName = fileName; // != null ? string.Intern(fileName) : null;
            _startLine = startLine;
            _startColumn = startColumn;
            _endLine = endLine;
            _endColumn = endColumn;
        }

        /// <summary>
        /// Namespace name of using item.
        /// </summary>
        public string UsingNamespace {
            get { return _usingNamespace; }
        }

        /// <summary>
        /// Namespace name of used item.
        /// </summary>
        public string UsedNamespace {
            get { return _usedNamespace; }
        }

        /// <summary>
        /// Coded name of using item.
        /// </summary>
        public string UsingItem {
            get { return _usingItem; }
        }

        /// <value>
        /// Coded name of used item.
        /// </value>
        public string UsedItem {
            get { return _usedItem; }
        }

        /// <value>
        /// A guess where the use occurs in the
        /// original source file (usually derived
        /// from a .line directive).
        /// </value>
        public string FileName {
            get { return _fileName; }
        }

        /// <summary>
        /// Gets a guess of the line number in the original
        /// source file.
        /// </summary>
        /// <value>The line number.</value>
        public uint StartLine {
            get { return _startLine; }
        }

        public uint EndLine {
            get { return _endLine; }
        }

        public uint StartColumn {
            get { return _startColumn; }
        }

        public uint EndColumn {
            get { return _endColumn; }
        }

        /// <summary>
        /// String representation of a Dependency; this is used
        /// in <see>DependencyChecker</see> to check a Dependency
        /// against (the regular expressions of) rules.
        /// </summary>
        public override string ToString() {
            return _usingItem + " ---> " + _usedItem;
        }

        /// <summary>
        /// A message presented to the user of this Dependency is questionable.
        /// </summary>
        /// <returns></returns>
        public string QuestionableMessage() {
            return "Questionable dependency " + _usingItem + " ---> " + _usedItem;
        }
        /// <summary>
        /// A message presented to the user of this Dependency is not allowed.
        /// </summary>
        /// <returns></returns>
        public string IllegalMessage() {
            return "Illegal dependency " + _usingItem + " ---> " + _usedItem;
        }
    }
}