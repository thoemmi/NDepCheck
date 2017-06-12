using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.PathMatching;

namespace NDepCheck.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class TestPathRegex {
        internal class SimpleTestPathRegex : PathRegex<string, int, Func<string, bool>, Func<int, bool>> {
            public SimpleTestPathRegex(string definition, Dictionary<string, Func<string, bool>> definedItemMatches = null,
                Dictionary<string, Func<int, bool>> definedDependencyMatches = null)
                : base(
                    definition, definedItemMatches: definedItemMatches ?? new Dictionary<string, Func<string, bool>>(),
                    definedDependencyMatches: definedDependencyMatches ?? new Dictionary<string, Func<int, bool>>(),
                    ignoreCase: false) {
            }

            protected override Func<string, bool> CreateItemMatch(string pattern, bool ignoreCase) {
                return item => Regex.IsMatch(item, pattern);
            }

            protected override Func<int, bool> CreateDependencyMatch(string pattern, bool ignoreCase) {
                return dependency => int.Parse(pattern) == dependency;
            }
        }

        [TestMethod]
        public void TestCreateSimpleRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A1B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateStateFromSimpleRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A1B");
            Assert.IsNotNull(regex);
            IBeforeItemGraphkenState<string, int, Func<string, bool>, Func<int, bool>> initState = regex.CreateState();
            Assert.IsTrue(initState.CanContinue);
        }

        [TestMethod]
        public void TestAdvanceSimpleRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A1B");
            IBeforeItemGraphkenState<string, int, Func<string, bool>, Func<int, bool>> initState = regex.CreateState();

            Func<Func<string, bool>, string, bool> itemMatch = (f, i) => f(i);
            bool atEnd, atCount;
            IBeforeDependencyGraphkenState<string, int, Func<string, bool>, Func<int, bool>> stateAfterA =
                initState.Advance("A", itemMatch, out atEnd, out atCount);
            Assert.IsTrue(stateAfterA.CanContinue);
        }

        [TestMethod]
        public void TestAdvanceToEndSimpleRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A1B");
            IBeforeItemGraphkenState<string, int, Func<string, bool>, Func<int, bool>> initState = regex.CreateState();

            Func<Func<string, bool>, string, bool> itemMatch = (f, i) => f(i);
            Func<Func<int, bool>, int, bool> dependencyMatch = (f, d) => f(d);
            bool atEnd, atCount;

            IBeforeDependencyGraphkenState<string, int, Func<string, bool>, Func<int, bool>> stateAfterA =
                initState.Advance("A", itemMatch, out atEnd, out atCount);
            Assert.IsTrue(stateAfterA.CanContinue);
            Assert.IsFalse(atEnd);

            IBeforeItemGraphkenState<string, int, Func<string, bool>, Func<int, bool>> stateAfter1 =
                stateAfterA.Advance(1, dependencyMatch, out atCount);
            Assert.IsTrue(stateAfter1.CanContinue);

            IBeforeDependencyGraphkenState<string, int, Func<string, bool>, Func<int, bool>> stateAfterB =
                stateAfter1.Advance("B", itemMatch, out atEnd, out atCount);
            Assert.IsTrue(stateAfterB.CanContinue);
            Assert.IsTrue(atEnd);
        }

        [TestMethod]
        public void TestCreateLoopRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A(1B)*");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateOneOrMoreRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A(1B)+");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateEmptySequenceRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A()2B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateOptionalRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A(1B)?2C");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateAlternativeRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A|B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateAlternativeWithEmptyDependencyRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A(1B|2C|)");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateAlternativeWithEmptyItemRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("(B1|C2|)D3");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateNestedRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A((1B|(2C(3D)?)*|)+)*");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateSimpleItemCountRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A1(B$2)*C");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateSimpleDependencyCountRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A1(B2$)*C");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateSimpleLongnameRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("{Abc}{123}{Bcd}");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateSingleItemSetRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("[A]1B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateSingleDependencySetRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A[1]B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateItemSetRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("[{Abc}B]2C");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateDependencySetRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("{Abc}[2{34}]C");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateNegativeItemSetRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("[^A]2B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateNegativeDependencySetRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A[^2]B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateAnyItemRegexes() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex(":1:");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestCreateAnyDependencyRegex() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A.B");
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestUsePredefinedItemMatch() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex("A.B",
                new Dictionary<string, Func<string, bool>> { { "A", i => i == "B" } });
            Assert.IsNotNull(regex);
        }

        [TestMethod]
        public void TestUsePredefinedDependencyMatch() {
            SimpleTestPathRegex regex = new SimpleTestPathRegex(":1:",
                definedDependencyMatches: new Dictionary<string, Func<int, bool>> { { "1", i => i == 2 } });
            Assert.IsNotNull(regex);
        }

        private IBeforeDependencyGraphkenState<string, int, Func<string, bool>, Func<int, bool>> AdvanceToEnd(
            string pattern, params string[] objects) {
            return AdvanceToEnd(pattern, "", objects);
        }

        private IBeforeDependencyGraphkenState<string, int, Func<string, bool>, Func<int, bool>> AdvanceToEnd(
            string pattern, string msg, string[] objects) {
            SimpleTestPathRegex regex = new SimpleTestPathRegex(pattern);

            //Console.WriteLine(regex.GetGraphkenRepresentation());

            IBeforeItemGraphkenState<string, int, Func<string, bool>, Func<int, bool>> beforeItemState =
                regex.CreateState();
            Assert.IsTrue(beforeItemState.CanContinue);

            Func<Func<string, bool>, string, bool> itemMatch = (f, i) => f(i);
            Func<Func<int, bool>, int, bool> dependencyMatch = (f, d) => f(d);

            for (int i = 0; ; i++) {
                bool atCount;
                IBeforeDependencyGraphkenState<string, int, Func<string, bool>, Func<int, bool>> beforeDependencyState;
                {
                    string item = objects[i];
                    bool atEnd;
                    beforeDependencyState = beforeItemState.Advance(item.TrimStart('+', '#', '$'), itemMatch, out atEnd,
                        out atCount);
                    Assert.AreNotEqual(item.Contains("#"), beforeDependencyState.CanContinue,
                        "CanContinue wrong for " + item + msg);
                    Assert.AreEqual(item.Contains("-"), atEnd, "AtEnd wrong for " + item + msg);
                    Assert.AreEqual(item.Contains("$"), atCount, "AtCount wrong for " + item + msg);
                }
                if (++i >= objects.Length) {
                    return beforeDependencyState;
                }
                {
                    string dependency = objects[i];
                    beforeItemState = beforeDependencyState.Advance(int.Parse(dependency.TrimStart('+', '#', '$')),
                        dependencyMatch, out atCount);
                    Assert.AreNotEqual(dependency.Contains("#"), beforeItemState.CanContinue,
                        "CanContinue wrong for " + dependency + msg);
                    Assert.AreEqual(dependency.Contains("$"), atCount, "AtCount wrong for " + dependency + msg);
                }
            }
        }

        [TestMethod]
        public void TestAdvanceZeroOrMore() {
            var result = AdvanceToEnd(":(.:)*", "-a", "1", "-b", "2", "-c");
            Assert.IsFalse(result.CountedObjects.Any());
        }

        [TestMethod]
        public void TestAdvanceOneOrMore() {
            IGraphkenState result = AdvanceToEnd(":(.:)+", "a", "1", "-b", "2", "-c");
            Assert.IsFalse(result.CountedObjects.Any());
        }

        [TestMethod]
        public void TestAdvanceIndeterministicAlternative() {
            IGraphkenState result = AdvanceToEnd(":(.b|.c)", "a", "1", "-b");
            Assert.IsFalse(result.CountedObjects.Any());
        }

        [TestMethod]
        public void TestAdvanceIndeterministicAlternativeFails() {
            IGraphkenState result = AdvanceToEnd(":(.b|.c)", "a", "1", "#d");
            Assert.IsFalse(result.CountedObjects.Any());
        }

        [TestMethod]
        public void TestAdvanceAlternativeWithOneSuccess() {
            const string oneOrTwoBs = "a(.b.b|.b).c";
            {
                IGraphkenState result = AdvanceToEnd(oneOrTwoBs, "a", "1", "b", "1", "-c");
                Assert.IsFalse(result.CountedObjects.Any());
            }
            {
                IGraphkenState result = AdvanceToEnd(oneOrTwoBs, "a", "1", "b", "1", "b", "1", "-c");
                Assert.IsFalse(result.CountedObjects.Any());
            }
            {
                IGraphkenState result = AdvanceToEnd(oneOrTwoBs, "a", "1", "b", "1", "b", "1", "#b");
                Assert.IsFalse(result.CountedObjects.Any());
            }
        }


        [TestMethod]
        public void TestAdvanceMultiplesOfTwoAndThree() {
            const string bsInPairsOrTriples = "a((.b.b.b)+|(.b.b)+).c";
            for (int i = 1; i < 40; i++) {
                string[] path = new[] { "a" }
                        .Concat(Enumerable.Range(0, i).Select(_ => new[] { "1", "b" }).SelectMany(s => s))
                        .Concat(i % 2 == 0 || i % 3 == 0 ? new[] { "2", "-c" } : new[] { "2", "#c" })
                        .ToArray();
                IGraphkenState result = AdvanceToEnd(bsInPairsOrTriples, " (for i=" + i + ")", path);
                Assert.IsFalse(result.CountedObjects.Any());
            }
        }
    }
}
