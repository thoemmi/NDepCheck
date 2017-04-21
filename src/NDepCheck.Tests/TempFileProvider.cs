using System;
using System.IO;

namespace NDepCheck.Tests {
    public class TempFileProvider : IDisposable {
        private bool _doDelete = true;
        public string Filename { get; }

        public TempFileProvider Keep {
            get {
                _doDelete = false;
                return this;
            }
        }

        public TempFileProvider(string fileName) {
            Filename = fileName;
        }

        public override string ToString() {
            return Filename;
        }

        public void Dispose() {
            if (_doDelete) {
                File.Delete(Filename);
            }
        }
    }
}