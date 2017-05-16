using System.Collections.Generic;

namespace NDepCheck.Transforming {
    public class ItemEffectOptions : EffectOptions<Item> {
        public ItemEffectOptions() : base("item") {
        }

        public IEnumerable<Option> AllOptions => BaseOptions;
    }
}