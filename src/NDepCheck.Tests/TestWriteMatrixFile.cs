using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestWriteMatrixFile {
        [TestMethod]
        public void TestSimpleDAG() {
            TestEdge[] edges = SetupDAG();

            using (var sw = new StringWriter()) {
                DependencyGrapher.WriteMatrixFile(edges, sw, '1', null, false);
                Assert.AreEqual(@"Id;Name;!1;!2;!3
!1;n3  ; 1;  ;  
!2;n2  ; 1;  ;  
!3;n1  ; 1; 1;  
", sw.ToString());
            }
        }

        [TestMethod]
        public void TestSimpleDAGWithNotOkCounts() {
            TestEdge[] edges = SetupDAG();

            using (var sw = new StringWriter()) {
                DependencyGrapher.WriteMatrixFile(edges, sw, '1', null, true);
                Assert.AreEqual(@"Id;Name;!1;  ;!2;  ;!3;  
!1;n3  ; 1;  ;  ;  ;  ;  
!2;n2  ; 1;  ;  ;  ;  ;  
!3;n1  ; 1;  ; 1;  ;  ;  
", sw.ToString());
            }
        }

        private static TestEdge[] SetupDAG() {
            var nodes = new[] {
                new TestNode("n1", true, null),
                new TestNode("n2", true, null),
                new TestNode("n3", true, null)
            };
            var edges = new[] {
                new TestEdge(nodes[2], nodes[1]),
                new TestEdge(nodes[2], nodes[0]),
                new TestEdge(nodes[1], nodes[0]),
                // a loop on the lowest
                new TestEdge(nodes[2], nodes[2])
            };
            return edges;
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

            using (var sw = new StringWriter()) {
                DependencyGrapher.WriteMatrixFile(edges, sw, '1', null, false);
                Assert.AreEqual(@"Id  ;Name;!001;!002;!003
!001;n3  ;    ;    ;#  7
!002;n2  ;  55;    ;    
!003;n1  ; 111;  77; 999
", sw.ToString());
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

            using (var sw = new StringWriter()) {
                DependencyGrapher.WriteMatrixFile(edges, sw, '1', null, true);
                Assert.AreEqual(@"Id  ;Name;!001;    ;!002;    ;!003;    
!001;n3  ; 109;~ 54;#106;* 53;#103;* 51
!002;n2  ; 108;~ 54; 105;~ 52;#102;* 51
!003;n1  ; 107;~ 53; 104;~ 52; 101;~ 50
", sw.ToString());
            }
        }
    }
}
