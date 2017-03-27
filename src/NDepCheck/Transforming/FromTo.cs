using JetBrains.Annotations;

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
    }
}
