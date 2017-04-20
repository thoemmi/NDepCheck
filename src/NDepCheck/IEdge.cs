using JetBrains.Annotations;

namespace NDepCheck {
    public static class EdgeConstants {
        public const string DIP_ARROW = "=>";
    }

    public interface IWithCt {
        int Ct { get; }
        int NotOkCt { get; }
    }

    public interface IEdge : IWithCt {
        [NotNull]
        Item UsingNode { get; }
        [NotNull]
        Item UsedNode { get; }
        [NotNull]
        string GetDotRepresentation(int? stringLengthForIllegalEdges);

        [NotNull]
        string AsDipStringWithTypes(bool withExampleInfo);
    }
}