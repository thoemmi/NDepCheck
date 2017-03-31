namespace NDepCheck.Tests {
    internal class TestEdge : IEdge {
        private readonly INode _usingNode;
        private readonly INode _usedNode;
        private readonly int _ct;
        private readonly int _notOkCt;

        public TestEdge(INode usingNode, INode usedNode, int ct = 1, int notOkCt = 0) {
            _usingNode = usingNode;
            _usedNode = usedNode;
            _ct = ct;
            _notOkCt = notOkCt;
        }

        public override string ToString() {
            return _usingNode + "->" + _ct + ";" + _notOkCt + "->" + _usedNode;
        }

        public INode UsedNode => _usedNode;

        public INode UsingNode => _usingNode;

        public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
            return _usingNode.Name + " -> " + _usedNode.Name + ";";
        }

        public int Ct => _ct;

        public int NotOkCt => _notOkCt;

        public string AsDipStringWithTypes(bool withExampleInfo) {
            return ToString();
        }
    }
}