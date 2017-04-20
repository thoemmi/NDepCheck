namespace NDepCheck.Tests {
    internal class TestEdge : IEdge {
        private readonly Item _usingNode;
        private readonly Item _usedNode;
        private readonly int _ct;
        private readonly int _notOkCt;

        public TestEdge(Item usingNode, Item usedNode, int ct = 1, int notOkCt = 0) {
            _usingNode = usingNode;
            _usedNode = usedNode;
            _ct = ct;
            _notOkCt = notOkCt;
        }

        public override string ToString() {
            return _usingNode + "->" + _ct + ";" + _notOkCt + "->" + _usedNode;
        }

        public Item UsedNode => _usedNode;

        public Item UsingNode => _usingNode;

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