using System;
using System.Collections.Generic;
using System.Text;
using NDepCheck.Markers;

namespace NDepCheck {
    internal class ItemAndDependencyFactoryList : IItemAndDependencyFactory {
        // Instead of this list type, one could also build a classic delegation chain. But this would put the burden of adding the 
        // delegation on each implementer (and therefore also the risk that this behavior is changed). So I go with the list.

        private readonly List<IItemAndDependencyFactory> _factories = new List<IItemAndDependencyFactory> { new DefaultItemAndDependencyFactory() };

        public Item CreateItem(ItemType type, string[] values) {
            foreach (var f in _factories) {
                Item result = f.CreateItem(type, values);
                if (result != null) {
                    return result;
                }
            }
            throw new InvalidOperationException("Internal error - this cannot happen, as the last factory has to create an item");
        }

        public Dependency CreateDependency(Item usingItem, Item usedItem, ISourceLocation source, IMarkerSet markers, int ct, int questionableCt = 0, 
                                           int badCt = 0, string notOkReason = null, string exampleInfo = null) {
            foreach (var f in _factories) {
                Dependency result = f.CreateDependency(usingItem, usedItem, source, markers, ct, questionableCt, badCt, notOkReason, exampleInfo);
                if (result != null) {
                    return result;
                }
            }
            throw new InvalidOperationException("Internal error - this cannot happen, as the last factory has to create an item");
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return "Internal list of item & dependency factories";
        }

        public void Add(IItemAndDependencyFactory itemAndDependencyFactory) {
            _factories.Insert(0, itemAndDependencyFactory);
        }

        public void Remove(string namePart) {
            _factories.RemoveAll(f => f.GetType().Name.Contains(namePart));
        }

        public string ListItemAndDependencyFactories() {
            var sb = new StringBuilder();
            foreach (var f in _factories) {
                sb.AppendLine(f.GetType().FullName);
            }
            return sb.ToString();
        }
    }
}