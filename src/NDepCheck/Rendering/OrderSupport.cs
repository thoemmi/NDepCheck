using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Transforming;

namespace NDepCheck.Rendering {
    public class OrderSupport {
        public static Option CreateOption() {
            return new Option("of", "order-field", "# or &",
                "Field or marker pattern on which items are sorted; fields are counted from 1 up. Items with equal order are sorted by dependency count",
                @default: "Dependency count.");
        }

        [NotNull]
        public Func<Item, string> OrderSelector { get; }

        public OrderSupport([NotNull] Func<Item, string> orderSelector) {
            OrderSelector = orderSelector;
        }

        public static OrderSupport Create(string pattern, bool ignoreCase) {
            int field;
            if (Int32.TryParse(pattern, out field)) {
                return new OrderSupport(item => item.GetCasedValue(field));
            } else {
                IMatcher orderMatcher = MarkerMatch.CreateMatcher(pattern, ignoreCase);
                return new OrderSupport(item => item.Markers.FirstOrDefault(m => orderMatcher.Matches(m) != null));
            }
        }

        public void SortWithEdgeCount(List<Item> list, Dependency[] relevantDependencies, Func<Item, Dependency, bool> filter) {
            list.Sort((i1, i2) => {
                string order1 = OrderSelector(i1);
                string order2 = OrderSelector(i2);
                return order1 != order2
                    ? String.Compare(order1, order2, StringComparison.Ordinal)
                    : Sum(relevantDependencies, d => filter(i2, d)) - Sum(relevantDependencies, d => filter(i1, d));
            });
        }

        private static int Sum(IEnumerable<Dependency> dependencies, Func<Dependency, bool> filter) {
            return dependencies.Where(filter).Sum(d => d.Ct);
        }
    }
}
