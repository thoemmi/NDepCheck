using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public class InputContext {
        [NotNull]
        private readonly Stack<List<Dependency>> _dependenciesStack = new Stack<List<Dependency>>();

        [NotNull]
        private List<Dependency> DependencyList => _dependenciesStack.Peek();

        public InputContext([NotNull] string fileName) {
            Filename = fileName;
            _dependenciesStack.Push(new List<Dependency>());
        }

        internal void AddDependency(Dependency d) {
            if (d.InputContext != this) {
                throw new ArgumentException(nameof(d));
            }
            DependencyList.Add(d);
        }

        [NotNull]
        public string Filename {
            get;
        }

        public int BadDependenciesCount => DependencyList.Sum(d => d.BadCt);

        public int QuestionableDependenciesCount => DependencyList.Sum(d => d.QuestionableCt);

        public IEnumerable<Dependency> Dependencies => DependencyList;

        public int PushDependencies([ItemNotNull] IEnumerable<Dependency> dependencies) {
            if (dependencies.Contains(null)) {
                throw new ArgumentNullException(nameof(dependencies), "Contains null item");
            }

            List<Dependency> dependencyList = dependencies.ToList();
            _dependenciesStack.Push(dependencyList);
            return dependencyList.Count;
        }

        internal int PopDependencies() {
            _dependenciesStack.Pop();
            return _dependenciesStack.Peek().Count;
        }
    }
}