namespace NDepCheck {
    public interface INode {
        //////IEnumerable<IEdge> Edges { get; } ___mhm_______________________________
        bool IsInner { get; }
        string Name { get; }
        ItemType Type { get; }
    }
}