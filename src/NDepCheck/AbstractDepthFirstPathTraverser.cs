using System.Collections.Generic;
using JetBrains.Annotations;

namespace NDepCheck {
    public abstract class AbstractDepthFirstPathTraverser {
        private readonly bool _retraverseItems;
        private readonly Stack<Dependency> _currentPath = new Stack<Dependency>();

        protected AbstractDepthFirstPathTraverser(bool retraverseItems) {
            _retraverseItems = retraverseItems;
        }

        [Pure]
        protected int WithAddedItemHash(int hash, [NotNull] Item i) {
            // This is a somewhat complicated hash: Its purpose is to map cyclically
            // shifted cycles to the same hashcode. For simplicity, it is therefore
            // commutative.
            // However, a simple XOR would be bad if e.g. the items were named A, B, C, D:
            // Then, A ^ B is the same as C ^ D. So, I try to inject the names somewhat
            // more "specifically": I do this by also XORing with a single bit at some
            // name-dependent position (not ORing, because that would fill up the
            // hash code to all 1s).
            // Hopefully, this works.
            int h = i.GetHashCode();
            return hash ^ h ^ (1 << (h % 31));
        }

        protected abstract void OnTailLoopsBack(Stack<Dependency> currentPath, Item tail);

        protected abstract void AfterPushDependency(Stack<Dependency> currentPath);

        protected abstract void OnFoundCycleToRoot(Stack<Dependency> currentPath);

        protected abstract void BeforePopDependency(Stack<Dependency> currentPath);

        protected abstract void OnPathEnd(Stack<Dependency> currentPath);

        protected void Traverse(Item root, Item tail, bool ignoreCyclesInThisRecursion,
            Dictionary<Item, IEnumerable<Dependency>> outgoing, Dictionary<Item, int> allVisitedItems, int restLength,
            HashSet<int> foundCycleHashs, int pathHash) {

            int lengthCheckedBehindTail;
            bool tailAlreadyVisited = allVisitedItems.TryGetValue(tail, out lengthCheckedBehindTail);
            if (tailAlreadyVisited && lengthCheckedBehindTail >= restLength) {
                OnTailLoopsBack(_currentPath, tail);
            } else {
                if (restLength > 0 && outgoing.ContainsKey(tail)) {
                    allVisitedItems[tail] = restLength;
                    // we are at this item for the first time - check whether we find a path back to the root item
                    foreach (var nextDep in outgoing[tail]) {
                        Item newTail = nextDep.UsedItem;
                        _currentPath.Push(nextDep);
                        AfterPushDependency(_currentPath);
                        if (!ignoreCyclesInThisRecursion && Equals(newTail, root)) {
                            // We found a cycle to the rootItem!

                            pathHash ^= restLength;
                            if (foundCycleHashs.Contains(pathHash)) {
                                // The cycle was already found via another item
                                // - we ignore it.
                            } else {
                                // New cycle found; we record it
                                OnFoundCycleToRoot(_currentPath);
                                foundCycleHashs.Add(pathHash);
                            }
                        } else {
                            Traverse(root, newTail, false, outgoing, allVisitedItems, restLength - 1, foundCycleHashs,
                                WithAddedItemHash(pathHash, newTail));
                        }
                        BeforePopDependency(_currentPath);
                        _currentPath.Pop();
                    }
                    if (_retraverseItems) {
                        allVisitedItems.Remove(tail);
                    }
                } else {
                    OnPathEnd(_currentPath);
                }
            }
        }
    }
}