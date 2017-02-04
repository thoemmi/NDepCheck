using System;

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NDepCheck {
    public interface IReaderFactory {
        IEnumerable<ItemType> GetDescriptors();
        bool Accepts(string extension);
        AbstractDependencyReader CreateReader(string filename, Options options, bool needsOnlyItemTails);
    }

    public abstract class AbstractReaderFactory : IReaderFactory {
        private static readonly IReaderFactory[] ALL_READER_FACTORIES = {
            new DotNetAssemblyDependencyReaderFactory(),
            new DipReaderFactory(),
            new IgnoreReaderFactory()
        };

        public static ItemType GetItemType(string definition) {
            IEnumerable<string> parts = definition.Split('(', ':', ')').Select(s => s.Trim()).Where(s => s != "");
            string name = parts.First();
            return ALL_READER_FACTORIES.SelectMany(f => f.GetDescriptors()).FirstOrDefault(d => d.Name == name)
                ?? ALL_READER_FACTORIES.OfType<DotNetAssemblyDependencyReaderFactory>().First().GetOrCreateDotNetType(name, parts.Skip(1));
        }

        public static AbstractDependencyReader CreateReader(string filename, string extensionOrNull, Options options, bool needsOnlyItemTails) {
            string extension = GetExtensionForItemType(filename, extensionOrNull);
            foreach (var f in ALL_READER_FACTORIES) {
                if (f.Accepts(extension)) {
                    return f.CreateReader(filename, options, needsOnlyItemTails);
                }
            }
            throw new ArgumentException("Extension " + extension + " is not supported for files with dependencies");
        }

        private static string GetExtensionForItemType(string filename, string extensionOrNull) {
            return (extensionOrNull ?? Path.GetExtension(filename) ?? "").TrimStart('.');
        }

        public static ItemType GetDefaultDescriptor(string rulefilename) {
            string extension = GetExtensionForItemType(rulefilename.Replace(".dep", ""), null);
            foreach (var f in ALL_READER_FACTORIES) {
                if (f.Accepts(extension)) {
                    return f.GetDescriptors().FirstOrDefault();
                }
            }
            return null;
        }

        public abstract IEnumerable<ItemType> GetDescriptors();
        public abstract bool Accepts(string extension);
        public abstract AbstractDependencyReader CreateReader(string filename, Options options, bool needsOnlyItemTails);
    }

    public abstract class AbstractDependencyReader {
        private Dependency[] _dependencies;
        protected readonly string _filename;

        protected AbstractDependencyReader(string filename) {
            if (string.IsNullOrWhiteSpace(filename)) {
                throw new ArgumentException("filename must be non-empty", nameof(filename));
            }
            _filename = filename;
        }

        public string FileName => _filename;

        protected abstract IEnumerable<Dependency> ReadDependencies();

        public Dependency[] ReadOrGetDependencies() {
            if (_dependencies == null) {
                _dependencies = ReadDependencies().ToArray();
            }
            return _dependencies;
        }
    }
}