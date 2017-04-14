using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems {
        private class ProjectionAndFixedPrefix {
            public readonly Projection Projection;
            public readonly string FixedPrefix;

            public ProjectionAndFixedPrefix(Projection projection, string fixedPrefix) {
                Projection = projection;
                FixedPrefix = fixedPrefix;
            }
        }

        private class TrieNode {
            private readonly Dictionary<char, TrieNode> _children;
            [NotNull]
            private SimpleProjector _projectorToUseIfNoCharMatches;

            private Projection[] _projections;

            // ReSharper disable once NotNullMemberIsNotInitialized - reliably set in SetProjector
            public TrieNode(IEqualityComparer<char> equalityComparer) {
                _children = new Dictionary<char, TrieNode>(equalityComparer);
            }

            public void Insert(string triePath, IEqualityComparer<char> equalityComparer) {
                if (triePath != "") {
                    char c = triePath[0];
                    TrieNode childNode;
                    if (!_children.TryGetValue(c, out childNode)) {
                        _children.Add(c, childNode = new TrieNode(equalityComparer));
                    }
                    childNode.Insert(triePath.Substring(1), equalityComparer);
                }
            }

            public void SetProjector(string triePath, ProjectionAndFixedPrefix[] pfps) {
                IEnumerable<ProjectionAndFixedPrefix> matchingProjections =
                    pfps.Where(pfp => pfp.FixedPrefix.StartsWith(triePath) || pfp.FixedPrefix == "");
                IEnumerable<ProjectionAndFixedPrefix> mightLandHere =
                    matchingProjections.Where(pfp => pfp.FixedPrefix == "" || 
                        !_children.Keys.Any(c => pfp.FixedPrefix.StartsWith(triePath + c)));
                _projections = mightLandHere.Select(pfp => pfp.Projection).ToArray();
                _projectorToUseIfNoCharMatches = new SimpleProjector(_projections);
            }

            [NotNull]
            public SimpleProjector SelectProjector(string triePath) {
                if (triePath == "") {
                    return _projectorToUseIfNoCharMatches;
                } else {
                    char c = triePath[0];
                    TrieNode childNode;
                    return _children.TryGetValue(c, out childNode)
                        ? childNode.SelectProjector(triePath.Substring(1))
                        : _projectorToUseIfNoCharMatches;
                }
            }

            public int GetCost() {
                // TODO: The following is wrong - we must somehow integrate over all children
                return AbstractResortableProjectorWithCost.CostOfOrderProjectionList(_projections);
            }
        }

        public class TrieNodeProjector : AbstractResortableProjectorWithCost {
            private readonly TrieNode _root;
            private readonly int _fieldPos;

            public TrieNodeProjector(Projection[] orderedProjections, int fieldPos, IEqualityComparer<char> equalityComparer, string name)
                : base(orderedProjections, name) {
                ProjectionAndFixedPrefix[] pms = orderedProjections
                        .Select(p => new ProjectionAndFixedPrefix(p, p.ItemMatch.ItemPattern.Matchers.ElementAtOrDefault(fieldPos)?.GetKnownFixedPrefix() ?? ""))
                        .ToArray();
                _root = new TrieNode(equalityComparer);
                var allPrefixes = new HashSet<string>(pms.Select(pm => pm.FixedPrefix));
                foreach (var p in allPrefixes) {
                    _root.Insert(p, equalityComparer);
                }
                foreach (var p in allPrefixes) {
                    _root.SetProjector(p, pms);
                }
                _fieldPos = fieldPos;
            }

            public override Item Project(Item item, bool left) {
                return _root.SelectProjector(item.Values.ElementAtOrDefault(_fieldPos)).Project(item, left);
            }

            protected override int Cost => _root.GetCost();
        }

        public class SelfOptimizingPrefixTrieProjector : AbstractSelfOptimizingProjector<TrieNodeProjector> {
            public SelfOptimizingPrefixTrieProjector(Projection[] orderedProjections, bool ignoreCase, int reorganizeInterval) :
                base(orderedProjections, ignoreCase, reorganizeInterval) {
            }

            protected override List<TrieNodeProjector> CreateResortableProjectors(Projection[] orderedProjections) {
                var result = new List<TrieNodeProjector>();
                for (int fieldPos = 0; ; fieldPos++) {
                    // The SelfOptimizingFirstLetterProjector will, in many scenarios, improve
                    // performance by a factor of at most 2. Why? Because in many applications, all
                    // assembly and namespace names start with the same letter ("nunit.dll", 
                    // "nunit.framework.dll" etc.); also, most interesting projections will be for 
                    // items in these assemblies or namespaces, so most projections will match that 
                    // letter. Hence, all the left sides of the dependencies - and a significant 
                    // amount of the right sides - will have to check most projections, exactly like 
                    // with the trivial method that always checks all projections.
                    //
                    // A better method is to check the complete fixed prefixes: Running over a string
                    // is a quick operation; and then we will quickly select only the really matching
                    // projections also for left-side items.
                    //
                    // The data structure to do this with any retries is the so-called "trie".
                    // Example: We have 6 projections, 5 with known prefxes abc, de, ab, abd, and 
                    // x, and a last catch-all projection.
                    // Here is the trie; the [#] markers are used to show the projection list attached
                    // to that node - it is used when projecting an item if that trie node is reached, 
                    // but no outgoing edge can be traversed:
                    //
                    // a[1]--->b[2]--->c[3]
                    //             --->d[4]
                    // d[5]--->e[6]
                    // x[7]
                    // [8]
                    // 
                    // [1]: ** (the item's prefix cannot be ab, abc, or abd, as we could not traverse
                    //          the edge to b!)
                    // [2]: ab, ** (again, the item's prefix cannot be abc or abd; we have to include 
                    //          **, as some other part of the ab projection pattern might not match,
                    //          so we might fall through to **. Remember, a prefix check says only that
                    //          it is *possible* that a pattern matches).
                    // [3]: abc, ab, **
                    // [4]: abd, ab, **
                    // [5]: **
                    // [6]: de, **
                    // [7]: x, **
                    // [8]: **

                    int fieldPos0 = fieldPos;

                    if (orderedProjections.All(p => p.ItemMatch.ItemPattern.Matchers.ElementAtOrDefault(fieldPos0) == null)) {
                        break;
                    }

                    result.Add(new TrieNodeProjector(orderedProjections, fieldPos, _equalityComparer, "TrieNodeProject:" + fieldPos0));
                }

                return result;
            }

            protected override TrieNodeProjector SelectProjector(IEnumerable<TrieNodeProjector> projectors, Item item, bool left) {
                return projectors.FirstOrDefault();
            }
        }
        //    private class SelfOptimizingPrefixProjector : SelfOptimizingProjector {
        //        public SelfOptimizingPrefixProjector(Projection[] orderedProjections, int reorganizeInterval = 1000) :
        //            base(orderedProjections, reorganizeInterval) {
        //        }

        //        protected override List<SelectingProjector> CreateSelectingProjectors(Projection[] orderedProjections) {
        //            //       abc def xyz ab[^c] [^ab|^def|^xyz]
        //            // abc   +
        //            // def       +
        //            // ab    +           +
        //            // xyz           +
        //            // **    +   +   +   +      +

        //throw new NotImplementedException();
        //        }
        //    }
    }
}
