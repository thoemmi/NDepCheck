using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Rendering {
    public class OrderSupport {
        public static Option CreateOption() {
            return new Option("of", "order-field", "# or &",
                "Field or marker pattern on which items are sorted; fields are counted from 1 up. Items with equal order are sorted by dependency count",
                @default: "Dependency count.");
        }

        [NotNull]
        public Func<Item, IComparable> OrderSelector {
            get;
        }

        /// <param name="orderSelector">must always return not-null</param>
        public OrderSupport([NotNull] Func<Item, IComparable> orderSelector) {
            OrderSelector = orderSelector;
        }

        public static OrderSupport Create(string pattern, bool ignoreCase) {
            int field;
            if (int.TryParse(pattern, out field)) {
                return new OrderSupport(item => item.GetCasedValue(field));
            } else {
                IMatcher[] orderMatcher = { MarkerMatch.CreateMatcher(pattern, ignoreCase) };
                return new OrderSupport(item => item.MarkerSet.MatchingMarkers(orderMatcher).Sum(m => item.MarkerSet.GetValue(m, ignoreCase)));
            }
        }

        public void SortWithEdgeCount(List<Item> list, Dependency[] relevantDependencies, Func<Item, Dependency, bool> filter) {
            list.Sort((i1, i2) => {
                IComparable order1 = OrderSelector(i1);
                IComparable order2 = OrderSelector(i2);
                return !Equals(order1, order2)
                    ? order1.CompareTo(order2)
                    : Sum(relevantDependencies, d => filter(i2, d)) - Sum(relevantDependencies, d => filter(i1, d));
            });
        }

        private static int Sum([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
            return dependencies.Where(filter).Sum(d => d.Ct);
        }
    }
}
