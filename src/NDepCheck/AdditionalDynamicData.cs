using System.Collections.Generic;
using System.Dynamic;

namespace NDepCheck {
    public class AdditionalDynamicData : DynamicObject {
        private readonly Item _item;
        private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

        public AdditionalDynamicData(Item item) {
            _item = item;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            result = null;
            if (!_properties.ContainsKey(binder.Name)) {
                Log.WriteError($"Item '{_item}' has not been assigned property '{binder.Name}'");
                return false;
            } else {
                result = _properties[binder.Name];
                return true;
            }
        }

        public override bool TrySetMember(SetMemberBinder binder, object value) {
            _properties[binder.Name] = value;
            return true;
        }
    }
}