using System.Collections.Generic;
using System.Linq;
using Gibraltar;

namespace NDepCheck {
    public enum EnvironmentCreationType { Manual, AutoRead, AutoTransform }

    public class Environment {
        public readonly string Name;
        public readonly EnvironmentCreationType Type;
        private readonly List<Dependency> _dependencies;

        public readonly Intern<ItemTail> ItemTailCache = new Intern<ItemTail>();
        public readonly Intern<Item> ItemCache = new Intern<Item>();

        public Environment(string name, EnvironmentCreationType type, IEnumerable<Dependency> dependencies) {
            Name = name;
            Type = type;
            _dependencies = dependencies.ToList();
        }

        public override string ToString() {
            return $"{Name} ({_dependencies.Count})";
        }

        public IEnumerable<Dependency> Dependencies => _dependencies;

        public void AddDependencies(IEnumerable<Dependency> dependencies) {
            _dependencies.AddRange(dependencies);
        }

        public void ReplaceDependencies(IEnumerable<Dependency> dependencies) {
            _dependencies.Clear();
            AddDependencies(dependencies);
        }
    }
}