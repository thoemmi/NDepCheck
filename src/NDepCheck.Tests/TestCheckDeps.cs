using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.ViolationChecking;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestCheckDeps {
        [TestMethod]
        public void TestRuleWithParenthesesAndBarsWorksAgain() {
            GlobalContext gc = new GlobalContext();

            var env = gc.CurrentGraph;
            var a = env.CreateItem(ItemType.SIMPLE, "a");
            var b = env.CreateItem(ItemType.SIMPLE, "b");
            var deps = new[] { env.CreateDependency(a, b, null, "", 1) };
            var ignore = new List<Dependency>();

            var cd = new CheckDeps();
            cd.Configure(gc, "{ -rd $SIMPLE--->SIMPLE a--->(a|b) }".Replace(" ", "\r\n"), true);
            cd.Transform(gc, deps, "", ignore, s => null);

            Assert.IsFalse(deps.Any(d => d.BadCt > 0));
        }

        [TestMethod]
        public void TestIndirectRulesOverTwoLevels() {
            GlobalContext gc = new GlobalContext();

            var env = gc.CurrentGraph;
            var a = env.CreateItem(ItemType.SIMPLE, "a");
            var b = env.CreateItem(ItemType.SIMPLE, "b");
            var deps = new[] { env.CreateDependency(a, b, null, "", 1) };

            var cd = new CheckDeps();
            cd.Configure(gc, @"{
    -rd 
    $SIMPLE ---> SIMPLE 
    x ---> b
    y ===> x
    a ===> y
}", true);
            var ignore = new List<Dependency>();
            cd.Transform(gc, deps, "", ignore, s => null);

            Assert.IsFalse(deps.Any(d => d.BadCt > 0));

        }
    }
}