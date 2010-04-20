using System.Collections.Generic;

namespace DotNetArchitectureChecker {
    public class Options {
        private readonly List<DirectoryOption> _directories = new List<DirectoryOption>();

        /// <summary>
        /// Set output file name. If set to <c>null</c> (or left
        /// at <c>null</c>), no DOT output is created.
        /// </summary>
        /// <value>The dot filename.</value>
        public string DotFilename { get; set; }

        /// <value>
        /// Show transitive edges. If set to <c>null</c> (or left 
        /// at <c>null</c>), transitive edges are heuristically
        /// removed.
        /// </value>
        public bool ShowTransitiveEdges { get; set; }

        /// <value>
        /// If not null, show a concrete dependency 
        /// for each illegal edge.
        /// </value>
        public int? StringLengthForIllegalEdges { get; set; }

        /// <value>
        /// Mark output of <c>DependencyGrapher</c>
        /// as verbose.
        /// </value>
        public bool Verbose { get; set; }

        public bool Debug { get; set; }

        public DependencyRuleSet DefaultRuleSet { get; set; }

        public string[] Assemblies { get; set; }

        public List<DirectoryOption> Directories {
            get { return _directories; }
        }
    }
}