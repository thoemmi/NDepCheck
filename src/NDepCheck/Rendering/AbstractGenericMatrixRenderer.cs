using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    public abstract class AbstractMatrixRenderer {
        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            SomeRendererTestData.CreateSomeTestItems(out items, out dependencies);
        }

        public string GetHelp(bool detailedHelp) {
            return
@"  Write a textual matrix representation of dependencies.

  Options: [-l #] [-n] -o filename | filename
    -l #       maximal width of an item name
    -n         also output count of bad dependencies
    filename   output filename";
        }
    }

    public abstract class AbstractGenericMatrixRenderer : IRenderer<INode, IEdge> {
        protected static void ParseOptions(string argsAsString, out int? labelWidthOrNull, out bool withNotOkCt) {
            int lw = -1;
            bool wct = false;
            Options.Parse(argsAsString, new OptionAction('l', (args, j) => {
                if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out lw)) {
                    Options.Throw("No valid length after l", args);
                }
                return j;
            }), new OptionAction('n', (args, j) => {
                wct = true;
                return j;
            }));
            labelWidthOrNull = lw < 0 ? default(int?) : lw;
            withNotOkCt = wct;
        }

        private static List<INode> MoreOrLessTopologicalSort(IEnumerable<IEdge> edges) {
            Dictionary<INode, List<IEdge>> nodesAndEdges = Dependency.Edges2NodesAndEdgesList(edges);

            var result = new List<INode>();
            var resultAsHashSet = new HashSet<INode>();
            var candidates = new List<INode>(nodesAndEdges.Keys.OrderBy(n => n.Name));

            while (candidates.Any()) {
                //INode best =
                //    // First try real sinks, i.e., nodes where the number of outgoing edges is 0
                //    candidates.FirstOrDefault(n => WeightOfAllLeavingEdges(nodesAndEdges, n, e => true, e => 1) == 0)
                //    // Then, try "not ok sinks", i.e., nodes where there are as many not-ok edges to nodes below.
                //          ?? SinkCandidate(candidates, nodesAndEdges, e => !resultAsHashSet.Contains(e.UsedNode), e => 1000 * e.NotOkCt / (e.Ct + 1))
                //          ;

                // Keine ausgehenden Kanten; und keine reinkommenden mit notOk-Ct von candidates

                INode best = FindBest(candidates, true, nodesAndEdges) ?? FindBest(candidates, false, nodesAndEdges);

                if (best == null) {
                    throw new Exception("Error in algorithm - no best candidate found");
                }
                result.Add(best);
                resultAsHashSet.Add(best);

                candidates.Remove(best);
                foreach (var es in nodesAndEdges.Values) {
                    es.RemoveAll(e => e.UsedNode.Equals(best));
                }
            }
            result.Reverse();
            return result;
        }

        private static INode FindBest(List<INode> candidates, bool skipOutgoing, Dictionary<INode, List<IEdge>> nodesAndEdges) {
            var minimalIncomingNotOkWeight = int.MaxValue;
            var minimalLeavingWeight = int.MaxValue;
            INode result = null;
            foreach (var n in candidates) {
                int leavingWeight = nodesAndEdges[n].Where(e => !e.UsedNode.Equals(n)).Sum(e => e.Ct);

                if (skipOutgoing) {
                    if (leavingWeight > 0) {
                        continue;
                    }
                }

                int incomingNotOkWeight = candidates.Where(c => !c.Equals(n)).Sum(c => nodesAndEdges[c].Where(e => e.UsedNode.Equals(n)).Sum(e => e.NotOkCt));

                if (leavingWeight < minimalLeavingWeight
                    || leavingWeight == minimalLeavingWeight && incomingNotOkWeight < minimalIncomingNotOkWeight) {
                    minimalIncomingNotOkWeight = incomingNotOkWeight;
                    minimalLeavingWeight = leavingWeight;
                    result = n;
                }
            }
            return result;
        }

        protected static string Limit(string s, int lg) {
            return (s + Repeat(' ', lg)).Substring(0, lg);
        }

        protected static string Repeat(char c, int lg) {
            return string.Join("", Enumerable.Repeat(c, lg));
        }

        protected static string NodeId(INode n, string nodeFormat, Dictionary<INode, int> node2Index) {
            return (n.IsInner ? '!' : '%') + string.Format(nodeFormat, node2Index[n]);
        }

        protected static string FormatCt(bool withNotOkCt, string ctFormat, bool rightUpper, IWithCt e) {
            return (e.Ct > 0 ? string.Format(ctFormat, rightUpper ? '#' : ' ', e.Ct)
                       : string.Format(ctFormat, ' ', 0))
                   + (withNotOkCt ? ";" + (e.NotOkCt > 0 ? string.Format(ctFormat, rightUpper ? '*' : '~', e.NotOkCt)
                                        : string.Format(ctFormat, ' ', 0))
                       : "");
        }

        protected void Render(IEnumerable<INode> nodes, IEnumerable<IEdge> edges,
            [NotNull] TextWriter output, int? labelWidthOrNull, bool withNotOkCt) {
            IEnumerable<IEdge> visibleEdges = edges.Where(e => !e.Hidden);
            IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges = Dependency.Edges2NodesAndEdges(visibleEdges);

            var innerAndReachableOuterNodes =
                new HashSet<INode>(nodesAndEdges.Where(n => n.Key.IsInner).SelectMany(kvp => new[] { kvp.Key }.Concat(kvp.Value.Select(e => e.UsedNode))));

            IEnumerable<INode> sortedNodes = MoreOrLessTopologicalSort(visibleEdges).Where(n => innerAndReachableOuterNodes.Contains(n));

            if (sortedNodes.Any()) {

                int m = 0;
                Dictionary<INode, int> node2Index = sortedNodes.ToDictionary(n => n, n => ++m);

                IEnumerable<INode> topNodes = sortedNodes.Where(n => n.IsInner);

                int labelWidth = labelWidthOrNull ?? Math.Max(Math.Min(sortedNodes.Max(n => n.Name.Length), 30), 4);
                int colWidth = Math.Max(1 + ("" + visibleEdges.Max(e => e.Ct)).Length, // 1+ because of loop prefix
                    1 + ("" + sortedNodes.Count()).Length); // 1+ because of ! or % marker
                string nodeFormat = "{0," + (colWidth - 1) + ":" + Repeat('0', colWidth - 1) + "}";
                string ctFormat = "{0}{1," + (colWidth - 1) + ":" + Repeat('#', colWidth) + "}";

                Write(output, colWidth, labelWidth, topNodes, nodeFormat, node2Index, withNotOkCt, sortedNodes, ctFormat, nodesAndEdges);
            } else {
                Log.WriteError("No visible nodes and edges found for output");
            }
        }

        protected abstract void Write(TextWriter output, int colWidth, int labelWidth, IEnumerable<INode> topNodes,
            string nodeFormat, Dictionary<INode, int> node2Index, bool withNotOkCt, IEnumerable<INode> sortedNodes,
            string ctFormat, IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges);

        public abstract void Render(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, string argsAsString, string baseFilename);

        public abstract void RenderToStreamForUnitTests(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, Stream stream);

        public string GetHelp() {
            return $"{GetType().Name} usage: -___ outputfilename";
        }
    }
}