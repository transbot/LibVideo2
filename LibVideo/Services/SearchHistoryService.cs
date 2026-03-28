using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using LibVideo.Helpers;

namespace LibVideo.Services
{
    public class SearchHistoryService
    {
        public ObservableCollection<string> History { get; } = new ObservableCollection<string>();
        private int _historyIndex = -1;

        public SearchHistoryService()
        {
            Load();
        }

        private void Load()
        {
            if (File.Exists(AppPaths.SearchHistoryFile))
            {
                try
                {
                    var lines = File.ReadAllLines(AppPaths.SearchHistoryFile);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            History.Add(line);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load search history");
                }
            }
        }

        public void Save()
        {
            try { File.WriteAllLines(AppPaths.SearchHistoryFile, History); }
            catch (Exception ex) { Logger.Error(ex, "Failed to save search history"); }
        }

        public void CommitSearchText(string text)
        {
            text = text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                var existingItems = History.Where(s => s.Equals(text, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var item in existingItems)
                {
                    History.Remove(item);
                }
                
                History.Insert(0, text);
                
                if (History.Count > 10)
                {
                    History.RemoveAt(History.Count - 1);
                }
                _historyIndex = -1;
                Save();
            }
        }

        public string GetPrevious()
        {
            if (History.Count > 0 && _historyIndex < History.Count - 1)
            {
                _historyIndex++;
                return History[_historyIndex];
            }
            return null;
        }

        public string GetNext()
        {
            if (History.Count > 0)
            {
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    return History[_historyIndex];
                }
                else if (_historyIndex == 0)
                {
                    _historyIndex = -1;
                    return "";
                }
            }
            return null;
        }

        public void ResetIndex()
        {
            _historyIndex = -1;
        }
    }
}
