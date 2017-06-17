namespace NDepCheck.VerySmallExplorativeTestAssembly {
    public class VerySmallTestClass {
        private readonly int _intField;

        public VerySmallTestClass(int intField) {
            _intField = intField;
        }

        public int Int() { return _intField; }

        public VerySmallTestClass Parameters(VerySmallTestClass @in, ref VerySmallTestClass @ref, out VerySmallTestClass @out, 
            int inValue, ref int refValue, out int outValue, VerySmallTestClass opt = null, int n = 999) {
            @out = null;
            outValue = 777;
            return @ref;
        }
    }
}
