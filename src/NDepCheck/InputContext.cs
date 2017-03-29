using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public class InputContext {
        [NotNull]
        private readonly List<Dependency> _dependencies = new List<Dependency>();

        public InputContext([NotNull] string fileName) {
            Filename = fileName;
        }

        internal void AddDependency(Dependency d) {
            if (d.InputContext != this) {
                throw new ArgumentException(nameof(d));
            }
            _dependencies.Add(d);
        }

        //////public DependencyRuleSet GetOrCreateDependencyRuleSet(string fileIncludeStack) {
        //////    string dependencyFilename = Path.GetFilename(_fileName) + _globalContext.RuleFileExtension;
        //////    return _globalContext.GetOrCreateDependencyRuleSet(_globalContext, dependencyFilename, fileIncludeStack);
        //////}

        [NotNull]
        public string Filename { get; }

        public int BadDependenciesCount => _dependencies.Sum(d => d.BadCt);

        public int QuestionableDependenciesCount => _dependencies.Sum(d => d.QuestionableCt);

        public IEnumerable<Dependency> Dependencies => _dependencies;

        public void SetDependencies(IEnumerable<Dependency> dependencies) {
            _dependencies.Clear();
            _dependencies.AddRange(dependencies);
        }
    }
}