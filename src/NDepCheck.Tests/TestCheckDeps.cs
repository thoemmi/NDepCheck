using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.CycleChecking;
using NDepCheck.Transforming.ViolationChecking;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestCheckDeps {
        [TestMethod]
        public void TestRuleWithParenthesesAndBarsWorksAgain() {
            var a = Item.New(ItemType.SIMPLE, "a");
            var b = Item.New(ItemType.SIMPLE, "b");
            var deps = new[] { new Dependency(a, b, null, "", 1) };
            var result = new List<Dependency>();

            GlobalContext globalContext = new GlobalContext();
            var cd = new CheckDeps();
            cd.Configure(globalContext, "{ -rd $SIMPLE--->SIMPLE a--->(a|b) }".Replace(" ", "\r\n"), true);
            cd.Transform(globalContext, "test", deps, "", "test", result);

            Assert.IsFalse(deps.Any(d => d.BadCt > 0));
        }
    }
}
