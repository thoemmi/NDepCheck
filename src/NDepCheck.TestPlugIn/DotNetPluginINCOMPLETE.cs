using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Markers;
using NDepCheck.Reading.AssemblyReading;

namespace NDepCheck.TestPlugin {
    public class DotNetType : Item {
        public DotNetType([NotNull] ItemType type, string[] values) : base(type, values) {
        }

        // TODO - paths ... ... 
        public IEnumerable<Dependency> ToBaseTypes => GetOutgoing().Where(d => d.IsMarkerMatch(
            AbstractDotNetAssemblyDependencyReader._directlyderivedfrom, AbstractDotNetAssemblyDependencyReader._directlyimplements));
    }


    public class MyDotNetFactory : IItemAndDependencyFactory {
        public static readonly ItemType DOTNETTYPE = ItemType.New("(Namespace:Name:Assembly.Name");

        public string GetHelp(bool detailedHelp, string filter) {
            return "MyDotNetFactory";
        }

        public Item CreateItem(ItemType type, string[] values) {
            return type.Get(values, "Member.Name") != null ? null : new DotNetType(DOTNETTYPE, values);
        }

        public Dependency CreateDependency(Item usingItem, Item usedItem, ISourceLocation source, IMarkerSet markers, int ct, int questionableCt = 0, 
                                           int badCt = 0, string notOkReason = null, string exampleInfo = null) {
            return null; // We use standard dependencies between items
        }
    }
}
