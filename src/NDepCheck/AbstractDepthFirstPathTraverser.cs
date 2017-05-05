using System.Collections.Generic;

namespace NDepCheck {
    public abstract class AbstractDepthFirstPathTraverser<TDependency, TItem>
            where TDependency : AbstractDependency<TItem>
            where TItem : AbstractItem<TItem> {
        private readonly bool _retraverseItems;
        private readonly Stack<TDependency> _currentPath = new Stack<TDependency>();

        protected AbstractDepthFirstPathTraverser(bool retraverseItems) {
            _retraverseItems = retraverseItems;
        }

        //[Pure]
        //protected int WithAddedItemHash(int hash, [NotNull] Item i) {
        //    // This is a somewhat complicated hash: Its purpose is to map cyclically
        //    // shifted cycles to the same hashcode. For simplicity, it is therefore
        //    // commutative.
        //    // However, a simple XOR would be bad if e.g. the items were named A, B, C, D:
        //    // Then, A ^ B is the same as C ^ D. So, I try to inject the names somewhat
        //    // more "specifically": I do this by also XORing with a single bit at some
        //    // name-dependent position (not ORing, because that would fill up the
        //    // hash code to all 1s).
        //    // Hopefully, this works.
        //    int h = i.GetHashCode();
        //    return hash ^ h ^ (1 << (h % 31));
        //}

        protected abstract void OnTailLoopsBack(Stack<TDependency> currentPath, TItem tail);

        protected abstract void AfterPushDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath);

        protected abstract void OnFoundCycleToRoot(Stack<TDependency> currentPath);

        protected abstract void BeforePopDependency(Stack<TDependency> currentPath, bool alreadyVisitedLastUsedItemInCurrentPath);

        protected abstract void OnPathEnd(Stack<TDependency> currentPath);

        protected void Traverse(TItem root, TItem tail, bool ignoreCyclesInThisRecursion,
            Dictionary<TItem, IEnumerable<TDependency>> outgoing, Dictionary<TItem, int> allVisitedItems, int restLength) {

            int lengthCheckedBehindTail;
            bool tailAlreadyVisited = allVisitedItems.TryGetValue(tail, out lengthCheckedBehindTail);
            if (!tailAlreadyVisited || lengthCheckedBehindTail < restLength) {
                if (restLength > 0 && outgoing.ContainsKey(tail)) {
                    allVisitedItems[tail] = restLength;
                    // we are at this item for the first time - check whether we find a path back to the root item
                    foreach (var nextDep in outgoing[tail]) {
                        TItem newTail = nextDep.UsedItem;
                        _currentPath.Push(nextDep);
                        bool alreadyVisitedUsedItem = allVisitedItems.ContainsKey(newTail);
                        AfterPushDependency(_currentPath, alreadyVisitedUsedItem);
                        if (!ignoreCyclesInThisRecursion && Equals(newTail, root)) {
                            // We found a cycle to the rootItem!
                            OnFoundCycleToRoot(_currentPath);
                        } else {
                            Traverse(root, newTail, false, outgoing, allVisitedItems, restLength - 1);
                        }
                        BeforePopDependency(_currentPath, alreadyVisitedUsedItem);
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