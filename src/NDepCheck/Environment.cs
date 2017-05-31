using System.Collections.Generic;
using System.Linq;
using Gibraltar;
using JetBrains.Annotations;
using NDepCheck.Markers;

namespace NDepCheck {
    public enum EnvironmentCreationType { Manual, AutoRead, AutoTransform }

    public class Environment {
        public string Name;
        public readonly EnvironmentCreationType Type;
        private readonly List<Dependency> _dependencies;

        public readonly Intern<ItemTail> ItemTailCache = new Intern<ItemTail>();
        private readonly Intern<Item> ItemCache = new Intern<Item>();

        public IItemAndDependencyFactory ItemAndDependencyFactory { get; } = new DefaultItemAndDependencyFactory();

        public Environment(string name, EnvironmentCreationType type, IEnumerable<Dependency> dependencies) {
            Name = name;
            Type = type;
            _dependencies = dependencies.ToList();
        }

        public override string ToString() {
            return $"{Name} ({_dependencies.Count})";
        }

        public IEnumerable<Dependency> Dependencies => _dependencies;

        public int DependencyCount => _dependencies.Count;

        public void AddDependencies(IEnumerable<Dependency> dependencies) {
            _dependencies.AddRange(dependencies);
        }

        public void ReplaceDependencies(IEnumerable<Dependency> dependencies) {
            _dependencies.Clear();
            AddDependencies(dependencies);
        }

        public Item NewItem([NotNull] ItemType type, [ItemNotNull] params string[] values) {
            return ItemAndDependencyFactory.New(ItemCache, type, values, null);
        }

        public Item NewItem([NotNull] ItemType type, [ItemNotNull] string[] values,
            [CanBeNull] [ItemNotNull] string[] markers) {
            return ItemAndDependencyFactory.New(ItemCache, type, values, markers);
        }

        public Item NewItem([NotNull] ItemType type, [NotNull] string reducedName) {
            return ItemAndDependencyFactory.New(ItemCache, type, reducedName.Split(':'), null);
        }

        public Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [CanBeNull] IMarkerSet markers, int ct, int questionableCt = 0,
            int badCt = 0, [CanBeNull] string exampleInfo = null) {
            return ItemAndDependencyFactory.CreateDependency(usingItem, usedItem, source,
                markers, ct, questionableCt, badCt, exampleInfo);
        }

        public Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [NotNull] IEnumerable<string> markers, int ct, int questionableCt = 0,
            int badCt = 0, [CanBeNull] string exampleInfo = null) {
            return CreateDependency(usingItem, usedItem, source,
                new ReadOnlyMarkerSet(false, markers), ct, questionableCt, badCt, exampleInfo);
        }

        public Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [NotNull] string markers, int ct, int questionableCt = 0, int badCt = 0,
            [CanBeNull] string exampleInfo = null) {
            return CreateDependency(usingItem, usedItem, source, 
                markers: new ReadOnlyMarkerSet(false, markers.Split('&', '+', ',')), ct: ct,
                questionableCt: questionableCt, badCt: badCt, exampleInfo: exampleInfo);
        }

    }
}