using System;

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Reading {
    public interface IReaderFactory : IPlugin {
        AbstractDependencyReader CreateReader(string filename, GlobalContext options, bool needsOnlyItemTails);
    }

    public abstract class AbstractReaderFactory : IReaderFactory {
        //private static readonly IReaderFactory[] ALL_READER_FACTORIES = {
        //    new DotNetAssemblyDependencyReaderFactory(),
        //    new DipReaderFactory(),
        //    new IgnoreReaderFactory()
        //};

        //public static AbstractDependencyReader CreateMatchingReader(string filename, GlobalContext options, bool needsOnlyItemTails) {
        //    string extension = GetExtensionForItemType(filename);
        //    foreach (var f in ALL_READER_FACTORIES) {
        //        if (f.Accepts(extension)) {
        //            return f.CreateReader(filename, options, needsOnlyItemTails);
        //        }
        //    }
        //    throw new ArgumentException("Extension " + extension + " is not supported for files with dependencies");
        //}

        ////private static string GetExtensionForItemType(string filename) {
        ////    return (Path.GetExtension(filename) ?? "").TrimStart('.');
        ////}

        ////public static ItemType GetDefaultDescriptor(string rulefilename, [CanBeNull] string ruleFileExtension) {
        ////    string extension = GetExtensionForItemType(string.IsNullOrWhiteSpace(ruleFileExtension) ? rulefilename : rulefilename.Replace(ruleFileExtension, ""));
        ////    return ALL_READER_FACTORIES
        ////        .Where(f => f.Accepts(extension))
        ////        .Select(f => f.GetDescriptors().FirstOrDefault())
        ////        .FirstOrDefault();
        ////}

        [NotNull]
        public abstract AbstractDependencyReader CreateReader([NotNull]string filename, [NotNull]GlobalContext options, bool needsOnlyItemTails);

        public string GetHelp(bool detailedHelp) {
            throw new NotImplementedException(); // TODO: !!!!!!!!!!!!!!!!!!!!!
        }
    }

    public abstract class AbstractDependencyReader {
        private InputContext _inputContext;
        [NotNull]
        protected readonly string _filename;


        protected AbstractDependencyReader([NotNull]string filename) {
            if (string.IsNullOrWhiteSpace(filename)) {
                throw new ArgumentException("filename must be non-empty", nameof(filename));
            }
            _filename = filename;
        }

        [NotNull]
        public string FileName => _filename;

        [NotNull]
        protected abstract IEnumerable<Dependency> ReadDependencies(InputContext inputContext, int depth);

        /// <summary>
        /// Read dependencies from file
        /// </summary>
        /// <param name="globalContext"></param>
        /// <param name="depth"></param>
        /// <returns><c>null</c> if already read in</returns>
        [CanBeNull]
        public InputContext ReadOrGetDependencies(GlobalContext globalContext, int depth) {
            if (_inputContext == null) {
                _inputContext = new InputContext(FileName);
                Dependency[] dependencies = ReadDependencies(_inputContext, depth).ToArray();
                if (!dependencies.Any()) {
                    Log.WriteWarning("No dependencies found in " + FileName);
                }
                return _inputContext;
            } else {
                return null;
            }
        }
    }
}