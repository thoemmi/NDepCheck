using System;
using System.IO;

namespace NDepCheck {
    public class NamedTextWriter : IDisposable {
        public NamedTextWriter(TextWriter writer, string fileName) {
            Writer = writer;
            FileName = fileName;
        }

        public TextWriter Writer {
            get;
        }
        public string FileName {
            get;
        }

        public bool IsConsole => FileName == null;

        public void Dispose() {
            Writer?.Dispose();
        }
    }
}