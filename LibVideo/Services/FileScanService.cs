using System;
using System.Collections.Generic;
using System.IO;

namespace LibVideo.Services
{
    public class FileScanService : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        public event EventHandler FilesChanged;

        public void SetupWatchers(IEnumerable<string> directories)
        {
            DisposeWatchers();

            foreach (var dir in directories)
            {
                if (Directory.Exists(dir))
                {
                    var watcher = new FileSystemWatcher(dir);
                    watcher.IncludeSubdirectories = false;
                    watcher.Created += Watcher_Changed;
                    watcher.Deleted += Watcher_Changed;
                    watcher.Renamed += Watcher_Changed;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
            }
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            FilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DisposeWatchers()
        {
            foreach (var w in _watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            _watchers.Clear();
        }

        public void Dispose()
        {
            DisposeWatchers();
        }
    }
}
