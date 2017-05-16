using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Markers;

namespace NDepCheck.Transforming {
    public abstract class EffectOptions<T> where T : IWithMutableMarkerSet {
        public readonly Option AddMarkerOption;
        public readonly Option RemoveMarkerOption;
        public readonly Option DeleteOption;

        public static readonly Action<T> DELETE_ACTION_MARKER = x => { };

        protected EffectOptions([NotNull] string sort) {
            AddMarkerOption = new Option("am", "add-marker", "marker", "add a marker to the " + sort, @default: "", multiple: true);
            RemoveMarkerOption = new Option("rm", "remove-marker", "markerpattern", "remove matching markers from the " + sort, @default: "", multiple: true);
            DeleteOption = new Option("d" + sort.First(), "delete-" + sort, "", "delete the " + sort, @default: "keep " + sort);
        }

        protected IEnumerable<Option> BaseOptions => new[] { AddMarkerOption, RemoveMarkerOption, DeleteOption };

        protected internal virtual IEnumerable<Action<T>> Parse([NotNull] GlobalContext globalContext, 
                [CanBeNull] string argsAsString, string defaultReasonForSetBad, bool ignoreCase,
                [NotNull] [ItemNotNull] IEnumerable<OptionAction> moreOptionActions) {
            var result = new List<Action<T>>();

            Option.Parse(globalContext, argsAsString, new[] {
                AddMarkerOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name");
                    result.Add(obj => obj.IncrementMarker(marker));
                    return j;
                }),
                RemoveMarkerOption.Action((args, j) => {
                    string markerpattern = Option.ExtractRequiredOptionValue(args, ref j, "missing marker pattern");
                    result.Add(obj => obj.RemoveMarkers(markerpattern, ignoreCase));
                    return j;
                }),
                DeleteOption.Action((args, j) => {
                    result.Add(DELETE_ACTION_MARKER);
                    return j;
                })
            }.Concat(moreOptionActions).ToArray());
            return result;
        }

        public static void Execute([NotNull, ItemNotNull] IEnumerable<Action<T>> effects, [NotNull, ItemNotNull] HashSet<T> objects) {
            foreach (var d in objects) {
                foreach (var e in effects) {
                    e(d);
                }
            }
        }
    }
}
