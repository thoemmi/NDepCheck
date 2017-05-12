using System;
using System.IO;

namespace NDepCheck.Tests {
    public class DisposingFile : IDisposable {
        private bool _doDelete = true;
        public string Filename { get; }

        public DisposingFile Keep {
            get {
                _doDelete = false;
                return this;
            }
        }

        private DisposingFile(string filename) {
            Filename = filename;
        }

        public override string ToString() {
            return Filename;
        }

        public void Dispose() {
            if (_doDelete) {
                File.Delete(Filename);
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