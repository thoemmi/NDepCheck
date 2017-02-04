namespace NDepCheck {
    public interface IWithCt {
        int Ct { get; }
        int NotOkCt { get; }
    }

    public interface IEdge : IWithCt {
        INode UsingNode { get; }
        INode UsedNode { get; }
        bool Hidden { get; set; }
        string GetDotRepresentation(int? stringLengthForIllegalEdges);
        void MarkOnCycle();
        void MarkCarrysTransitive();
        string AsStringWithTypes();
    }
}