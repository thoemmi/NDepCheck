using System;
using System.IO;

namespace NDepCheck {
    public interface ITargetWriter : IDisposable {
        void WriteLine();
        void WriteLine(string s);
        void Write(string s);
        void Write(char c);
    }

    public class TargetStreamWriter : ITargetWriter {
        private readonly StreamWriter _sw;

        public TargetStreamWriter(Stream s) {
            _sw = new StreamWriter(s);
        }

        public void Dispose() {
            _sw.Dispose();
        }

        public void WriteLine() {
            _sw.WriteLine();
        }

        public void WriteLine(string s) {
            _sw.WriteLine(s);
        }

        public void Write(string s) {
            _sw.Write(s);
        }

        public void Write(char c) {
            _sw.Write(c);
        }
    }

    public class WriteTarget {
        private class InnerTargetWriter : ITargetWriter {
            private readonly WriteTarget _parent;
            private int _limitLines;
            private readonly TextWriter _sw;

            public InnerTargetWriter(WriteTarget parent, bool logToConsoleInfo, int limitLinesForConsole) {
                _parent = parent;

                if (!parent.IsConsoleOut) {
                    _limitLines = int.MaxValue;
                    Log.WriteInfo($"... writing to {parent.FullFileName}");
                    _sw = new StreamWriter(new FileStream(parent.FullFileName, parent.Append ? FileMode.Append : FileMode.Create));
                } else {
                    _limitLines = limitLinesForConsole;
                    if (logToConsoleInfo) {
                        Log.WriteInfo("... writing to console");
                    }
                    _sw = Console.Out;
                }
            }

            public void WriteLine() {
                _limitLines--;
                if (_limitLines >= 0) {
                    _sw.WriteLine();
                }
            }

            public void WriteLine(string s) {
                _limitLines--;
                if (_limitLines >= 0) {
                    _sw.WriteLine(s);
                }
            }

            public void Write(string s) {
                if (_limitLines >= 0) {
                    _sw.Write(s);
                }
            }

            public void Write(char c) {
                if (_limitLines >= 0) {
                    _sw.Write(c);
                }
            }

            public void Dispose() {
                if (_limitLines < 0) {
                    _sw.WriteLine($"... and {-_limitLines} more lines that were not written (use >! to write all)");
                }

                if (!_parent.IsConsoleOut) {
                    _sw.Dispose();
                }
            }
        }

        private readonly int _limitLinesForConsole;

        public WriteTarget(string fileName, bool append, int limitLinesForConsole) {
            _limitLinesForConsole = limitLinesForConsole;
            FileName = fileName;
            Append = append;
        }

        public override string ToString() {
            return FullFileName;
        }

        public string FileName {
            get;
        }

        public bool Append {
            get;
        }

        public string FullFileName => IsConsoleOut ? "console" : Path.GetFullPath(FileName);

        public bool IsConsoleOut => string.IsNullOrWhiteSpace(FileName) || FileName == "-";

        public WriteTarget ChangeExtension(string extension) {
            return new WriteTarget(Path.ChangeExtension(FullFileName, extension), Append, _limitLinesForConsole);
        }

        public ITargetWriter CreateWriter(bool logToConsoleInfo = false) {
            return new InnerTargetWriter(this, logToConsoleInfo, _limitLinesForConsole);
        }
    }
}