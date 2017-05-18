using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public class DependencyEffectOptions : EffectOptions<Dependency> {
        public readonly Option SetBadOption = new Option("sb", "set-bad", "", "Set bad counter to overall counter", @default: false);
        public readonly Option SetQuestionableOption = new Option("sq", "set-Questionable", "", "Set questionable counter to overall counter", @default: false);

        public DependencyEffectOptions() : base("dependency") {
        }

        public virtual IEnumerable<Option> AllOptions => BaseOptions.Concat(new[] { SetBadOption, SetQuestionableOption });

        protected internal override IEnumerable<Action<Dependency>> Parse([NotNull] GlobalContext globalContext, 
            [CanBeNull] string argsAsString, string defaultReasonForSetBad, bool ignoreCase,
            [NotNull] [ItemNotNull] IEnumerable<OptionAction> moreOptionActions) {
            var localResult = new List<Action<Dependency>>();
            IEnumerable<Action<Dependency>> baseResult = base.Parse(globalContext, argsAsString, defaultReasonForSetBad, ignoreCase, new[] {
                SetBadOption.Action((args, j) => {
                    localResult.Add(d => d.MarkAsBad(SetBadOption.Name));
                    return j;
                }),
                SetQuestionableOption.Action((args, j) => {
                    localResult.Add(d => d.MarkAsQuestionable(SetQuestionableOption.Name));
                    return j;
                })
            }.Concat(moreOptionActions).ToArray());
            IEnumerable<Action<Dependency>> result = baseResult.Concat(localResult);
            return result.Any() ? result : new Action<Dependency>[] { d => d.MarkAsBad(defaultReasonForSetBad) };
        }
    }
}