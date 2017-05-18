using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming {
    public class ExtendedDependencyEffectOptions : DependencyEffectOptions {
        public readonly Option IncrementBadOption = new Option("i!", "increment-bad", "",
            "Increment bad counter by 1", @default: "", multiple: true);

        public readonly Option ResetBadOption = new Option("r!", "reset-bad", "", "Reset bad counter to 0", @default: "");

        public readonly Option IncrementQuestionableOption = new Option("i?", "increment-questionable", "",
            "Increment questionable counter by 1", @default: "", multiple: true);

        public readonly Option ResetQuestionableOption = new Option("r?", "reset-questionable", "",
            "Reset questionable counter to 0", @default: "");

        public override IEnumerable<Option> AllOptions =>
            base.AllOptions.Concat(new[] {
                IncrementBadOption, ResetBadOption,
                IncrementQuestionableOption, ResetQuestionableOption
            });

        protected internal override IEnumerable<Action<Dependency>> Parse([NotNull] GlobalContext globalContext, 
                [CanBeNull] string argsAsString, string defaultReasonForSetBad, bool ignoreCase, 
                [NotNull] [ItemNotNull] IEnumerable<OptionAction> moreOptionActions) {
            var localResult = new List<Action<Dependency>>();
            IEnumerable<Action<Dependency>> baseResult = base.Parse(globalContext, argsAsString, defaultReasonForSetBad, ignoreCase, new[] {
                IncrementBadOption.Action((args, j) => {
                    localResult.Add(d => d.IncrementBad(IncrementBadOption.Name));
                    return j;
                }),
                ResetBadOption.Action((args, j) => {
                    localResult.Add(d => d.ResetBad());
                    return j;
                }),
                SetQuestionableOption.Action((args, j) => {
                    localResult.Add(d => d.MarkAsQuestionable(SetQuestionableOption.Name));
                    return j;
                }),
                IncrementQuestionableOption.Action((args, j) => {
                    localResult.Add(d => d.IncrementQuestionable(IncrementQuestionableOption.Name));
                    return j;
                }),
                ResetQuestionableOption.Action((args, j) => {
                    localResult.Add(d => d.ResetQuestionable());
                    return j;
                })
            }.Concat(moreOptionActions).ToArray());
            return baseResult.Concat(localResult);
        }
    }
}