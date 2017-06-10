using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Gibraltar;
using JetBrains.Annotations;
using NDepCheck.Markers;
using NDepCheck.Matching;

namespace NDepCheck {
    public enum GraphCreationType { Manual, AutoRead, AutoTransform }

    /// <summary>
    /// A WorkingGraph is the basic model of NDepCheck.
    /// It consists of a set of dependencies and their incident items. 
    /// Each item in a graph must have an outgoing or incoming dependency - isolated items are not allowed.
    /// 
    /// I have thought long about the alternative, namely allowing isolated items. A paradigmatic
    /// example are ressources in .Net assemblies, as long as the assembly is considered a
    /// dimension [and not an item of its own]) - then, there is no useful dependency of a ressource
    /// to something.
    /// However, some algorithms delete dependencies; and then, one has to ask whether items that
    /// become isolated should remain in the graph. I am quite(!) sure that in many cases, one expects
    /// the items to vanish together with their last incident dependency; and therefore, a graph
    /// is (for pragmatic reasons only, as described) defined by its dependencies and their incident
    /// items alone.
    /// 
    /// But what for needed isolated items, like the ressources mentioned? Tha "workaround answer" is
    /// to give them a looping dependency of their own, with marker 'isolated. By default, such
    /// "single loops" should be ignored by most algorithms, but there should be options to recognize
    /// them.
    /// </summary>
    public class WorkingGraph {
        private static int _stickyIdCt = 0;

        public readonly string StickyId = "#_" + ++_stickyIdCt;
        public string UserDefinedName { get; private set; }
        public readonly GraphCreationType Type;

        private readonly List<Dependency> _dependencies;
        private List<Dependency> _visibleDependencies;
        private List<Dependency> _hiddenDependencies;

        private IReadOnlyDictionary<Item, Dependency[]> _outgoingVisible;
        private IReadOnlyDictionary<Item, Dependency[]> _incomingVisible;

        // TODO: The following two caches are most probably wrong - they should only be visible inside algorithms that need them (like Readers).
        public readonly Intern<ItemTail> ItemTailCache = new Intern<ItemTail>();
        private readonly Intern<Item> ItemCache = new Intern<Item>();

        [NotNull]
        internal readonly ItemAndDependencyFactoryList _globalItemAndDependencyFactories;
        [CanBeNull]
        internal ItemAndDependencyFactoryList _itemAndDependencyFactories;

        private readonly List<DependencyMatch> _filters = new List<DependencyMatch>();

        internal WorkingGraph(string userDefinedName, GraphCreationType type, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
                           [NotNull] ItemAndDependencyFactoryList itemAndDependencyFactories) {
            UserDefinedName = userDefinedName;
            Type = type;
            _dependencies = dependencies.ToList();
            ClearAllCaches();
            _globalItemAndDependencyFactories = itemAndDependencyFactories;
        }

        public string FullName => $"{StickyId,-3}:{UserDefinedName}";

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return $"{FullName} ({_dependencies.Count})";
        }

        public string AsString() {
            LazilyFillVisibilityCaches();
            int hiddenCount = _hiddenDependencies.Count;
            return $"{FullName} ({_visibleDependencies.Count}" + (hiddenCount > 0 ? $"+[{hiddenCount}]" : "") + ")";
        }

        public IEnumerable<Dependency> VisibleDependencies {
            get {
                LazilyFillVisibilityCaches();
                return _visibleDependencies;
            }
        }

        private void LazilyFillVisibilityCaches() {
            if (_visibleDependencies == null) {
                _hiddenDependencies = new List<Dependency>();
                if (_filters.Any()) {
                    _visibleDependencies = new List<Dependency>();
                    foreach (var d in _dependencies) {
                        (_filters.Any(f => f.IsMatch(d)) ? _visibleDependencies : _hiddenDependencies).Add(d);
                    }
                } else {
                    _visibleDependencies = _dependencies.ToList();
                }
            }
        }

        public int DependencyCount {
            get {
                LazilyFillVisibilityCaches();
                return _visibleDependencies.Count;
            }
        }

        [NotNull]
        public IReadOnlyDictionary<Item, Dependency[]> VisibleOutgoingVisible {
            get {
                if (_outgoingVisible == null) {
                    var result = new Dictionary<Item, List<Dependency>>();
                    foreach (var d in VisibleDependencies) {
                        List<Dependency> dependencies;
                        if (!result.TryGetValue(d.UsingItem, out dependencies)) {
                            result.Add(d.UsingItem, dependencies = new List<Dependency>());
                        }
                        dependencies.Add(d);
                    }
                    _outgoingVisible = result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                }
                return _outgoingVisible;
            }
        }

        [NotNull]
        public IReadOnlyDictionary<Item, Dependency[]> VisibleIncomingVisible {
            get {
                if (_incomingVisible == null) {
                    var result = new Dictionary<Item, List<Dependency>>();
                    foreach (var d in VisibleDependencies) {
                        List<Dependency> dependencies;
                        if (!result.TryGetValue(d.UsedItem, out dependencies)) {
                            result.Add(d.UsedItem, dependencies = new List<Dependency>());
                        }
                        dependencies.Add(d);
                    }
                    _incomingVisible = result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                }
                return _incomingVisible;
            }
        }

        private void ClearAllCaches() {
            _outgoingVisible = null;
            _incomingVisible = null;
            _visibleDependencies = null;
            _hiddenDependencies = null;
        }

        public void AddDependencies(IEnumerable<Dependency> dependencies) {
            _dependencies.AddRange(dependencies);
            ClearAllCaches();
        }

        public void ReplaceVisibleDependencies(IEnumerable<Dependency> dependencies) {
            LazilyFillVisibilityCaches();
            _dependencies.Clear();
            _dependencies.AddRange(_hiddenDependencies);
            _dependencies.AddRange(dependencies);
            ClearAllCaches();
        }

        private Item CompleteItem(Item item, [CanBeNull] [ItemNotNull] string[] markers) {
            if (markers != null) {
                item.MergeWithMarkers(new MutableMarkerSet(item.Type.IgnoreCase,
                    AbstractMarkerSet.CreateMarkerSetWithClonedDictionary(item.Type.IgnoreCase, markers)));
            }
            Item result = ItemCache.GetReference(item).SetWorkingGraph(this);
            return result;
        }

        public Item CreateItem([NotNull] ItemType type, [ItemNotNull] params string[] values) {
            return CompleteItem((_itemAndDependencyFactories ?? _globalItemAndDependencyFactories).CreateItem(type, values), null);
        }

        public Item CreateItem([NotNull] ItemType type, [ItemNotNull] string[] values,
            [CanBeNull] [ItemNotNull] string[] markers) {
            return CompleteItem((_itemAndDependencyFactories ?? _globalItemAndDependencyFactories).CreateItem(type, values), markers);
        }

        public Item CreateItem([NotNull] ItemType type, [NotNull] string reducedName) {
            return CompleteItem((_itemAndDependencyFactories ?? _globalItemAndDependencyFactories).CreateItem(type, reducedName.Split(':')), null);
        }

        public Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [CanBeNull] IMarkerSet markers, int ct, int questionableCt = 0,
            int badCt = 0, [CanBeNull] string notOkReason = null, [CanBeNull] string exampleInfo = null) {
            return (_itemAndDependencyFactories ?? _globalItemAndDependencyFactories).CreateDependency(usingItem, usedItem, source,
                markers, ct, questionableCt, badCt, notOkReason, exampleInfo);
        }

        public Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [NotNull] IEnumerable<string> markers, int ct, int questionableCt = 0,
            int badCt = 0, [CanBeNull] string notOkReason = null, [CanBeNull] string exampleInfo = null) {
            return CreateDependency(usingItem, usedItem, source,
                new ReadOnlyMarkerSet(false, markers), ct, questionableCt, badCt, notOkReason, exampleInfo);
        }

        public Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [NotNull] string markers, int ct, int questionableCt = 0, int badCt = 0,
            [CanBeNull] string notOkReason = null, [CanBeNull] string exampleInfo = null) {
            return CreateDependency(usingItem, usedItem, source,
                markers: new ReadOnlyMarkerSet(false, markers.Split('&', '+', ',')), ct: ct,
                questionableCt: questionableCt, badCt: badCt, notOkReason: notOkReason, exampleInfo: exampleInfo);
        }

        public void AddItemAndDependencyFactory(IItemAndDependencyFactory itemAndDependencyFactory) {
            if (_itemAndDependencyFactories == null) {
                _itemAndDependencyFactories = new ItemAndDependencyFactoryList();
            }
            _itemAndDependencyFactories.Add(itemAndDependencyFactory);
        }

        public void RemoveItemAndDependencyFactories(string namePart) {
            if (_itemAndDependencyFactories == null) {
                _itemAndDependencyFactories = new ItemAndDependencyFactoryList();
            }
            _itemAndDependencyFactories.Remove(namePart);
        }

        public string ListItemAndDependencyFactories() {
            return _itemAndDependencyFactories?.ListItemAndDependencyFactories();
        }

        #region Filters

        public void AddGraphFilter(string filter, bool ignorecase) {
            _filters.Add(DependencyMatch.Create(filter, ignorecase));
            ClearAllCaches();
        }

        public void RemoveGraphFilters(string substring, bool ignorecase) {
            if (substring == null) {
                if (_filters.Any()) {
                    // Filters change
                    ClearAllCaches();
                }
                _filters.Clear();
            } else {
                StringComparison comparison = ignorecase
                    ? StringComparison.InvariantCultureIgnoreCase
                    : StringComparison.InvariantCulture;
                int removed = _filters.RemoveAll(f => f.Representation.IndexOf(substring, comparison) >= 0);
                if (removed > 0) {
                    ClearAllCaches();
                }
            }
        }

        public void ShowFilters() {
            foreach (var f in _filters) {
                Log.WriteInfo(f.Representation);
            }
        }

        #endregion Filters

        public void SetName(string newName) {
            UserDefinedName = newName;
        }
    }
}