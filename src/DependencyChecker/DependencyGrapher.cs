// (c) HMMüller 2006

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DependencyChecker {
    /// <remarks>
    /// Class that creates AT&amp;T DOT output from
    /// dependencies.
    /// </remarks>
    public class DependencyGrapher {
        private readonly DependencyChecker _checker;
        private bool _debug;
        private string _dotFilename;
        private bool _showTransitiveEdges;
        private int? _stringLengthForIllegalEdges;

        private bool _verbose;

        public DependencyGrapher(DependencyChecker checker) {
            _checker = checker;
        }

        /// <value>
        /// Mark output of <c>DependencyChecker</c>
        /// as verbose.
        /// </value>
        public bool Verbose {
            set { _verbose = value; }
        }

        /// <summary>
        /// Set output file name. If set to <c>null</c> (or left 
        /// at <c>null</c>), no DOT output is created.
        /// </summary>
        public string DOTFilename {
            get { return _dotFilename; }
            set { _dotFilename = value; }
        }

        /// <value>
        /// If not null, show a concrete dependency 
        /// for each illegal edge.
        /// </value>
        public int? StringLengthForIllegalEdges {
            set { _stringLengthForIllegalEdges = value; }
        }

        /// <value>
        /// Show transitive edges. If set to <c>null</c> (or left 
        /// at <c>null</c>), transitive edges are heuristically
        /// removed.
        /// </value>
        public bool ShowTransitiveEdges {
            set { _showTransitiveEdges = value; }
        }

        public bool Debug {
            set { _debug = value; }
        }

        /// <summary>
        /// Create the graph for all dependencies passed in.
        /// </summary>
        private void Graph(IEnumerable<GraphAbstraction> graphAbstractions, List<DependencyRule> allowed,
                           List<DependencyRule> questionable,
                           List<DependencyRule> forbidden,
                           IEnumerable<Dependency> dependencies) {
            if (_dotFilename != null) {
                var nodes = new Dictionary<string, Node>();

                // First pass: Compute all edges - i.e., 
                // select the abstraction pattern = first
                // group from each regexp match and put it
                // into edgeToLabel and n.odes
                foreach (Dependency d in dependencies) {
                    ComputeDependencyEdge(graphAbstractions, allowed,
                                          questionable,
                                          forbidden,
                                          d, nodes);
                }

                // Second pass: Remove transitive
                // edges where possible.
                ComputeATransitiveReduction(nodes);

                // Third pass: Write out all nodes and
                // all edges.
                using (TextWriter tw = new StreamWriter(_dotFilename)) {
                    tw.WriteLine("digraph D {");
                    tw.WriteLine("ranksep = 1.5;");
                    foreach (string nodeName in nodes.Keys) {
                        tw.WriteLine(nodeName + " [shape=box];");
                    }
                    foreach (Node n in nodes.Values) {
                        foreach (Edge e in n.Edges.Values) {
                            tw.Write(e.Name);
                            if (_stringLengthForIllegalEdges != null && e.NotOkCt > 0) {
                                tw.Write(" [label=\"" +
                                         LimitWidth(e.NotOkExample.UsingItem, (int) _stringLengthForIllegalEdges) +
                                         " --->\\n" +
                                         LimitWidth(e.NotOkExample.UsedItem, (int) _stringLengthForIllegalEdges) +
                                         "\\n(1 of " + e.NotOkCt + ")\"];");
                            } else {
                                tw.WriteLine(";");
                            }
                        }
                    }
                    tw.WriteLine("}");
                }
            }
        }

        private static string LimitWidth(string s, int lg) {
            if (s.Length > lg) {
                s = "..." + s.Substring(s.Length - lg + 3);
            }
            return s;
        }

        private void ComputeDependencyEdge(IEnumerable<GraphAbstraction> graphAbstractions, List<DependencyRule> allowed,
                                           List<DependencyRule> questionable,
                                           List<DependencyRule> forbidden,
                                           Dependency d, Dictionary<string, Node> nodes) {
            string usingMatch = null;
            string usedMatch = null;
            foreach (GraphAbstraction ga in graphAbstractions) {
                string m = ga.Match(d.UsingItem);
                if (m != null) {
                    if (usingMatch == null || usingMatch.Length < m.Length) {
                        usingMatch = m;
                    }
                }
                string n = ga.Match(d.UsedItem);
                if (n != null) {
                    if (usedMatch == null || usedMatch.Length < n.Length) {
                        usedMatch = n;
                    }
                }
            }
            if (usingMatch == null) {
                DependencyCheckerMain.WriteInfo("No graph output pattern found for drawing " + d.UsingItem + " - I ignore it");
            } else if (usedMatch == null) {
                DependencyCheckerMain.WriteInfo("No graph output pattern found for drawing " + d.UsedItem + " - I ignore it");
            } else if (usingMatch == "" || usedMatch == "") {
                // ignore this edge!
            } else {
                bool isOk = _checker.Check(allowed, questionable, forbidden, d);

                // Filter out loops that are ok - they are not shown.
                // All other edges (non-loops; and non-ok loops) are shown.
                if (usingMatch != usedMatch || !isOk) {
                    usingMatch = "\"" + usingMatch + "\"";
                    Node usingNode = GetOrCreateNode(nodes, usingMatch);
                    usedMatch = "\"" + usedMatch + "\"";
                    Node usedNode = GetOrCreateNode(nodes, usedMatch);
                    Edge ed = usingNode.FindOrAddEdgeTo(usedNode);
                    if (!isOk) {
                        if (ed.NotOkExample == null)
                            ed.NotOkExample = d;
                        ed.NotOkCt++;
                    }
                }
            }
        }

        private static Node GetOrCreateNode(Dictionary<string, Node> nodes, string usingMatch) {
            if (!nodes.ContainsKey(usingMatch)) {
                nodes[usingMatch] = new Node(usingMatch);
            }
            return nodes[usingMatch];
        }

        private void ComputeATransitiveReduction(Dictionary<string, Node> nodes) {
            if (_showTransitiveEdges) {
                return;
            }

            var path = new List<Node>();
            foreach (Node n in nodes.Values) {
                foreach (Edge e in n.Edges.Values) {
                    // This will update all node.Distance fields.
                    ComputeDistanceFromN(n, 0, path);
                    if (path.Count != 0)
                        throw new InvalidProgramException("Internal error on traversal of " + e +
                                                          " - not all nodes removed from path");
                }

                List<Node> edgesToRemove = n.Edges.Keys.Where(succ => succ.Distance > 1).ToList();

                foreach (Node succ in edgesToRemove) {
                    n.Edges.Remove(succ);
                }
                foreach (Node n2 in nodes.Values) {
                    n2.Distance = null;
                }
            }
        }

        private static void ComputeDistanceFromN(Node n, uint length, List<Node> path) {
            if (n.Distance == null) {
                n.Distance = length;
                path.Insert(0, n);
                foreach (Edge e in n.Edges.Values) {
                    ComputeDistanceFromN(e.UsedNode, length + 1, path);
                }
                path.RemoveAt(0);
            } else {
                if (!path.Contains(n) && length > n.Distance)
                    n.Distance = length;
            }
        }

        public void Graph(DependencyRuleSet ruleSet, IEnumerable<Dependency> dependencies) {
            var graphAbstractions = new List<GraphAbstraction>();
            ruleSet.ExtractGraphAbstractions(graphAbstractions);

            var allowed = new List<DependencyRule>();
            var questionable = new List<DependencyRule>();
            var forbidden = new List<DependencyRule>();
            ruleSet.ExtractDependencyRules(allowed, questionable, forbidden);

            Graph(graphAbstractions, allowed, questionable, forbidden, dependencies);
        }

        #region Nested type: Edge

        private class Edge {
            public readonly Node UsedNode;
            private readonly Node _usingNode;
            public uint NotOkCt;
            public Dependency NotOkExample;

            public Edge(Node usingNode, Node usedNode) {
                _usingNode = usingNode;
                UsedNode = usedNode;
                NotOkCt = 0;
                NotOkExample = null;
            }

            public string Name {
                get { return CreateEdgeName(_usingNode.Name, UsedNode.Name); }
            }

            private static string CreateEdgeName(string usingMatch, string usedMatch) {
                return usingMatch + " -> " + usedMatch;
            }
        }

        #endregion

        #region Nested type: Node

        private class Node {
            public readonly Dictionary<Node, Edge> Edges = new Dictionary<Node, Edge>();
            public readonly string Name;
            internal uint? Distance;

            public Node(string name) {
                Name = name;
            }

            public Edge FindOrAddEdgeTo(Node usedNode) {
                if (!Edges.ContainsKey(usedNode)) {
                    Edges.Add(usedNode, new Edge(this, usedNode));
                }
                return Edges[usedNode];
            }

            public override string ToString() {
                return Name + ":" + Distance;
            }
        }

        #endregion
    }
}