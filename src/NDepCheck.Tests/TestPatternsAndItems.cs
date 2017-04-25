using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Reading;
using NDepCheck.Transforming.Projecting;
using NDepCheck.Transforming.ViolationChecking;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestPatternsAndItems {
        private const bool IGNORECASE = false;

        [TestMethod]
        public void TestSimpleDependencyRuleMatches() {
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation("FILE", 0, "...", false);
            ItemType itemType = ItemType.New("NC", new[] { "NAMESPACE", "CLASS" }, new[] { "", "" }, ignoreCase: false);

            var r1 = new DependencyRule(itemType, ":", itemType, ":", rep, IGNORECASE);

            var rn1 = new DependencyRule(itemType, "n*", itemType, ":", rep, IGNORECASE);
            var rn2 = new DependencyRule(itemType, "n*:", itemType, ":", rep, IGNORECASE);
            var rn3 = new DependencyRule(itemType, "n**", itemType, ":", rep, IGNORECASE);
            var rn4 = new DependencyRule(itemType, "n**:", itemType, ":", rep, IGNORECASE);
            var rn5 = new DependencyRule(itemType, "n1", itemType, ":", rep, IGNORECASE);
            var rn6 = new DependencyRule(itemType, "n1:", itemType, ":", rep, IGNORECASE);
            var rn7 = new DependencyRule(itemType, "*n1:", itemType, ":", rep, IGNORECASE);
            var rn8 = new DependencyRule(itemType, "**n1:", itemType, ":", rep, IGNORECASE);

            var rc1 = new DependencyRule(itemType, ":c*", itemType, ":", rep, IGNORECASE);
            var rc2 = new DependencyRule(itemType, ":c**", itemType, ":", rep, IGNORECASE);
            var rc3 = new DependencyRule(itemType, ":*c1", itemType, ":", rep, IGNORECASE);
            var rc4 = new DependencyRule(itemType, ":**c1", itemType, ":", rep, IGNORECASE);

            var rnc1 = new DependencyRule(itemType, "n*:c*", itemType, ":", rep, IGNORECASE);
            var rnc2 = new DependencyRule(itemType, "n**:c**", itemType, ":", rep, IGNORECASE);

            Dependency dep = new Dependency(Item.New(itemType, "n1", "c1"), Item.New(itemType, "n2", "c2"), null, "Test", ct: 1);
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
            ItemType itemtype = ItemType.New("SO", new[] { "SCHEMA", "OBJECT" }, new[] { "", "" }, ignoreCase: false);
            var rn1 = new DependencyRule(itemtype, "(s)*", itemtype, @"\1*", rep, IGNORECASE);
            var rn2 = new DependencyRule(itemtype, "(s)*:(t)*", itemtype, @"\1*:\2*", rep, IGNORECASE);
            var rn3 = new DependencyRule(itemtype, "(s)**:(t)**", itemtype, @"\1*:\2*", rep, IGNORECASE);
            var rn4 = new DependencyRule(itemtype, "s(*):t(*)", itemtype, @"s\1:t\2", rep, IGNORECASE);
            var rn5 = new DependencyRule(itemtype, "s(**):t(**)", itemtype, @"s\1:t\2", rep, IGNORECASE);

            Dependency dep = new Dependency(Item.New(itemtype, "s1", "t1"), Item.New(itemtype, "s2", "t2"), null, "Test", ct: 1);
            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsFalse(rn4.IsMatch(dep));
            Assert.IsFalse(rn5.IsMatch(dep));
        }

        [TestMethod]
        public void TestBackReferenceDependencyRuleMatchesWithOuterParentheses() {
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation("FILE", 0, "...", false);
            ItemType itemtype = ItemType.New("SO", new[] { "SCHEMA", "OBJECT" }, new[] { "", "" }, ignoreCase: false);
            var rn3 = new DependencyRule(itemtype, "(s**):(t**)", itemtype, @"\1*:\2*", rep, IGNORECASE);
            var rn5 = new DependencyRule(itemtype, "(**s**):(**t**)", itemtype, @"\1:\2", rep, IGNORECASE);

            Dependency dep = new Dependency(Item.New(itemtype, "sx", "tx"), Item.New(itemtype, "sx", "tx"), null, "Test", ct: 1);
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsTrue(rn5.IsMatch(dep));
        }

        [TestMethod]
        public void TestMoreBackReferenceDependencyRuleMatches() {
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation("FILE", 0, "...", false);
            ItemType itemType = ItemType.New("SO", new[] { "SCHEMA", "OBJECT" }, new[] { "", "" }, ignoreCase: false);
            var rn1 = new DependencyRule(itemType, "(s)*", itemType, @"\1*", rep, IGNORECASE);
            var rn2 = new DependencyRule(itemType, "(s*)", itemType, @"\1", rep, IGNORECASE);
            var rn3 = new DependencyRule(itemType, "s(*)", itemType, @"s\1", rep, IGNORECASE);
            var rn4 = new DependencyRule(itemType, "s(*):(t)*", itemType, @"s\1:\2*", rep, IGNORECASE);
            var rn5 = new DependencyRule(itemType, "s*:t(*)", itemType, @"s\1:t*", rep, IGNORECASE);

            var rn6 = new DependencyRule(itemType, "s*:(t*)", itemType, @"s\1:t*", rep, IGNORECASE);

            Dependency dep = new Dependency(Item.New(itemType, "s1", "t1"), Item.New(itemType, "s1", "t2"), null, "Test", ct: 1);
            Assert.IsTrue(rn1.IsMatch(dep));
            Assert.IsTrue(rn2.IsMatch(dep));
            Assert.IsTrue(rn3.IsMatch(dep));
            Assert.IsTrue(rn4.IsMatch(dep));
            Assert.IsTrue(rn5.IsMatch(dep));
            Assert.IsFalse(rn6.IsMatch(dep));
        }

        [TestMethod]
        public void TestSimpleGraphAbstractionMatches() {
            ItemType testType = ItemType.New("NC", new[] { "NAMESPACE", "CLASS" }, new[] { "", "" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "NAME", }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "**():", null, IGNORECASE, true, true);

            var gn1 = new Projection(testType, simpleType, "(n)*", null, IGNORECASE, true, true);
            var gn2 = new Projection(testType, simpleType, "(n)*:", null, IGNORECASE, true, true);
            var gn3 = new Projection(testType, simpleType, "(n)**", null, IGNORECASE, true, true);
            var gn4 = new Projection(testType, simpleType, "(n)**:", null, IGNORECASE, true, true);
            var gn5 = new Projection(testType, simpleType, "(n)1", null, IGNORECASE, true, true);
            var gn6 = new Projection(testType, simpleType, "(n)1:", null, IGNORECASE, true, true);
            var gn7 = new Projection(testType, simpleType, "(*n)1:", null, IGNORECASE, true, true);
            var gn8 = new Projection(testType, simpleType, "**(n)1:", null, IGNORECASE, true, true);

            var hn1 = new Projection(testType, simpleType, "n(*)", null, IGNORECASE, true, true);
            var hn2 = new Projection(testType, simpleType, "n(*):", null, IGNORECASE, true, true);
            var hn3 = new Projection(testType, simpleType, "n(**)", null, IGNORECASE, true, true);
            var hn4 = new Projection(testType, simpleType, "n(**):", null, IGNORECASE, true, true);
            var hn5 = new Projection(testType, simpleType, "n(1)", null, IGNORECASE, true, true);
            var hn6 = new Projection(testType, simpleType, "n(1):", null, IGNORECASE, true, true);
            var hn7 = new Projection(testType, simpleType, "*n(1):", null, IGNORECASE, true, true);
            var hn8 = new Projection(testType, simpleType, "**n(1):", null, IGNORECASE, true, true);

            var gc1 = new Projection(testType, simpleType, ":(c)*", null, IGNORECASE, true, true);
            var gc2 = new Projection(testType, simpleType, ":(c)**", null, IGNORECASE, true, true);
            var gc3 = new Projection(testType, simpleType, ":(*c)1", null, IGNORECASE, true, true);
            var gc4 = new Projection(testType, simpleType, ":(**c)1", null, IGNORECASE, true, true);

            var hc1 = new Projection(testType, simpleType, ":c(*)", null, IGNORECASE, true, true);
            var hc2 = new Projection(testType, simpleType, ":c(*)*", null, IGNORECASE, true, true);
            var hc3 = new Projection(testType, simpleType, ":*c(1)", null, IGNORECASE, true, true);
            var hc4 = new Projection(testType, simpleType, ":**c(1)", null, IGNORECASE, true, true);

            var gnc1 = new Projection(testType, simpleType, "n(*):(c)*", new[] { "\\1+\\2" }, IGNORECASE, true, true);
            var gnc2 = new Projection(testType, simpleType, "n(**):(c)**", new[] { "\\1+\\2" }, IGNORECASE, true, true);

            Item i = Item.New(testType, "n1", "c1");

            Assert.AreEqual("", g1.Match(i, true).Name);

            Assert.AreEqual("n", gn1.Match(i, true).Name);
            Assert.AreEqual("n", gn2.Match(i, true).Name);
            Assert.AreEqual("n", gn3.Match(i, true).Name);
            Assert.AreEqual("n", gn4.Match(i, true).Name);
            Assert.AreEqual("n", gn5.Match(i, true).Name);
            Assert.AreEqual("n", gn6.Match(i, true).Name);
            Assert.AreEqual("n", gn7.Match(i, true).Name);
            Assert.AreEqual("n", gn8.Match(i, true).Name);

            Assert.AreEqual("1", hn1.Match(i, true).Name);
            Assert.AreEqual("1", hn2.Match(i, true).Name);
            Assert.AreEqual("1", hn3.Match(i, true).Name);
            Assert.AreEqual("1", hn4.Match(i, true).Name);
            Assert.AreEqual("1", hn5.Match(i, true).Name);
            Assert.AreEqual("1", hn6.Match(i, true).Name);
            Assert.AreEqual("1", hn7.Match(i, true).Name);
            Assert.AreEqual("1", hn8.Match(i, true).Name);

            Assert.AreEqual("c", gc1.Match(i, true).Name);
            Assert.AreEqual("c", gc2.Match(i, true).Name);
            Assert.AreEqual("c", gc3.Match(i, true).Name);
            Assert.AreEqual("c", gc4.Match(i, true).Name);

            Assert.AreEqual("1", hc1.Match(i, true).Name);
            Assert.AreEqual("1", hc2.Match(i, true).Name);
            Assert.AreEqual("1", hc3.Match(i, true).Name);
            Assert.AreEqual("1", hc4.Match(i, true).Name);

            Assert.AreEqual("1+c", gnc1.Match(i, true).Name);
            Assert.AreEqual("1+c", gnc2.Match(i, true).Name);
        }

        [TestMethod]
        public void TestRegexGraphAbstractionMatches() {
            ItemType testType = ItemType.New("NC", new[] { "NAMESPACE", "CLASS" }, new[] { "", "" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "NAME" }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "^.*()$:", null, IGNORECASE, true, true);

            // ReSharper disable InconsistentNaming
            var gn1a = new Projection(testType, simpleType, "^(n).*", null, IGNORECASE, true, true);
            var gn1b = new Projection(testType, simpleType, "^(n).*$", null, IGNORECASE, true, true);
            var gn1c = new Projection(testType, simpleType, "(n).*$", null, IGNORECASE, true, true);
            var gn2a = new Projection(testType, simpleType, "^(n).*:", null, IGNORECASE, true, true);
            var gn2b = new Projection(testType, simpleType, "^(n).*$:", null, IGNORECASE, true, true);
            var gn2c = new Projection(testType, simpleType, "(n).*$:", null, IGNORECASE, true, true);
            var gn5a = new Projection(testType, simpleType, "^(n)1", null, IGNORECASE, true, true);
            var gn5b = new Projection(testType, simpleType, "^(n)1$", null, IGNORECASE, true, true);
            var gn5c = new Projection(testType, simpleType, "(n)1$", null, IGNORECASE, true, true);
            var gn6a = new Projection(testType, simpleType, "^(n)1:", null, IGNORECASE, true, true);
            var gn6b = new Projection(testType, simpleType, "^(n)1$:", null, IGNORECASE, true, true);
            var gn6c = new Projection(testType, simpleType, "(n)1$:", null, IGNORECASE, true, true);
            var gn7a = new Projection(testType, simpleType, "^(.*n)1:", null, IGNORECASE, true, true);
            var gn7b = new Projection(testType, simpleType, "^(.*n)1$:", null, IGNORECASE, true, true);
            var gn7c = new Projection(testType, simpleType, "(.*n)1$:", null, IGNORECASE, true, true);

            var hn1a = new Projection(testType, simpleType, "^n(.*)", null, IGNORECASE, true, true);
            var hn1b = new Projection(testType, simpleType, "^n(.*)$", null, IGNORECASE, true, true);
            var hn1c = new Projection(testType, simpleType, "n(.*)$", null, IGNORECASE, true, true);
            var hn2a = new Projection(testType, simpleType, "^n(.*):", null, IGNORECASE, true, true);
            var hn2b = new Projection(testType, simpleType, "^n(.*)$:", null, IGNORECASE, true, true);
            var hn2c = new Projection(testType, simpleType, "n(.*)$:", null, IGNORECASE, true, true);
            var hn5a = new Projection(testType, simpleType, "^n(1)", null, IGNORECASE, true, true);
            var hn5b = new Projection(testType, simpleType, "^n(1)$", null, IGNORECASE, true, true);
            var hn5c = new Projection(testType, simpleType, "n(1)$", null, IGNORECASE, true, true);
            var hn6a = new Projection(testType, simpleType, "^n(1):", null, IGNORECASE, true, true);
            var hn6b = new Projection(testType, simpleType, "^n(1)$:", null, IGNORECASE, true, true);
            var hn6c = new Projection(testType, simpleType, "n(1)$:", null, IGNORECASE, true, true);
            var hn7a = new Projection(testType, simpleType, "^.*n(1):", null, IGNORECASE, true, true);
            var hn7b = new Projection(testType, simpleType, "^.*n(1)$:", null, IGNORECASE, true, true);
            var hn7c = new Projection(testType, simpleType, ".*n(1)$:", null, IGNORECASE, true, true);

            var gc1a = new Projection(testType, simpleType, ":^(c).*", null, IGNORECASE, true, true);
            var gc1b = new Projection(testType, simpleType, ":^(c).*$", null, IGNORECASE, true, true);
            var gc1c = new Projection(testType, simpleType, ":(c).*$", null, IGNORECASE, true, true);
            var gc3a = new Projection(testType, simpleType, ":^(.*c)1", null, IGNORECASE, true, true);
            var gc3b = new Projection(testType, simpleType, ":^(.*c)1$", null, IGNORECASE, true, true);
            var gc3c = new Projection(testType, simpleType, ":(.*c)1$", null, IGNORECASE, true, true);
            // ReSharper restore InconsistentNaming

            //var hc1 = new GraphAbstraction_(":c(*)", false);
            //var hc2 = new GraphAbstraction_(":c(*)*", false);
            //var hc3 = new GraphAbstraction_(":*c(1)", false);
            //var hc4 = new GraphAbstraction_(":**c(1)", false);

            //var gnc1 = new GraphAbstraction_("n(*):(c)*", false);
            //var gnc2 = new GraphAbstraction_("n(**):(c)**", false);

            Item i = Item.New(testType, "n1", "c1");

            Assert.AreEqual("", g1.Match(i, true).Name);

            Assert.AreEqual("n", gn1a.Match(i, true).Name);
            Assert.AreEqual("n", gn1b.Match(i, true).Name);
            Assert.AreEqual("n", gn1c.Match(i, true).Name);
            Assert.AreEqual("n", gn2a.Match(i, true).Name);
            Assert.AreEqual("n", gn2b.Match(i, true).Name);
            Assert.AreEqual("n", gn2c.Match(i, true).Name);
            Assert.AreEqual("n", gn5a.Match(i, true).Name);
            Assert.AreEqual("n", gn5b.Match(i, true).Name);
            Assert.AreEqual("n", gn5c.Match(i, true).Name);
            Assert.AreEqual("n", gn6a.Match(i, true).Name);
            Assert.AreEqual("n", gn6b.Match(i, true).Name);
            Assert.AreEqual("n", gn6c.Match(i, true).Name);
            Assert.AreEqual("n", gn7a.Match(i, true).Name);
            Assert.AreEqual("n", gn7b.Match(i, true).Name);
            Assert.AreEqual("n", gn7c.Match(i, true).Name);

            Assert.AreEqual("1", hn1a.Match(i, true).Name);
            Assert.AreEqual("1", hn1b.Match(i, true).Name);
            Assert.AreEqual("1", hn1c.Match(i, true).Name);
            Assert.AreEqual("1", hn2a.Match(i, true).Name);
            Assert.AreEqual("1", hn2b.Match(i, true).Name);
            Assert.AreEqual("1", hn2c.Match(i, true).Name);
            Assert.AreEqual("1", hn5a.Match(i, true).Name);
            Assert.AreEqual("1", hn5b.Match(i, true).Name);
            Assert.AreEqual("1", hn5c.Match(i, true).Name);
            Assert.AreEqual("1", hn6a.Match(i, true).Name);
            Assert.AreEqual("1", hn6b.Match(i, true).Name);
            Assert.AreEqual("1", hn6c.Match(i, true).Name);
            Assert.AreEqual("1", hn7a.Match(i, true).Name);
            Assert.AreEqual("1", hn7b.Match(i, true).Name);
            Assert.AreEqual("1", hn7c.Match(i, true).Name);

            Assert.AreEqual("c", gc1a.Match(i, true).Name);
            Assert.AreEqual("c", gc1b.Match(i, true).Name);
            Assert.AreEqual("c", gc1c.Match(i, true).Name);
            Assert.AreEqual("c", gc3a.Match(i, true).Name);
            Assert.AreEqual("c", gc3b.Match(i, true).Name);
            Assert.AreEqual("c", gc3c.Match(i, true).Name);

            //Assert.AreEqual("1", hc1.Match(i, true).Name);
            //Assert.AreEqual("1", hc2.Match(i, true).Name);
            //Assert.AreEqual("1", hc3.Match(i, true).Name);
            //Assert.AreEqual("1", hc4.Match(i, true).Name);

            //Assert.AreEqual("1c", gnc1.Match(i, true).Name);
            //Assert.AreEqual("1c", gnc2.Match(i, true).Name);
        }

        [TestMethod]
        public void TestAsterisks() {
            ItemType testType = ItemType.New("T", new[] { "NAME", }, new[] { "" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "NAME", }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "(**)", null, IGNORECASE, true, true);

            Assert.AreEqual("n1", g1.Match(Item.New(testType, "n1"), true).Name);
            Assert.AreEqual("n1.n2", g1.Match(Item.New(testType, "n1.n2"), true).Name);
            Assert.AreEqual("n1.n2.n3", g1.Match(Item.New(testType, "n1.n2.n3"), true).Name);
        }
        //

        [TestMethod]
        public void TestProblemWithTests() {
            ItemType testType = ItemType.New("A", new[] { "ASSEMBLY", }, new[] { "NAME" }, ignoreCase: false);
            ItemType simpleType = ItemType.New("N", new[] { "NAME", }, new[] { "" }, ignoreCase: false);
            var g1 = new Projection(testType, simpleType, "**Tests**", null, IGNORECASE, true, true);
            var g2 = new Projection(testType, simpleType, "**Tests**()", null, IGNORECASE, true, true);

            Assert.AreEqual("\\1", g1.Match(Item.New(testType, "Framework.Core.NunitTests.IBOL"), true).Name);
            Assert.AreEqual("", g2.Match(Item.New(testType, "Framework.Core.NunitTests.IBOL"), true).Name);
        }

        [TestMethod]
        public void TestProblemWithEmptyNamespace() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETCALL;
            var @using = Item.New(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "", "");
            var used = Item.New(itemType, "System", "Object", "mscorlib", "", "", "", "");
            var d = new Dependency(@using, used, null, "Test", ct: 1);

            var r = new DependencyRule(itemType, "**", itemType, "System.**", new DependencyRuleRepresentation("rules.dep", 0, "...", false), IGNORECASE);
            Assert.IsTrue(r.IsMatch(d));
        }

        [TestMethod]
        public void TestEmptyNamespaceInRule() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETCALL;

            Item @using = Item.New(itemType, "NDepCheck.TestAssembly.dir1.dir2", "SomeClass", "NDepCheck.TestAssembly", "1.0.0.0", "", "AnotherMethod", "");
            Item used = Item.New(itemType, "", "NamespacelessTestClassForNDepCheck", "NDepCheck.TestAssembly", "1.0.0.0", "", "I", "");
            var d = new Dependency(@using, used, null, "Test", ct: 1);

            var r = new DependencyRule(itemType, "NDepCheck.TestAssembly.dir1.dir2:SomeClass:**", itemType, "-:NamespacelessTestClassForNDepCheck::I", new DependencyRuleRepresentation("rules.dep", 0, "...", false), IGNORECASE);
            Assert.IsTrue(r.IsMatch(d));
        }


        [TestMethod]
        public void TestAmpersand() {
            ItemType itemType = DotNetAssemblyDependencyReaderFactory.DOTNETCALL;

            var ga = new Projection(itemType, itemType, "(**):(**):(**)", new[] { "\\1", "\\2", "\\3", "", "", "", "" },
                                    ignoreCase: false, forLeftSide: true, forRightSide: true);

            Item used = Item.New(itemType, "System", "Byte&", "mscorlib", "4.0.0.0", "", "", "");

            string result = ga.Match(used, true).Name;

            Assert.AreEqual("System:Byte&:mscorlib;;:;", result);
        }
    }
}
