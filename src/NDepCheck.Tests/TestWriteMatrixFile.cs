using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestWriteMatrixFile {
        [TestMethod]
        public void TestSimpleDAG() {
            TestNode[] nodes;
            TestEdge[] edges;
            SetupDAG(out nodes, out edges);


            using (var s = new MemoryStream()) {
                new GenericMatrixRenderer1().RenderToStreamForUnitTests(nodes, edges, s);
            
                Assert.AreEqual(@"Id;Name;!1;  ;!2;  ;!3;  
!1;n3  ; 1;  ;  ;  ;  ;  
!2;n2  ; 1;  ;  ;  ;  ;  
!3;n1  ; 1;  ; 1;  ;  ;  
", Encoding.ASCII.GetString(s.ToArray()));
            }
        }

        [TestMethod]
        public void TestSimpleDAGWithNotOkCounts() {
            TestNode[] nodes;
            TestEdge[] edges;
            SetupDAG(out nodes, out edges);


            using (var s = new MemoryStream()) {
                new GenericMatrixRenderer1().RenderToStreamForUnitTests(nodes, edges, s);
                Assert.AreEqual(@"Id;Name;!1;  ;!2;  ;!3;  
!1;n3  ; 1;  ;  ;  ;  ;  
!2;n2  ; 1;  ;  ;  ;  ;  
!3;n1  ; 1;  ; 1;  ;  ;  
", Encoding.ASCII.GetString(s.ToArray()));
            }
        }

        private static void SetupDAG(out TestNode[] nodes, out TestEdge[] edges) {
            nodes = new[] {
                new TestNode("n1", true, null),
                new TestNode("n2", true, null),
                new TestNode("n3", true, null)
            };
            edges = new[] {
                new TestEdge(nodes[2], nodes[1]),
                new TestEdge(nodes[2], nodes[0]),
                new TestEdge(nodes[1], nodes[0]),
                // a loop on the lowest
                new TestEdge(nodes[2], nodes[2])
            };
        }

        [TestMethod]
        public void TestSimpleCycle() {
            var nodes = new[] {
                new TestNode("n1", true, null),
                new TestNode("n2", true, null),
                new TestNode("n3", true, null)
            };
            var edges = new[] {
                new TestEdge(nodes[2], nodes[1], 55),
                new TestEdge(nodes[2], nodes[0], 111),
                new TestEdge(nodes[1], nodes[0], 77),
                new TestEdge(nodes[0], nodes[2], 7),
                new TestEdge(nodes[0], nodes[0], 999),
            };

            using (var s = new MemoryStream()) {
                new GenericMatrixRenderer1().RenderToStreamForUnitTests(nodes, edges, s);
                Assert.AreEqual(@"Id  ;Name;!001;    ;!002;    ;!003;    
!001;n3  ;    ;    ;    ;    ;#  7;    
!002;n2  ;  55;    ;    ;    ;    ;    
!003;n1  ; 111;    ;  77;    ; 999;    
", Encoding.ASCII.GetString(s.ToArray()));
            }

        }


        [TestMethod]
        public void TestCompleteGraph() {
            var nodes = new[] {
                new TestNode("n1", true, null),
                new TestNode("n2", true, null),
                new TestNode("n3", true, null)
            };
            int ct = 100;
            TestEdge[] edges = nodes.SelectMany(from => nodes.Select(to => new TestEdge(from, to, ++ct, ct / 2))).ToArray();

            using (var s = new MemoryStream()) {
                new GenericMatrixRenderer1().RenderToStreamForUnitTests(nodes, edges, s);
                Assert.AreEqual(@"Id  ;Name;!001;    ;!002;    ;!003;    
!001;n3  ; 109;~ 54;#106;* 53;#103;* 51
!002;n2  ; 108;~ 54; 105;~ 52;#102;* 51
!003;n1  ; 107;~ 53; 104;~ 52; 101;~ 50
", Encoding.ASCII.GetString(s.ToArray()));
            }
        }
    }
}
