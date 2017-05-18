using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Matching {
    public class DependencyMatchOptions {
        public readonly Option DependencyMatchOption;
        public readonly Option NoMatchOption;

        public DependencyMatchOptions(string verb) {
            DependencyMatchOption = new Option("dm", "dependency-match", "&", $"Match to select dependencies to {verb}",
                @default : $"{verb} all dependencies", multiple : true);
            NoMatchOption = new Option("nm", "no-match", "&", $"Edges not to {verb}", @default : "no excluded dependencies", multiple : true);
        }

        public Option[] WithOptions(params Option[] moreOptions) {
            return new[] {DependencyMatchOption, NoMatchOption}.Concat(moreOptions).ToArray();
        }

        public void Parse([NotNull] GlobalContext globalContext, [CanBeNull] string argsAsString, bool ignoreCase,
            [NotNull] [ItemNotNull] List<DependencyMatch> matches, [NotNull] [ItemNotNull] List<DependencyMatch> excludes,
            [NotNull] [ItemNotNull] params OptionAction[] moreOptions) {
            Option.Parse(globalContext, argsAsString, new[] {
                DependencyMatchOption.Action((args, j) => {
                    string pattern = Option.ExtractRequiredOptionValue(args, ref j, "missing dependency match pattern",
                        allowOptionValue : true);
                    matches.Add(DependencyMatch.Create(pattern, ignoreCase));
                    return j;
                }),
                NoMatchOption.Action((args, j) => {
                    string pattern = Option.ExtractRequiredOptionValue(args, ref j, "missing dependency match pattern",
                        allowOptionValue : true);
                    excludes.Add(DependencyMatch.Create(pattern, ignoreCase));
                    return j;
                })
            }.Concat(moreOptions).ToArray());
        }
    }
}