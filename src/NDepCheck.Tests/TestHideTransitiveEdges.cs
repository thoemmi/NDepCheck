using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.GraphTransformations;
using NDepCheck.Rendering;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestHideTransitiveEdges {
        private static readonly ItemType TEST = ItemType.New("TEST", new[] { "NAME" }, new[] { "" });

        private void CreateEdge(Dictionary<string, TestNode> nodes, string from, string to) {
            TestNode fromNode = GetOrCreate(nodes, "\"" + from + "\"");
            TestNode toNode = GetOrCreate(nodes, "\"" + to + "\"");
            fromNode.AddEdgeTo(toNode);
        }

        private TestNode GetOrCreate(Dictionary<string, TestNode> nodes, string name) {
            TestNode result;
            if (!nodes.TryGetValue(name, out result)) {
                nodes.Add(name, result = new TestNode(name, isInner: true, type: TEST));
            }
            return result;
        }

        [TestMethod]
        public void TestSmallHideTransitive() {
            var nodes = new Dictionary<string, TestNode>();
            CreateEdge(nodes, "BUC", "BAC");
            CreateEdge(nodes, "BUC", "AUS");
            //CreateEdge(nodes, "BUC", "VKF");
            CreateEdge(nodes, "AUS", "BUC");
            //CreateEdge(nodes, "AUS", "VKF");
            CreateEdge(nodes, "AUS", "BAC");
            //CreateEdge(nodes, "VKF", "BUC");
            //CreateEdge(nodes, "VKF", "BAC");
            //CreateEdge(nodes, "VKF", "AUS");
            CreateEdge(nodes, "BAC", "BUC");
            //CreateEdge(nodes, "BAC", "VKF");

            IEnumerable<TestEdge> edges = nodes.Values.SelectMany(n => n.Edges);
            int ctEdgesBeforeHiding = edges.Count(e => !e.Hidden);

            new HideTransitiveEdges<TestEdge>(new string[0]).Run(edges);

            using (var s = new MemoryStream()) {
                new GenericDotRenderer().RenderToStream(nodes.Values, edges, s, null);

                int ctEdgesAfterHiding = edges.Count(e => !e.Hidden);
                Assert.IsTrue(ctEdgesAfterHiding < ctEdgesBeforeHiding, ctEdgesAfterHiding + " not < " + ctEdgesBeforeHiding);

                // what else to assert??
            }
        }

        [TestMethod]
        public void TestLargeHideTransitive() {
            var nodes = new Dictionary<string, TestNode>();
            CreateEdge(nodes, "WLG.Bestellwesen.MI", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "WLG.Bestellwesen.MI", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "KST.Grosshandelsangebote.MI", "BUC");
            CreateEdge(nodes, "KST.Grosshandelsangebote.MI", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "KST.Grosshandelsangebote.MI", "KST");
            CreateEdge(nodes, "KST", "BUC");
            CreateEdge(nodes, "KST", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "KST", "VKF.Verkauf.MI");
            CreateEdge(nodes, "KST", "KST.AbdaAktuelleInfo.MI");
            CreateEdge(nodes, "KST", "BAC");
            CreateEdge(nodes, "KST", "LAG");
            CreateEdge(nodes, "KST", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "KST", "KAH");
            CreateEdge(nodes, "KST", "DataUpdate");
            CreateEdge(nodes, "KST", "IMP");
            CreateEdge(nodes, "KST", "WLG.Bestellwesen.MI");
            CreateEdge(nodes, "KST", "KST.Grosshandelsangebote.MI");
            CreateEdge(nodes, "KST", "AUS");
            CreateEdge(nodes, "KST", "WLG.OnlineBestellung.MI");
            CreateEdge(nodes, "KST", "KST.Bonussystem.MI");
            CreateEdge(nodes, "KST", "KST.InterneBonusmodelle.MI");
            CreateEdge(nodes, "KST", "OfficeIntegration");
            CreateEdge(nodes, "LAG", "BUC");
            CreateEdge(nodes, "LAG", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "LAG", "BAC");
            CreateEdge(nodes, "LAG", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "LAG", "KST");
            CreateEdge(nodes, "LAG", "Kommissionierer");
            CreateEdge(nodes, "LAG", "Sound");
            CreateEdge(nodes, "BUC", "BAC");
            CreateEdge(nodes, "BUC", "OfficeIntegration");
            CreateEdge(nodes, "BUC", "KAH");
            CreateEdge(nodes, "BUC", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "BUC", "RezeptScan");
            CreateEdge(nodes, "BUC", "AUS");
            CreateEdge(nodes, "BUC", "VKF");
            CreateEdge(nodes, "BUC", "WLG");
            CreateEdge(nodes, "BUC", "VKF.Verkauf.MI");
            CreateEdge(nodes, "BUC", "RezeptScan.MI");
            CreateEdge(nodes, "BUC", "KST");
            CreateEdge(nodes, "BUC", "Sound");
            CreateEdge(nodes, "BUC", "DataUpdate");
            CreateEdge(nodes, "AUS", "KST");
            CreateEdge(nodes, "AUS", "BUC");
            CreateEdge(nodes, "AUS", "LAG");
            CreateEdge(nodes, "AUS", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "AUS", "WLG");
            CreateEdge(nodes, "AUS", "VKF");
            CreateEdge(nodes, "AUS", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "AUS", "KAH");
            CreateEdge(nodes, "AUS", "VKF.Verkauf.MI");
            CreateEdge(nodes, "AUS", "OfficeIntegration");
            CreateEdge(nodes, "AUS", "BAC");
            CreateEdge(nodes, "AUS", "PUF");
            CreateEdge(nodes, "AUS", "WLG.Bestellwesen.MI");
            CreateEdge(nodes, "AUS", "IMP");
            CreateEdge(nodes, "AUS", "DataUpdate");
            CreateEdge(nodes, "IMP", "DataUpdate");
            CreateEdge(nodes, "IMP", "BUC");
            CreateEdge(nodes, "IMP", "KAH");
            CreateEdge(nodes, "IMP", "KST");
            CreateEdge(nodes, "IMP", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "IMP", "KVS");
            CreateEdge(nodes, "IMP", "Import");
            CreateEdge(nodes, "IMP", "KST.Grosshandelsangebote.MI");
            CreateEdge(nodes, "IMP", "BAC");
            CreateEdge(nodes, "IMP", "LAG");
            CreateEdge(nodes, "IMP", "VKF");
            CreateEdge(nodes, "IMP", "WLG");
            CreateEdge(nodes, "IMP", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "IMP", "DOK");
            CreateEdge(nodes, "WLG.OnlineBestellung.MI", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "WLG.OnlineBestellung.MI", "KST");
            CreateEdge(nodes, "WLG.OnlineBestellung.MI", "BUC");
            CreateEdge(nodes, "KVS", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "KVS", "KST");
            CreateEdge(nodes, "KVS", "BAC");
            CreateEdge(nodes, "KVS", "BUC");
            CreateEdge(nodes, "KVS", "KAH");
            CreateEdge(nodes, "PUF", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "PUF", "KST");
            CreateEdge(nodes, "PUF", "BUC");
            CreateEdge(nodes, "PUF", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "PUF", "WLG.Bestellwesen.MI");
            CreateEdge(nodes, "PUF", "WLG");
            CreateEdge(nodes, "PUF", "KAH");
            CreateEdge(nodes, "PUF", "KST.Grosshandelsangebote.MI");
            CreateEdge(nodes, "PUF", "LAG");
            CreateEdge(nodes, "PUF", "Sound");
            CreateEdge(nodes, "KST.Artikelstamm.MI", "BUC");
            CreateEdge(nodes, "KST.Artikelstamm.MI", "BAC");
            CreateEdge(nodes, "KAH", "IMP");
            CreateEdge(nodes, "KAH", "BUC");
            CreateEdge(nodes, "KAH", "BAC");
            CreateEdge(nodes, "KAH", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "KAH", "KST");
            CreateEdge(nodes, "KAH", "AUS");
            CreateEdge(nodes, "KAH", "VKF.Verkauf.MI");
            CreateEdge(nodes, "KAH", "KST.InterneBonusmodelle.MI");
            CreateEdge(nodes, "KAH", "OffenePosten.MI");
            CreateEdge(nodes, "RezeptScan.MI", "RezeptScan");
            CreateEdge(nodes, "VKF", "BUC");
            CreateEdge(nodes, "VKF", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "VKF", "BAC");
            CreateEdge(nodes, "VKF", "KAH");
            CreateEdge(nodes, "VKF", "AUS");
            CreateEdge(nodes, "VKF", "KST");
            CreateEdge(nodes, "VKF", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "VKF", "WLG");
            CreateEdge(nodes, "VKF", "VKF.Verkauf.MI");
            CreateEdge(nodes, "VKF", "LAG");
            CreateEdge(nodes, "VKF", "PUF");
            CreateEdge(nodes, "VKF", "OffenePosten.MI");
            CreateEdge(nodes, "VKF", "WLG.OnlineBestellung.MI");
            CreateEdge(nodes, "VKF", "KST.Bonussystem.MI");
            CreateEdge(nodes, "VKF", "RezeptScan.MI");
            CreateEdge(nodes, "VKF", "KVS");
            CreateEdge(nodes, "VKF", "KST.InterneBonusmodelle.MI");
            CreateEdge(nodes, "VKF", "RezeptScan");
            CreateEdge(nodes, "VKF", "Sound");
            CreateEdge(nodes, "VKF", "DOK");
            CreateEdge(nodes, "VKF", "ApplicationTransfer");
            CreateEdge(nodes, "VKF", "KST.Grosshandelsangebote.MI");
            CreateEdge(nodes, "WLG", "BUC");
            CreateEdge(nodes, "WLG", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "WLG", "KST");
            CreateEdge(nodes, "WLG", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "WLG", "AUS");
            CreateEdge(nodes, "WLG", "BAC");
            CreateEdge(nodes, "WLG", "KST.Grosshandelsangebote.MI");
            CreateEdge(nodes, "WLG", "DOK");
            CreateEdge(nodes, "WLG", "KAH");
            CreateEdge(nodes, "WLG", "PUF");
            CreateEdge(nodes, "WLG", "VKF.Verkauf.MI");
            CreateEdge(nodes, "WLG", "LAG");
            CreateEdge(nodes, "WLG", "WLG.OnlineBestellung.MI");
            CreateEdge(nodes, "WLG", "WLG.Bestellwesen.MI");
            CreateEdge(nodes, "WLG", "Sound");
            CreateEdge(nodes, "DOK", "BUC");
            CreateEdge(nodes, "DOK", "BAC");
            CreateEdge(nodes, "DOK", "KAH");
            CreateEdge(nodes, "DOK", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "DOK", "KST");
            CreateEdge(nodes, "EXP", "BUC");
            CreateEdge(nodes, "EXP", "KST");
            CreateEdge(nodes, "EXP", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "EXP", "KAH");
            CreateEdge(nodes, "EXP", "DataUpdate");
            CreateEdge(nodes, "EXP", "VKF");
            CreateEdge(nodes, "EXP", "KVS");
            CreateEdge(nodes, "EXP", "BAC");
            CreateEdge(nodes, "EXP", "WLG");
            CreateEdge(nodes, "OffenePosten.MI", "VKF.Verkauf.MI");
            CreateEdge(nodes, "OffenePosten.MI", "VKF");
            CreateEdge(nodes, "KST.AbdaAktuelleInfo.MI", "KST");
            CreateEdge(nodes, "BAC", "BUC");
            CreateEdge(nodes, "BAC", "DataUpdate");
            CreateEdge(nodes, "BAC", "VKF");
            CreateEdge(nodes, "BAC", "KAH");
            CreateEdge(nodes, "BAC", "WLG");
            CreateEdge(nodes, "BAC", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "KST.InterneBonusmodelle.MI", "BUC");
            CreateEdge(nodes, "KST.InterneBonusmodelle.MI", "KST.Bonussystem.MI");
            CreateEdge(nodes, "KST.InterneBonusmodelle.MI", "KST");
            CreateEdge(nodes, "KST.InterneBonusmodelle.MI", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "VKF.Verkauf.MI", "LAG.Lagerbestand.MI");
            CreateEdge(nodes, "VKF.Verkauf.MI", "BAC");
            CreateEdge(nodes, "VKF.Verkauf.MI", "BUC");
            CreateEdge(nodes, "KST.Bonussystem.MI", "BUC");
            CreateEdge(nodes, "LAG.Lagerbestand.MI", "KST.Artikelstamm.MI");
            CreateEdge(nodes, "LAG.Lagerbestand.MI", "BUC");
            CreateEdge(nodes, "LAG.Lagerbestand.MI", "KST.Bonussystem.MI");

            IEnumerable<TestEdge> edges = nodes.Values.SelectMany(n => n.Edges);
            int ctEdgesBeforeHiding = edges.Count(e => !e.Hidden);

            new HideTransitiveEdges<TestEdge>(new string[0]).Run(edges);

            using (var s = new MemoryStream()) {
                new GenericDotRenderer().RenderToStream(nodes.Values, edges, s, null);

                int ctEdgesAfterHiding = edges.Count(e => !e.Hidden);
                Assert.IsTrue(ctEdgesAfterHiding < ctEdgesBeforeHiding, ctEdgesAfterHiding + " not < " + ctEdgesBeforeHiding);

                // what else to assert??
            }
        }
    }
}
