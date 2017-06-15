using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace NDepCheck.Tests {
    [ExcludeFromCodeCoverage]
    public class DisposingFile : IDisposable {
        private bool _doDelete = true;
        public string FileName { get; }

        public DisposingFile Keep {
            get {
                Console.WriteLine("Writing to " + FileName);
                _doDelete = false;
                return this;
            }
        }

        private DisposingFile(string fileName) {
            FileName = fileName;
        }

        public override string ToString() {
            return FileName;
        }

        public void Dispose() {
            if (_doDelete) {
                File.Delete(FileName);
            }
        }

        public static DisposingFile Create(string filename) {
            return new DisposingFile(filename);
        }

        public static DisposingFile CreateTempFileWithTail(string tail) {
            return new DisposingFile(Path.GetTempFileName() + tail);
        }
    }
}