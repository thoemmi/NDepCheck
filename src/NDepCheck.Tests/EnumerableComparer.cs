using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NDepCheck.TestAssembly {
    [ExcludeFromCodeCoverage]
    internal class EnumerableComparer<T> {

        private readonly Func<T, T, bool> _matchesPerfectly;
        private readonly Func<T, T, bool> _matchesAlmost;
        private List<T> _itemsInANotInB;
        private List<T> _itemsInBNotInA;
        private List<T> _itemsMatchingPerfectly;
        private List<T> _itemsMatchingAlmost;

        public EnumerableComparer(Func<T, T, bool> matchesPerfectly, Func<T, T, bool> matchesAlmost) {
            _matchesPerfectly = matchesPerfectly;
            _matchesAlmost = matchesAlmost;
        }

        public void Compare(IEnumerable<T> listA, IEnumerable<T> listB) {
            _itemsInANotInB = new List<T>();
            _itemsInBNotInA = new List<T>(listB);
            _itemsMatchingPerfectly = new List<T>();
            _itemsMatchingAlmost = new List<T>();

            foreach (T itemA in listA) {
                bool foundInB = false;
                foreach (T itemB in new List<T>(_itemsInBNotInA)) {
                    if (_matchesPerfectly(itemA, itemB)) {
                        _itemsMatchingPerfectly.Add(itemA);
                        _itemsInBNotInA.Remove(itemB);
                        foundInB = true;
                        //break;
                    } else if (_matchesAlmost(itemA, itemB)) {
                        _itemsMatchingAlmost.Add(itemB);
                        _itemsInBNotInA.Remove(itemB);
                        foundInB = true;
                        //break;
                    }
                }

                if (!foundInB) {
                    T localItemA = itemA;
                    bool alreadyMatched = _itemsMatchingPerfectly.Concat(_itemsMatchingAlmost)
                                                                 .Any(item => _matchesAlmost(localItemA, item));
                    if (!alreadyMatched) {
                        _itemsInANotInB.Add(itemA);
                    }
                }
            }
        }

        public List<T> ItemsInANotInB => _itemsInANotInB;

        public List<T> ItemsInBNotInA => _itemsInBNotInA;

        public List<T> ItemsMatchingPerfectly => _itemsMatchingPerfectly;

        public List<T> ItemsMatchingAlmost => _itemsMatchingAlmost;
    }
}
