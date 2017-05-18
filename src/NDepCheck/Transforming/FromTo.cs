using System.Collections.Generic;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming {
    public class FromTo {
        [NotNull]
        public readonly Item From;
        public readonly Item To;
        public FromTo([NotNull] Item from, [NotNull] Item to) {
            From = from;
            To = to;
        }

        public override bool Equals(object obj) {
            var other = obj as FromTo;
            return other != null && other.From == From && other.To == To;
        }

        public override int GetHashCode() {
            return From.GetHashCode() ^ To.GetHashCode();
        }

        public FromTo AggregateDependency(Dependency d, Dictionary<FromTo, Dependency> edgeCollector) {
            Dependency result;
            if (!edgeCollector.TryGetValue(this, out result)) {
                result = new Dependency(From, To, d.Source, d.MarkerSet, d.Ct, d.QuestionableCt, d.BadCt, d.ExampleInfo);
                edgeCollector.Add(this, result);
            } else {
                result.AggregateMarkersAndCounts(d);
            }
            result.UsingItem.MergeWithMarkers(d.UsingItem.MarkerSet);
            result.UsedItem.MergeWithMarkers(d.UsedItem.MarkerSet);
            return this;
        }

        public static Dictionary<FromTo, Dependency> AggregateAllDependencies([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies) {
            var result = new Dictionary<FromTo, Dependency>();
            foreach (var d in dependencies) {
                new FromTo(d.UsingItem, d.UsedItem).AggregateDependency(d, result);
            }
            return result;
        }

        public static bool ContainsMatchingDependency(Dictionary<FromTo, Dependency> fromTos, Item from, Item to,
            DependencyPattern patternOrNull = null) {
            Dependency fromTo;
            return fromTos.TryGetValue(new FromTo(from, to), out fromTo) &&
                   (patternOrNull == null || patternOrNull.IsMatch(fromTo));
        }
    }
}
