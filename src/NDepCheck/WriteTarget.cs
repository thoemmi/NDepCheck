using System;
using System.IO;

namespace NDepCheck {
    public class WriteTarget {
        public WriteTarget(string fileName, bool append) {
            FileName = fileName;
            Append = append;
        }

        public override string ToString() {
            return FullFileName;
        }

        public string FileName { get; }

        public bool Append { get; }

        public string FullFileName => IsConsoleOut ? "console" : Path.GetFullPath(FileName);

        public bool IsConsoleOut => string.IsNullOrWhiteSpace(FileName) || FileName == "-";

        public WriteTarget ChangeExtension(string extension) {
            return new WriteTarget(Path.ChangeExtension(FullFileName, extension), Append);
        }

        public TextWriter CreateTextWriter(bool logToConsoleInfo = false) {
            if (!IsConsoleOut) {
                Log.WriteInfo($"... writing to {FullFileName}");
                return new StreamWriter(new FileStream(FullFileName, Append ? FileMode.Append : FileMode.Create));
            } else {
                if (logToConsoleInfo) {
                    Log.WriteInfo("... writing to console");
                }
                return Console.Out;
            }
        }
    }
}