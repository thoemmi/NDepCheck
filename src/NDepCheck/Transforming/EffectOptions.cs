using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public abstract class EffectOptions<T> where T : ObjectWithMarkers {
        public readonly Option AddMarkerOption;
        public readonly Option RemoveMarkerOption;
        public readonly Option DeleteOption;

        public static readonly Action<T> DELETE_ACTION_MARKER = x => { };

        protected EffectOptions([NotNull] string sort) {
            AddMarkerOption = new Option("am", "add-marker", "marker", "add a marker to the " + sort, @default: "", multiple: true);
            RemoveMarkerOption = new Option("rm", "remove-marker", "marker", "remove a marker from the " + sort, @default: "", multiple: true);
            DeleteOption = new Option("d" + sort.First(), "delete-" + sort, "", "delete the " + sort, @default: "keep " + sort);
        }

        protected IEnumerable<Option> BaseOptions => new[] { AddMarkerOption, RemoveMarkerOption, DeleteOption };

        protected internal virtual IEnumerable<Action<T>> Parse(string argsAsString,
            [NotNull, ItemNotNull] IEnumerable<OptionAction> moreOptions) {
            var result = new List<Action<T>>();
            Option.Parse(argsAsString, new[] {
                AddMarkerOption.Action((args, j) => {
                    string marker = Option.ExtractOptionValue(args, ref j);
                    result.Add(item => item.AddMarker(marker));
                    return j;
                }),
                RemoveMarkerOption.Action((args, j) => {
                    string marker = Option.ExtractOptionValue(args, ref j);
                    result.Add(item => item.RemoveMarker(marker));
                    return j;
                }),
                DeleteOption.Action((args, j) => {
                    result.Add(DELETE_ACTION_MARKER);
                    return j;
                })
            }.Concat(moreOptions).ToArray());
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

    public class ItemEffectOptions : EffectOptions<Item> {
        public ItemEffectOptions() : base("item") {
        }

        public IEnumerable<Option> AllOptions => BaseOptions;
    }

    public class DepencencyEffectOptions : EffectOptions<Dependency> {
        public readonly Option SetBadOption = new Option("s!", "set-bad", "", "Set bad counter to edge counter", @default: "");

        public readonly Option IncrementBadOption = new Option("i!", "increment-bad", "",
            "Increment bad counter by 1", @default: "", multiple: true);

        public readonly Option ResetBadOption = new Option("r!", "reset-bad", "", "Reset bad counter to 0", @default: "");

        public readonly Option SetQuestionableOption = new Option("s?", "set-questionable", "",
            "Set questionable counter to edge counter", @default: "");

        public readonly Option IncrementQuestionableOption = new Option("i?", "increment-questionable", "",
            "Increment questionable counter by 1", @default: "", multiple: true);

        public readonly Option ResetQuestionableOption = new Option("r?", "reset-questionable", "",
            "Reset questionable counter to 0", @default: "");

        public DepencencyEffectOptions() : base("dependency") {
        }

        public IEnumerable<Option> AllOptions =>
                BaseOptions.Concat(new[] {
                    SetBadOption, IncrementBadOption, ResetBadOption, SetQuestionableOption,
                    IncrementQuestionableOption, ResetQuestionableOption
                });

        protected internal override IEnumerable<Action<Dependency>> Parse([NotNull] string argsAsString,
                [NotNull, ItemNotNull] IEnumerable<OptionAction> moreOptions) {
            var localResult = new List<Action<Dependency>>();
            IEnumerable<Action<Dependency>> baseResult = base.Parse(argsAsString,
                new[] {
                    SetBadOption.Action((args, j) => {
                        localResult.Add(d => d.MarkAsBad());
                        return j;
                    }),
                    IncrementBadOption.Action((args, j) => {
                        localResult.Add(d => d.IncrementBad());
                        return j;
                    }),
                    ResetBadOption.Action((args, j) => {
                        localResult.Add(d => d.ResetBad());
                        return j;
                    }),
                    SetQuestionableOption.Action((args, j) => {
                        localResult.Add(d => d.MarkAsQuestionable());
                        return j;
                    }),
                    IncrementQuestionableOption.Action((args, j) => {
                        localResult.Add(d => d.IncrementQuestionable());
                        return j;
                    }),
                    ResetQuestionableOption.Action((args, j) => {
                        localResult.Add(d => d.ResetQuestionable());
                        return j;
                    })
                }.Concat(moreOptions).ToArray());
            IEnumerable<Action<Dependency>> result = baseResult.Concat(localResult);
            return result.Any() ? result : new Action<Dependency>[] { d => d.MarkAsBad() };
        }
    }
}
