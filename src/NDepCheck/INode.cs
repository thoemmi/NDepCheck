using JetBrains.Annotations;

namespace NDepCheck {
    public interface INode {
        [NotNull]
        string Name { get; }

        [NotNull]
        ItemType Type { get; }
    }
}