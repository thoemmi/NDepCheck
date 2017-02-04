using JetBrains.Annotations;

namespace NDepCheck {
    public interface INode {
        //////IEnumerable<IEdge> Edges { get; } ___mhm_______________________________
        bool IsInner { get; }

        [NotNull]
        string Name { get; }

        [NotNull]
        ItemType Type { get; }
    }
}