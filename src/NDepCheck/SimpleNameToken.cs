// (c) HMMüller 2006

using antlr;

namespace ILDASMDependencyChecker {
    /// <remarks>
    /// A token representing a single name. This is used
    /// either to represent a method name, or (in rare 
    /// cases) the name of a class in the anonymous 
    /// namespace.
    /// </remarks>
    public class SimpleNameToken : CommonToken {
        string _name;
        /// <summary>
        /// Name of this simple name token.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        internal SimpleNameToken(string name) {
            _name = name;
            // Dont forget to set the type! - see FullNameToken.
            Type = ILDASMLexerTokenTypes.FULLNAME;
        }

        /// <summary>
        /// Text representation of this token.
        /// </summary>
        /// <returns></returns>
        public override string getText() {
            return _name;
        }
    }

}
