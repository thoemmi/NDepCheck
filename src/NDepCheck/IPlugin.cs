using JetBrains.Annotations;

namespace NDepCheck {
    public interface IPlugin {
        [NotNull]
        string GetHelp(bool detailedHelp);
    }
}