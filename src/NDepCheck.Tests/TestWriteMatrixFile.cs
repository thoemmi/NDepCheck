using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Rendering.GraphicsRendering;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestWriteMatrixFile {
        private static Dependency CreateDependency(Environment env, Item @using, Item used, int ct = 1, int badCt = 0) {
            return env.CreateDependency(@using, used, null, "", ct, badCt: badCt);
        }

        [TestMethod]
        public void TestSimpleDAG() {
            var gc = new GlobalContext();

            Item[] items;
            Dependency[] dependencies;
            SetupDAG(gc, out items, out dependencies);

            using (var s = new MemoryStream()) {
                new MatrixRenderer1().RenderToStreamForUnitTests(gc, dependencies, s, "");

                Assert.AreEqual(@"Id;Name;1;  ;2;  ;3;  
1;n3  ; 1;  ;  ;  ;  ;  
2;n2  ; 1;  ;  ;  ;  ;  
3;n1  ; 1;  ; 1;  ;  ;  
", Encoding.ASCII.GetString(s.ToArray()));
            }
        }

        [TestMethod]
        public void TestSimpleDAGWithNotOkCounts() {
            var gc = new GlobalContext();

            Item[] nodes;
            Dependency[] edges;
            SetupDAG(gc, out nodes, out edges);

            using (var s = new MemoryStream()) {
                new MatrixRenderer1().RenderToStreamForUnitTests(gc, edges, s, "");
                Assert.AreEqual(@"Id;Name;1;  ;2;  ;3;  
1;n3  ; 1;  ;  ;  ;  ;  
2;n2  ; 1;  ;  ;  ;  ;  
3;n1  ; 1;  ; 1;  ;  ;  
", Encoding.ASCII.GetString(s.ToArray()));
            }
        }

        private static void SetupDAG(GlobalContext gc, out Item[] items, out Dependency[] dependencies) {
            Environment env = gc.CurrentEnvironment;

            items = new[] {
                env.NewItem(ItemType.SIMPLE, new[] {"n1"}),
                env.NewItem(ItemType.SIMPLE, new[] {"n2"}),
                env.NewItem(ItemType.SIMPLE, new[] {"n3"})
            };
            dependencies = new[] {
                CreateDependency(env, items[2], items[1]),
                CreateDependency(env, items[2], items[0]),
                CreateDependency(env, items[1], items[0]),
                // a loop on the lowest
                CreateDependency(env, items[2], items[2])
            };
        }

        [TestMethod]
        public void TestSimpleCycle() {
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var nodes = new[] {
                env.NewItem(ItemType.SIMPLE, new[] {"n1"}),
                env.NewItem(ItemType.SIMPLE, new[] {"n2"}),
                env.NewItem(ItemType.SIMPLE, new[] {"n3"})
            };
            var edges = new[] {
                CreateDependency(env, nodes[2], nodes[1], 55),
                CreateDependency(env, nodes[2], nodes[0], 111),
                CreateDependency(env, nodes[1], nodes[0], 77),
                CreateDependency(env, nodes[0], nodes[2], 7),
                CreateDependency(env, nodes[0], nodes[0], 999),
            };

            using (var s = new MemoryStream()) {
                new MatrixRenderer1().RenderToStreamForUnitTests(gc, edges, s, "");
                Assert.AreEqual(@"Id  ;Name;001;    ;002;    ;003;    
001;n3  ;    ;    ;    ;    ;#  7;    
002;n2  ;  55;    ;    ;    ;    ;    
003;n1  ; 111;    ;  77;    ; 999;    
", Encoding.ASCII.GetString(s.ToArray()));
            }

        }


        [TestMethod]
        public void TestCompleteGraph() {
            var gc = new GlobalContext();
            Environment env = gc.CurrentEnvironment;

            var nodes = new[] {
                env.NewItem(ItemType.SIMPLE, new[] {"n1"}),
                env.NewItem(ItemType.SIMPLE, new[] {"n2"}),
                env.NewItem(ItemType.SIMPLE, new[] {"n3"})
            };
            int ct = 100;
            Dependency[] edges = nodes.SelectMany(from => nodes.Select(to => CreateDependency(env, from, to, ++ct, ct / 2))).ToArray();

            using (var s = new MemoryStream()) {
                new MatrixRenderer1().RenderToStreamForUnitTests(gc, edges, s, "");
                Assert.AreEqual(@"Id  ;Name;001;    ;002;    ;003;    
001;n3  ; 109;~ 54;#106;* 53;#103;* 51
002;n2  ; 108;~ 54; 105;~ 52;#102;* 51
003;n1  ; 107;~ 53; 104;~ 52; 101;~ 50
", Encoding.ASCII.GetString(s.ToArray()));
            }
        }
    }
}
