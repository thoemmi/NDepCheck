namespace NDepCheck.Calculating {
    public interface ICalculator : IPlugin {
        string Calculate(string[] values);
    }
}
