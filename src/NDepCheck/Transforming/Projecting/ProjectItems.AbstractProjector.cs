using System;
using System.Collections.Generic;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems {
        private class CharIgnoreCaseEqualityComparer : IEqualityComparer<char> {
            public bool Equals(char x, char y) {
                return Char.ToUpperInvariant(x) == Char.ToUpperInvariant(y);
            }

            public int GetHashCode(char obj) {
                return Char.ToUpperInvariant(obj).GetHashCode();
            }
        }

        public abstract class AbstractProjectorWithProjectionList : IProjector {
            protected readonly Projection[] _orderedProjections;

            protected AbstractProjectorWithProjectionList(Projection[] orderedProjections) {
                _orderedProjections = orderedProjections;
            }

            public abstract Item Project(Item item, bool left);

            protected Item ProjectBySequentialSearch(Item item, bool left) {
                foreach (var p in _orderedProjections) {
                    Item result = p.Match(item, left);
                    if (result != null) {
                        return result;
                    }
                }
                return null;
            }
        }

        private class SimpleProjector : AbstractProjectorWithProjectionList {
            public SimpleProjector(Projection[] orderedProjections) : base(orderedProjections) {
            }

            public override Item Project(Item item, bool left) {
                return ProjectBySequentialSearch(item, left);
            }
        }
    }
}
