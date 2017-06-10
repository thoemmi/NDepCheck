using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NDepCheck.Matching;

namespace NDepCheck.PathRegex {
    public class PathRegex {
        public class RegexSyntaxException : Exception {
            public string S { get; }

            public RegexSyntaxException(string s, int pos, string message) : base(message) {
                S = s.Substring(pos);
            }
        }

        public struct ChainingInfo {
            public readonly bool StartsWithItemMatch;
            public readonly bool EndsWithItemMatch;
            public readonly bool CanBeEmpty;

            public ChainingInfo(bool startsWithItemMatch, bool endsWithItemMatch, bool canBeEmpty) {
                StartsWithItemMatch = startsWithItemMatch;
                EndsWithItemMatch = endsWithItemMatch;
                CanBeEmpty = canBeEmpty;
            }
        }

        private abstract class RegexElement {
            public int TextPos { get; }

            protected RegexElement(int textPos) {
                TextPos = textPos;
            }
        }

        private abstract class ItemMatchAndMoreRegex : RegexElement {
            protected ItemMatchAndMoreRegex(int textPos) : base(textPos) {
            }
        }

        private abstract class DependencyMatchAndMoreRegex : RegexElement {
            protected DependencyMatchAndMoreRegex(int textPos) : base(textPos) {
            }
        }

        private class ItemMatchAndMore : RegexElement {
            public ItemMatchAndMore(int textPos) : base(textPos) {
            }
        }

        private class AnyDependency : RegexElement {
            public AnyDependency(int textPos) : base(textPos) {
            }
        }

        private abstract class SetElement<TPattern> : RegexElement {
            protected SetElement(int textPos, bool invert, IEnumerable<TPattern> patterns) : base(textPos) {
                Invert = invert;
                Patterns = patterns;
            }

            public bool Invert { get; }
            public IEnumerable<TPattern> Patterns { get; }
        }

        private class ItemSet : SetElement<ItemMatch> {
            public ItemSet(int textPos, bool invert, IEnumerable<ItemMatch> patterns) : base(textPos, invert, patterns) {
            }

            public override ChainingInfo Validate() {
                return new ChainingInfo(startsWithItemMatch: true, endsWithItemMatch: true, canBeEmpty: false);
            }
        }

        private class DependencySet : SetElement<DependencyMatch> {
            public DependencySet(int textPos, bool invert, IEnumerable<DependencyMatch> patterns)
                : base(textPos, invert, patterns) {
            }

            public override ChainingInfo Validate() {
                return new ChainingInfo(startsWithItemMatch: false, endsWithItemMatch: false, canBeEmpty: false);
            }
        }

        private 





        private abstract class Sequence : RegexElement {
            public Sequence(int textPos, IEnumerable<RegexElement> elements)
                : base(textPos) {
                if (!elements.Any()) {
                    throw new ArgumentException("Sequence must contain at least one element", nameof(elements));
                }
                Elements = elements;
            }

            public IEnumerable<RegexElement> Elements { get; }

            public ChainingInfo[] ValidateSequence() {
                ChainingInfo[] childrenChainingInfos = Elements.Select(e => e.Validate()).ToArray();
                int n = childrenChainingInfos.Length - 1;

                for (int i = 0; i < n; i++) {
                    ChainingInfo info = childrenChainingInfos[i];
                    if (info.CanBeEmpty) {
                        if (info.StartsWithItemMatch !=
                            childrenChainingInfos[i + 1].StartsWithItemMatch) {
                            throw new RegexValidationException(Elements.ElementAt(i), Elements.ElementAt(i + 1),
                                "Element after possibly empty element must also start with " + (info.EndsWithItemMatch ? "item" : "dependency"));
                        }
                    }

                    for (int j = i + 1; j <= n && (j - 1 == i || childrenChainingInfos[j - 1].CanBeEmpty); j++) {
                        if (info.EndsWithItemMatch == childrenChainingInfos[j].StartsWithItemMatch) {
                            throw new RegexValidationException(Elements.ElementAt(i), Elements.ElementAt(j),
                                "Missing " + (info.EndsWithItemMatch ? "dependency" : "item") +
                                "between possible adjacent patterns");
                        }
                    }
                }

                return childrenChainingInfos;

            }
        }

        private class Subsequence : Sequence {
            public Subsequence(int textPos, IEnumerable<RegexElement> elements) : base(textPos, elements) {
            }
            public override ChainingInfo Validate() {
                ChainingInfo[] childrenChainingInfos = ValidateSequence();
                return new ChainingInfo(startsWithItemMatch: childrenChainingInfos.First().StartsWithItemMatch,
                    endsWithItemMatch: childrenChainingInfos.Last().EndsWithItemMatch,
                    canBeEmpty: false);
            }
        }

        private class Optional : Sequence {
            public Optional(int textPos, IEnumerable<RegexElement> elements) : base(textPos, elements) {
            }
            public override ChainingInfo Validate() {
                ChainingInfo[] childrenChainingInfos = ValidateSequence();
                return new ChainingInfo(startsWithItemMatch: childrenChainingInfos.First().StartsWithItemMatch,
                    endsWithItemMatch: childrenChainingInfos.Last().EndsWithItemMatch,
                    canBeEmpty: true);
            }
        }

        private abstract class Multiple : Sequence {
            public Multiple(int textPos, IEnumerable<RegexElement> elements) : base(textPos, elements) {
            }

            public ChainingInfo[] ValidateMultiple() {
                ChainingInfo[] childrenChainingInfos = ValidateSequence();
                ChainingInfo first = childrenChainingInfos.First();
                if (childrenChainingInfos.Last().EndsWithItemMatch == first.StartsWithItemMatch) {
                    throw new RegexValidationException(Elements.Last(), Elements.First(),
                        "Repeated sequence ends and starts with " + (first.StartsWithItemMatch ? "item" : "dependency") +
                        " pattern");
                }
                return childrenChainingInfos;
            }
        }

        private class ZeroOrMore : Multiple {
            public ZeroOrMore(int textPos, IEnumerable<RegexElement> elements) : base(textPos, elements) {
            }

            public override ChainingInfo Validate() {
                ChainingInfo[] childrenChainingInfos = ValidateMultiple();
                return new ChainingInfo(startsWithItemMatch: childrenChainingInfos.First().StartsWithItemMatch,
                    endsWithItemMatch: childrenChainingInfos.Last().EndsWithItemMatch,
                    canBeEmpty: true);
            }
        }

        private class OneOrMore : Multiple {
            public OneOrMore(int textPos, IEnumerable<RegexElement> elements) : base(textPos, elements) {
            }
            public override ChainingInfo Validate() {
                ChainingInfo[] childrenChainingInfos = ValidateMultiple();
                return new ChainingInfo(startsWithItemMatch: childrenChainingInfos.First().StartsWithItemMatch,
                    endsWithItemMatch: childrenChainingInfos.Last().EndsWithItemMatch,
                    canBeEmpty: childrenChainingInfos.All(c => c.CanBeEmpty));
            }
        }

        private class RegexValidationException : Exception {
            public RegexElement First { get; }
            public RegexElement Second { get; }

            public RegexValidationException(RegexElement first, RegexElement second, string message) : base(message) {
                First = first;
                Second = second;
            }
        }

        private class ItemMatchNode {
            public readonly Dictionary<DependencyMatch, DependencyMatchNode> Follow =
                new Dictionary<DependencyMatch, DependencyMatchNode>();

            public readonly bool IsEnd;

            public ItemMatchNode(bool isEnd) {
                IsEnd = isEnd;
            }
        }

        private class DependencyMatchNode {
            public readonly Dictionary<ItemMatch, ItemMatchNode> Follow = new Dictionary<ItemMatch, ItemMatchNode>();
        }

        private readonly Dictionary<string, ItemMatch> _definedItemMatches;
        private readonly Dictionary<string, DependencyMatch> _definedDependencyMatches;
        private readonly bool _ignoreCase;

        private readonly ItemMatchNode _rootExecutionNode;

        public PathRegex(string s, Dictionary<string, ItemMatch> definedItemMatches,
            Dictionary<string, DependencyMatch> definedDependencyMatches, bool ignoreCase) {
            _definedItemMatches = definedItemMatches;
            _definedDependencyMatches = definedDependencyMatches;
            _ignoreCase = ignoreCase;
            RegexElement e = Parse(s, 0);
            ChainingInfo topChainingInfo = e.Validate();
            if (!topChainingInfo.StartsWithItemMatch) {
                throw new RegexValidationException(e, null, "Path regex must start with item match");
            }
            if (!topChainingInfo.EndsWithItemMatch) {
                throw new RegexValidationException(e, null, "Path regex must end with item match");
            }
            _rootExecutionNode = e.CreateDeterministicExecutionGraph();
        }

        private static readonly Regex _symbol = new Regex(@"\b[(.:?*+\[\]a-zA-Z^]|{[^}]*}");
        private static string PeekSym(string s, int pos) {
            if (pos >= s.Length) {
                return null;
            } else {
                Match m = _symbol.Match(s, pos);
                if (m.Success) {
                    return m.Value;
                } else {
                    throw new RegexSyntaxException(s, pos, $"No valid symbol");
                }
            }
        }

        private static void AdvancePos(string s, ref int pos) {
            string peekSym = PeekSym(s, pos);
            if (peekSym != null) {
                pos += peekSym.Length;
            }
        }

        private Sequence Parse(string s, int pos) {
            return ParseSequence(s, ref pos);
        }

        private Sequence ParseSequence(string s, ref int pos) {
            int startPos = pos;
            var elements = new List<RegexElement>();
            for (;;) {
                switch (PeekSym(s, pos)) {
                    case null:
                    case ")":
                        AdvancePos(s, ref pos);
                        goto AFTER_RPAR;
                    case ".":
                        elements.Add(new ItemMatchAndMore(pos));
                        AdvancePos(s, ref pos);
                        break;
                    case ":":
                        elements.Add(new AnyDependency(pos));
                        AdvancePos(s, ref pos);
                        break;
                    case "[":
                        AdvancePos(s, ref pos);
                        elements.Add(ParseSet(s, ref pos));
                        AdvancePos(s, ref pos);
                        break;
                    case "(":
                        AdvancePos(s, ref pos);
                        Sequence sequence = ParseSequence(s, ref pos);
                        Subsequence sub = sequence as Subsequence;
                        if (sub != null) {
                            elements.AddRange(sub.Elements);
                        } else {
                            elements.Add(sequence);
                        }
                        AdvancePos(s, ref pos);
                        break;
                    default:
                        throw new RegexValidationException(null, null, $"Invalid symbol at {s.Substring(pos)}");
                }
            }
            AFTER_RPAR:
            Sequence result;
            switch (PeekSym(s, pos)) {
                case "*":
                    result = new ZeroOrMore(startPos, elements);
                    AdvancePos(s, ref pos);
                    break;
                case "+":
                    result = new OneOrMore(startPos, elements);
                    AdvancePos(s, ref pos);
                    break;
                case "?":
                    result = new Optional(startPos, elements);
                    AdvancePos(s, ref pos);
                    break;
                default:
                    result = new Subsequence(startPos, elements);
                    break;
            }
            return result;
        }

        private RegexElement ParseSet(string s, ref int pos) {
            int startPos = pos;
            var itemMatches = new List<ItemMatch>();
            var dependencyMatches = new List<DependencyMatch>();
            bool invert = false;
            if (PeekSym(s, pos) == "^") {
                invert = true;
                AdvancePos(s, ref pos);
            }
            for (;;) {
                string peekSym = PeekSym(s, pos);
                if (peekSym == null) {
                    // Error ...
                    break;
                } else if (peekSym == "]") {
                    break;
                } else {
                    ItemMatch itemMatch;
                    DependencyMatch dependencyMatch;
                    string trimmedSym = peekSym.TrimStart('{').TrimEnd('}');
                    if (_definedDependencyMatches.TryGetValue(trimmedSym, out dependencyMatch)) {
                        dependencyMatches.Add(dependencyMatch);
                    } else if (_definedItemMatches.TryGetValue(trimmedSym, out itemMatch)) {
                        itemMatches.Add(itemMatch);
                    } else if (trimmedSym.Contains("--") || trimmedSym.Contains("->")) {
                        // We try to parse it as a DependencyMatch
                        dependencyMatches.Add(DependencyMatch.Create(trimmedSym, _ignoreCase));
                    } else if (trimmedSym.Contains(":") || trimmedSym.Contains(".")) {
                        // We try to parse it as an ItemMatch
                        itemMatches.Add(new ItemMatch(trimmedSym, _ignoreCase, anyWhereMatcherOk: false));
                    } else {
                        throw new RegexSyntaxException(s, pos, "Name in set cannot be converted to item or dependency match");
                    }
                }
            }
            if (itemMatches.Any()) {
                if (dependencyMatches.Any()) {
                    throw new RegexSyntaxException(s, startPos,
                        "[...] must not contain both item and dependency matches");
                }
                return new ItemSet(startPos, invert, itemMatches);
            } else if (dependencyMatches.Any()) {
                return new DependencySet(startPos, invert, dependencyMatches);
            } else {
                throw new RegexSyntaxException(s, startPos, "[...] must not be empty");
            }
        }
    }
}