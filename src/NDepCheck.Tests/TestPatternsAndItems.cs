using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestPatternsAndItems {
        private const bool IGNORECASE = false;

        [TestMethod]
        public void TestSimpleDependencyRuleMatches() {
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation("FILE", 0, "...", false);
            ItemType ITEMTYPE = new ItemType("TEST", new[] { "NAMESPACE", "CLASS" }, new[] { "", "" });
            var r1 = new DependencyRule(ITEMTYPE, ":", ITEMTYPE, ":", rep, IGNORECASE);

            var rn1 = new DependencyRule(ITEMTYPE, "n*", ITEMTYPE, ":", rep, IGNORECASE);
            var rn2 = new DependencyRule(ITEMTYPE, "n*:", ITEMTYPE, ":", rep, IGNORECASE);
            var rn3 = new DependencyRule(ITEMTYPE, "n**", ITEMTYPE, ":", rep, IGNORECASE);
            var rn4 = new DependencyRule(ITEMTYPE, "n**:", ITEMTYPE, ":", rep, IGNORECASE);
            var rn5 = new DependencyRule(ITEMTYPE, "n1", ITEMTYPE, ":", rep, IGNORECASE);
            var rn6 = new DependencyRule(ITEMTYPE, "n1:", ITEMTYPE, ":", rep, IGNORECASE);
            var rn7 = new DependencyRule(ITEMTYPE, "*n1:", ITEMTYPE, ":", rep, IGNORECASE);
            var rn8 = new DependencyRule(ITEMTYPE, "**n1:", ITEMTYPE, ":", rep, IGNORECASE);

            var rc1 = new DependencyRule(ITEMTYPE, ":c*", ITEMTYPE, ":", rep, IGNORECASE);
            var rc2 = new DependencyRule(ITEMTYPE, ":c**", ITEMTYPE, ":", rep, IGNORECASE);
            var rc3 = new DependencyRule(ITEMTYPE, ":*c1", ITEMTYPE, ":", rep, IGNORECASE);
            var rc4 = new DependencyRule(ITEMTYPE, ":**c1", ITEMTYPE, ":", rep, IGNORECASE);

            var rnc1 = new DependencyRule(ITEMTYPE, "n*:c*", ITEMTYPE, ":", rep, IGNORECASE);
            var rnc2 = new DependencyRule(ITEMTYPE, "n**:c**", ITEMTYPE, ":", rep, IGNORECASE);

            Dependency dep = new Dependency(new Item(ITEMTYPE, "n1", "c1"), new Item(ITEMTYPE, "n2", "c2"), null, 0, 0, 0, 0);
            Assert.IsTrue(r1.IsMatch(dep));

            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsTrue(rn4.IsMatch(dep));
            Assert.IsTrue(rn5.IsMatch(dep));
            Assert.IsTrue(rn6.IsMatch(dep));
            Assert.IsTrue(rn7.IsMatch(dep));
            Assert.IsTrue(rn8.IsMatch(dep));

            Assert.IsTrue(rc1.IsMatch(dep));
            Assert.IsTrue(rc2.IsMatch(dep));
            Assert.IsTrue(rc3.IsMatch(dep));
            Assert.IsTrue(rc4.IsMatch(dep));

            Assert.IsTrue(rnc1.IsMatch(dep));
            Assert.IsTrue(rnc2.IsMatch(dep));
        }

        [TestMethod]
        public void TestBackReferenceDependencyRuleMatches() {
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation("FILE", 0, "...", false);
            ItemType ITEMTYPE = new ItemType("TEST", new[] { "SCHEMA", "OBJECT" }, new[] { "", "" });
            var rn1 = new DependencyRule(ITEMTYPE, "(s)*", ITEMTYPE, @"\1*", rep, IGNORECASE);
            var rn2 = new DependencyRule(ITEMTYPE, "(s)*:(t)*", ITEMTYPE, @"\1*:\2*", rep, IGNORECASE);
            var rn3 = new DependencyRule(ITEMTYPE, "(s)**:(t)**", ITEMTYPE, @"\1*:\2*", rep, IGNORECASE);
            var rn4 = new DependencyRule(ITEMTYPE, "s(*):t(*)", ITEMTYPE, @"s\1:t\2", rep, IGNORECASE);
            var rn5 = new DependencyRule(ITEMTYPE, "s(**):t(**)", ITEMTYPE, @"s\1:t\2", rep, IGNORECASE);

            Dependency dep = new Dependency(new Item(ITEMTYPE, "s1", "t1"), new Item(ITEMTYPE, "s2", "t2"), null, 0, 0, 0, 0);
            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsFalse(rn4.IsMatch(dep));
            Assert.IsFalse(rn5.IsMatch(dep));
        }

        [TestMethod]
        public void TestMoreBackReferenceDependencyRuleMatches() {
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation("FILE", 0, "...", false);
            ItemType ITEMTYPE = new ItemType("TEST", new[] { "SCHEMA", "OBJECT" }, new[] { "", "" });
            var rn1 = new DependencyRule(ITEMTYPE, "(s)*", ITEMTYPE, @"\1*", rep, IGNORECASE);
            var rn2 = new DependencyRule(ITEMTYPE, "(s*)", ITEMTYPE, @"\1", rep, IGNORECASE);
            var rn3 = new DependencyRule(ITEMTYPE, "s(*)", ITEMTYPE, @"s\1", rep, IGNORECASE);
            var rn4 = new DependencyRule(ITEMTYPE, "s(*):(t)*", ITEMTYPE, @"s\1:\2*", rep, IGNORECASE);
            var rn5 = new DependencyRule(ITEMTYPE, "s*:t(*)", ITEMTYPE, @"s\1:t*", rep, IGNORECASE);

            var rn6 = new DependencyRule(ITEMTYPE, "s*:(t*)", ITEMTYPE, @"s\1:t*", rep, IGNORECASE);

            Dependency dep = new Dependency(new Item(ITEMTYPE, "s1", "t1"), new Item(ITEMTYPE, "s1", "t2"), null, 0, 0, 0, 0);
            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsTrue(rn4.IsMatch(dep));
            Assert.IsTrue(rn5.IsMatch(dep));
            Assert.IsFalse(rn6.IsMatch(dep));
        }

        [TestMethod]
        public void TestSimpleGraphAbstractionMatches() {
            ItemType ITEMTYPE = new ItemType("TEST", new[] { "NAMESPACE", "CLASS" }, new[] { "", "" });
            var g1 = new GraphAbstraction(ITEMTYPE, "**():", false, IGNORECASE);

            var gn1 = new GraphAbstraction(ITEMTYPE, "(n)*", false, IGNORECASE);
            var gn2 = new GraphAbstraction(ITEMTYPE, "(n)*:", false, IGNORECASE);
            var gn3 = new GraphAbstraction(ITEMTYPE, "(n)**", false, IGNORECASE);
            var gn4 = new GraphAbstraction(ITEMTYPE, "(n)**:", false, IGNORECASE);
            var gn5 = new GraphAbstraction(ITEMTYPE, "(n)1", false, IGNORECASE);
            var gn6 = new GraphAbstraction(ITEMTYPE, "(n)1:", false, IGNORECASE);
            var gn7 = new GraphAbstraction(ITEMTYPE, "(*n)1:", false, IGNORECASE);
            var gn8 = new GraphAbstraction(ITEMTYPE, "**(n)1:", false, IGNORECASE);

            var hn1 = new GraphAbstraction(ITEMTYPE, "n(*)", false, IGNORECASE);
            var hn2 = new GraphAbstraction(ITEMTYPE, "n(*):", false, IGNORECASE);
            var hn3 = new GraphAbstraction(ITEMTYPE, "n(**)", false, IGNORECASE);
            var hn4 = new GraphAbstraction(ITEMTYPE, "n(**):", false, IGNORECASE);
            var hn5 = new GraphAbstraction(ITEMTYPE, "n(1)", false, IGNORECASE);
            var hn6 = new GraphAbstraction(ITEMTYPE, "n(1):", false, IGNORECASE);
            var hn7 = new GraphAbstraction(ITEMTYPE, "*n(1):", false, IGNORECASE);
            var hn8 = new GraphAbstraction(ITEMTYPE, "**n(1):", false, IGNORECASE);

            var gc1 = new GraphAbstraction(ITEMTYPE, ":(c)*", false, IGNORECASE);
            var gc2 = new GraphAbstraction(ITEMTYPE, ":(c)**", false, IGNORECASE);
            var gc3 = new GraphAbstraction(ITEMTYPE, ":(*c)1", false, IGNORECASE);
            var gc4 = new GraphAbstraction(ITEMTYPE, ":(**c)1", false, IGNORECASE);

            var hc1 = new GraphAbstraction(ITEMTYPE, ":c(*)", false, IGNORECASE);
            var hc2 = new GraphAbstraction(ITEMTYPE, ":c(*)*", false, IGNORECASE);
            var hc3 = new GraphAbstraction(ITEMTYPE, ":*c(1)", false, IGNORECASE);
            var hc4 = new GraphAbstraction(ITEMTYPE, ":**c(1)", false, IGNORECASE);

            var gnc1 = new GraphAbstraction(ITEMTYPE, "n(*):(c)*", false, IGNORECASE);
            var gnc2 = new GraphAbstraction(ITEMTYPE, "n(**):(c)**", false, IGNORECASE);

            Item i = new Item(ITEMTYPE, "n1", "c1");

            bool isInner;
            Assert.AreEqual("", g1.Match(i, out isInner));

            Assert.AreEqual("n", gn1.Match(i, out isInner));
            Assert.AreEqual("n", gn2.Match(i, out isInner));
            Assert.AreEqual("n", gn3.Match(i, out isInner));
            Assert.AreEqual("n", gn4.Match(i, out isInner));
            Assert.AreEqual("n", gn5.Match(i, out isInner));
            Assert.AreEqual("n", gn6.Match(i, out isInner));
            Assert.AreEqual("n", gn7.Match(i, out isInner));
            Assert.AreEqual("n", gn8.Match(i, out isInner));

            Assert.AreEqual("1", hn1.Match(i, out isInner));
            Assert.AreEqual("1", hn2.Match(i, out isInner));
            Assert.AreEqual("1", hn3.Match(i, out isInner));
            Assert.AreEqual("1", hn4.Match(i, out isInner));
            Assert.AreEqual("1", hn5.Match(i, out isInner));
            Assert.AreEqual("1", hn6.Match(i, out isInner));
            Assert.AreEqual("1", hn7.Match(i, out isInner));
            Assert.AreEqual("1", hn8.Match(i, out isInner));

            Assert.AreEqual("c", gc1.Match(i, out isInner));
            Assert.AreEqual("c", gc2.Match(i, out isInner));
            Assert.AreEqual("c", gc3.Match(i, out isInner));
            Assert.AreEqual("c", gc4.Match(i, out isInner));

            Assert.AreEqual("1", hc1.Match(i, out isInner));
            Assert.AreEqual("1", hc2.Match(i, out isInner));
            Assert.AreEqual("1", hc3.Match(i, out isInner));
            Assert.AreEqual("1", hc4.Match(i, out isInner));

            Assert.AreEqual("1:c", gnc1.Match(i, out isInner));
            Assert.AreEqual("1:c", gnc2.Match(i, out isInner));
        }

        [TestMethod]
        public void TestRegexGraphAbstractionMatches() {
            ItemType ITEMTYPE = new ItemType("TEST", new[] { "NAMESPACE", "CLASS" }, new[] { "", "" });
            var g1 = new GraphAbstraction(ITEMTYPE, "^.*()$:", false, IGNORECASE);

            var gn1a = new GraphAbstraction(ITEMTYPE, "^(n).*", false, IGNORECASE);
            var gn1b = new GraphAbstraction(ITEMTYPE, "^(n).*$", false, IGNORECASE);
            var gn1c = new GraphAbstraction(ITEMTYPE, "(n).*$", false, IGNORECASE);
            var gn2a = new GraphAbstraction(ITEMTYPE, "^(n).*:", false, IGNORECASE);
            var gn2b = new GraphAbstraction(ITEMTYPE, "^(n).*$:", false, IGNORECASE);
            var gn2c = new GraphAbstraction(ITEMTYPE, "(n).*$:", false, IGNORECASE);
            var gn5a = new GraphAbstraction(ITEMTYPE, "^(n)1", false, IGNORECASE);
            var gn5b = new GraphAbstraction(ITEMTYPE, "^(n)1$", false, IGNORECASE);
            var gn5c = new GraphAbstraction(ITEMTYPE, "(n)1$", false, IGNORECASE);
            var gn6a = new GraphAbstraction(ITEMTYPE, "^(n)1:", false, IGNORECASE);
            var gn6b = new GraphAbstraction(ITEMTYPE, "^(n)1$:", false, IGNORECASE);
            var gn6c = new GraphAbstraction(ITEMTYPE, "(n)1$:", false, IGNORECASE);
            var gn7a = new GraphAbstraction(ITEMTYPE, "^(.*n)1:", false, IGNORECASE);
            var gn7b = new GraphAbstraction(ITEMTYPE, "^(.*n)1$:", false, IGNORECASE);
            var gn7c = new GraphAbstraction(ITEMTYPE, "(.*n)1$:", false, IGNORECASE);

            var hn1a = new GraphAbstraction(ITEMTYPE, "^n(.*)", false, IGNORECASE);
            var hn1b = new GraphAbstraction(ITEMTYPE, "^n(.*)$", false, IGNORECASE);
            var hn1c = new GraphAbstraction(ITEMTYPE, "n(.*)$", false, IGNORECASE);
            var hn2a = new GraphAbstraction(ITEMTYPE, "^n(.*):", false, IGNORECASE);
            var hn2b = new GraphAbstraction(ITEMTYPE, "^n(.*)$:", false, IGNORECASE);
            var hn2c = new GraphAbstraction(ITEMTYPE, "n(.*)$:", false, IGNORECASE);
            var hn5a = new GraphAbstraction(ITEMTYPE, "^n(1)", false, IGNORECASE);
            var hn5b = new GraphAbstraction(ITEMTYPE, "^n(1)$", false, IGNORECASE);
            var hn5c = new GraphAbstraction(ITEMTYPE, "n(1)$", false, IGNORECASE);
            var hn6a = new GraphAbstraction(ITEMTYPE, "^n(1):", false, IGNORECASE);
            var hn6b = new GraphAbstraction(ITEMTYPE, "^n(1)$:", false, IGNORECASE);
            var hn6c = new GraphAbstraction(ITEMTYPE, "n(1)$:", false, IGNORECASE);
            var hn7a = new GraphAbstraction(ITEMTYPE, "^.*n(1):", false, IGNORECASE);
            var hn7b = new GraphAbstraction(ITEMTYPE, "^.*n(1)$:", false, IGNORECASE);
            var hn7c = new GraphAbstraction(ITEMTYPE, ".*n(1)$:", false, IGNORECASE);

            var gc1a = new GraphAbstraction(ITEMTYPE, ":^(c).*", false, IGNORECASE);
            var gc1b = new GraphAbstraction(ITEMTYPE, ":^(c).*$", false, IGNORECASE);
            var gc1c = new GraphAbstraction(ITEMTYPE, ":(c).*$", false, IGNORECASE);
            var gc3a = new GraphAbstraction(ITEMTYPE, ":^(.*c)1", false, IGNORECASE);
            var gc3b = new GraphAbstraction(ITEMTYPE, ":^(.*c)1$", false, IGNORECASE);
            var gc3c = new GraphAbstraction(ITEMTYPE, ":(.*c)1$", false, IGNORECASE);

            //var hc1 = new GraphAbstraction_(":c(*)", false);
            //var hc2 = new GraphAbstraction_(":c(*)*", false);
            //var hc3 = new GraphAbstraction_(":*c(1)", false);
            //var hc4 = new GraphAbstraction_(":**c(1)", false);

            //var gnc1 = new GraphAbstraction_("n(*):(c)*", false);
            //var gnc2 = new GraphAbstraction_("n(**):(c)**", false);

            Item i = new Item(ITEMTYPE, "n1", "c1");

            bool isInner;
            Assert.AreEqual("", g1.Match(i, out isInner));

            Assert.AreEqual("n", gn1a.Match(i, out isInner));
            Assert.AreEqual("n", gn1b.Match(i, out isInner));
            Assert.AreEqual("n", gn1c.Match(i, out isInner));
            Assert.AreEqual("n", gn2a.Match(i, out isInner));
            Assert.AreEqual("n", gn2b.Match(i, out isInner));
            Assert.AreEqual("n", gn2c.Match(i, out isInner));
            Assert.AreEqual("n", gn5a.Match(i, out isInner));
            Assert.AreEqual("n", gn5b.Match(i, out isInner));
            Assert.AreEqual("n", gn5c.Match(i, out isInner));
            Assert.AreEqual("n", gn6a.Match(i, out isInner));
            Assert.AreEqual("n", gn6b.Match(i, out isInner));
            Assert.AreEqual("n", gn6c.Match(i, out isInner));
            Assert.AreEqual("n", gn7a.Match(i, out isInner));
            Assert.AreEqual("n", gn7b.Match(i, out isInner));
            Assert.AreEqual("n", gn7c.Match(i, out isInner));

            Assert.AreEqual("1", hn1a.Match(i, out isInner));
            Assert.AreEqual("1", hn1b.Match(i, out isInner));
            Assert.AreEqual("1", hn1c.Match(i, out isInner));
            Assert.AreEqual("1", hn2a.Match(i, out isInner));
            Assert.AreEqual("1", hn2b.Match(i, out isInner));
            Assert.AreEqual("1", hn2c.Match(i, out isInner));
            Assert.AreEqual("1", hn5a.Match(i, out isInner));
            Assert.AreEqual("1", hn5b.Match(i, out isInner));
            Assert.AreEqual("1", hn5c.Match(i, out isInner));
            Assert.AreEqual("1", hn6a.Match(i, out isInner));
            Assert.AreEqual("1", hn6b.Match(i, out isInner));
            Assert.AreEqual("1", hn6c.Match(i, out isInner));
            Assert.AreEqual("1", hn7a.Match(i, out isInner));
            Assert.AreEqual("1", hn7b.Match(i, out isInner));
            Assert.AreEqual("1", hn7c.Match(i, out isInner));

            Assert.AreEqual("c", gc1a.Match(i, out isInner));
            Assert.AreEqual("c", gc1b.Match(i, out isInner));
            Assert.AreEqual("c", gc1c.Match(i, out isInner));
            Assert.AreEqual("c", gc3a.Match(i, out isInner));
            Assert.AreEqual("c", gc3b.Match(i, out isInner));
            Assert.AreEqual("c", gc3c.Match(i, out isInner));

            //Assert.AreEqual("1", hc1.Match(i, out isInner));
            //Assert.AreEqual("1", hc2.Match(i, out isInner));
            //Assert.AreEqual("1", hc3.Match(i, out isInner));
            //Assert.AreEqual("1", hc4.Match(i, out isInner));

            //Assert.AreEqual("1c", gnc1.Match(i, out isInner));
            //Assert.AreEqual("1c", gnc2.Match(i, out isInner));
        }

        [TestMethod]
        public void TestAsterisks() {
            ItemType ITEMTYPE = new ItemType("TEST", new[] { "NAME", }, new[] { "" });
            var g1 = new GraphAbstraction(ITEMTYPE, "(**)", false, IGNORECASE);

            bool isInner;
            Assert.AreEqual("n1", g1.Match(new Item(ITEMTYPE, "n1"), out isInner));
            Assert.AreEqual("n1.n2", g1.Match(new Item(ITEMTYPE, "n1.n2"), out isInner));
            Assert.AreEqual("n1.n2.n3", g1.Match(new Item(ITEMTYPE, "n1.n2.n3"), out isInner));
        }
        //

        [TestMethod]
        public void TestProblemWithTests() {
            ItemType ITEMTYPE = new ItemType("TEST", new[] { "ASSEMBLY", }, new[] { "NAME" });
            var g1 = new GraphAbstraction(ITEMTYPE, "**Tests**", false, IGNORECASE);

            bool isInner;
            Assert.AreEqual("", g1.Match(new Item(ITEMTYPE, "Framework.Core.NunitTests.IBOL"), out isInner));
        }

        [TestMethod]
        public void TestProblemWithEmptyNamespace() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETCALL;
            var @using = new Item(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "", "");
            var used = new Item(itemType, "System", "Object", "mscorlib", "", "", "", "");
            var d = new Dependency(@using, used, null, 0, 0, 0, 0);

            var r = new DependencyRule(itemType, "**", itemType, "System.**", new DependencyRuleRepresentation("rules.dep", 0, "...", false), IGNORECASE);
            Assert.IsTrue(r.IsMatch(d));
        }

        [TestMethod]
        public void TestEmptyNamespaceInRule() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETCALL;

            var @using = new Item(itemType, "NDepCheck.TestAssembly.dir1.dir2", "SomeClass", "NDepCheck.TestAssembly", "1.0.0.0", "", "AnotherMethod", "");
            var used = new Item(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "I", "");
            var d = new Dependency(@using, used, null, 0, 0, 0, 0);

            var r = new DependencyRule(itemType, "NDepCheck.TestAssembly.dir1.dir2:SomeClass:**", itemType, "-:NamespacelessTestClassForNDepCheck::I", new DependencyRuleRepresentation("rules.dep", 0, "...", false), IGNORECASE);
            Assert.IsTrue(r.IsMatch(d));
        }


        [TestMethod]
        public void TestAmpersand() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETCALL;

            var ga = new GraphAbstraction(itemType, "(**):(**):(**)", false, false);

            var used = new Item(itemType, "System", "Byte&", "mscorlib", "4.0.0.0", "", "", "");

            bool isInner;
            string result = ga.Match(used, out isInner);

            Assert.AreEqual("System:Byte&:mscorlib", result);
        }
    }
}
