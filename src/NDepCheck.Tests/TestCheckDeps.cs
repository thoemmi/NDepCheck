using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.ViolationChecking;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestCheckDeps {
        [TestMethod]
        public void TestRuleWithParenthesesAndBarsWorksAgain() {
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var deps = new[] {new Dependency(a, b, null, "", 1)};
            var ignore = new List<Dependency>();

            GlobalContext globalContext = new GlobalContext();
            var cd = new CheckDeps();
            cd.Configure(globalContext, "{ -rd $SIMPLE--->SIMPLE a--->(a|b) }".Replace(" ", "\r\n"), true);
            cd.Transform(globalContext, deps, "", ignore);

            Assert.IsFalse(deps.Any(d => d.BadCt > 0));
        }

        [TestMethod]
        public void TestIndirectRulesOverTwoLevels() {
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var deps = new[] {new Dependency(a, b, null, "", 1)};

            var cd = new CheckDeps();
            GlobalContext globalContext = new GlobalContext();
            cd.Configure(globalContext, @"{
    -rd 
    $SIMPLE ---> SIMPLE 
    x ---> b
    y ===> x
    a ===> y
}", true);
            var ignore = new List<Dependency>();
            cd.Transform(globalContext, deps, "", ignore);

            Assert.IsFalse(deps.Any(d => d.BadCt > 0));

        }
    }
}