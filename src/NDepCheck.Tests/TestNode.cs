using System.Collections.Generic;

namespace NDepCheck.Tests {
    internal class TestNode : INode {
        private readonly List<TestEdge> _edges = new List<TestEdge>();
        private readonly string _name;
        private bool _isInner;
        private readonly ItemType _type;

        public TestNode(string name, bool isInner, ItemType type) {
            _name = name;
            _isInner = isInner;
            _type = type;
        }

        public override string ToString() {
            return Name;
        }

        public void AddEdgeTo(TestNode other) {
            _edges.Add(new TestEdge(this, other));
        }

        public IEnumerable<TestEdge> Edges => _edges;

        public bool IsInner => _isInner;

        public string Name => _name;

        public ItemType Type => _type;

        public void MarkIsInner() {
            _isInner = true;
        }
    }
}