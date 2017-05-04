using System.Collections.Generic;
using System.IO;
using System.Threading;
using JetBrains.Annotations;

namespace NDepCheck {
    public class FileWatcher {
        [NotNull]
        private readonly Program _program;

        [NotNull]
        public string FullScriptName { get; }

        [NotNull]
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();

        private bool _triggered;
        private bool _stopped;

        public FileWatcher([NotNull]string fullScriptName, [NotNull] Program program) {
            FullScriptName = fullScriptName;
            _program = program;
            // Each FileWatcher watches its own script
            AddFile(FullScriptName);
            new Thread(Run) { Name = fullScriptName, IsBackground = true }.Start();
        }

        public void AddFile(string fullFileName) {
            if (!_watchers.ContainsKey(fullFileName)) {
                var watcher = new FileSystemWatcher(Path.GetDirectoryName(fullFileName) ?? "");
                watcher.Changed += (o, e) => Trigger(e.FullPath);
                watcher.EnableRaisingEvents = true;
                _watchers.Add(fullFileName, watcher);
            }
        }

        public void RemoveFile(string fullFileName) {
            if (_watchers.ContainsKey(fullFileName)) {
                _watchers[fullFileName].Dispose();
                _watchers.Remove(fullFileName);
            }
        }

        private void Trigger(string filename) {
            if (_watchers.ContainsKey(filename)) {
                lock (_watchers) {
                    _triggered = true;
                    Monitor.PulseAll(_watchers);
                }
            }
        }

        private void Run() {
            for (;;) {
                lock (_watchers) {
                    while (!_triggered) {
                        Monitor.Wait(_watchers);
                    }
                }
                if (_stopped) {
                    return;
                }
                // Triggers inside 2 secs are "captured" in this run; later triggers will run the script again.
                Thread.Sleep(2000);
                _triggered = false;
                var writtenMasterFiles = new List<string>();
                _program.RunFromFile(FullScriptName, new string[0], new GlobalContext(), writtenMasterFiles, logCommands: true, onlyShowParameters: false);
                _program.WriteWrittenMasterFiles(writtenMasterFiles);
            }
        }

        public void Close() {
            lock (_watchers) {
                _stopped = true;
                Monitor.PulseAll(_watchers);
            }
            foreach (var w in _watchers.Values) {
                w.Dispose();
            }
        }
    }
}