namespace NDepCheck.VerySmallExplorativeTestAssembly {
    public class VerySmallTestClass {
        private readonly int _intField;

        public VerySmallTestClass(int intField) {
            _intField = intField;
        }

        public int Int() { return _intField; }
    }
}
