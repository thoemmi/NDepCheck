namespace NDepCheck.Calculating {
    public class GetHash : ICalculator {
        public string GetHelp(bool detailedHelp, string filter) {
            return @"Compute hash of all input values.";
        }

        public string Calculate(string[] values) {
            int hash = 0;
            foreach (var v in values) {
                hash ^= v?.GetHashCode() ?? 0;
            }
            return "" + (hash & int.MaxValue); // always positive so that they can be used as markers (-am -123 would be an error!).
        }
    }
}
