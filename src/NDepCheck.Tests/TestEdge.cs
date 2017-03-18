namespace NDepCheck.Tests {
    internal class TestEdge : IEdge {
        private readonly INode _usingNode;
        private readonly INode _usedNode;
        private readonly int _ct;
        private readonly int _notOkCt;
        private bool _onCycle;
        private bool _carrysTransitive;

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

        public bool Hidden { get; set; }

        public bool OnCycle => _onCycle;

        public bool CarrysTransitive => _carrysTransitive;

        public INode UsingNode => _usingNode;

        public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
            return _usingNode.Name + " -> " + _usedNode.Name + ";";
        }

        public void MarkOnCycle() {
            _onCycle = true;
        }

        public int Ct => _ct;

        public int NotOkCt => _notOkCt;

        public void MarkCarrysTransitive() {
            _carrysTransitive = true;
        }

        public string AsDipStringWithTypes(bool withNotOkExampleInfo) {
            return ToString();
        }
    }
}