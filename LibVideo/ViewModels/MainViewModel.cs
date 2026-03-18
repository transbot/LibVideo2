using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVideo.Models;
using LibVideo.Data;
using Microsoft.Win32;

namespace LibVideo.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly DatabaseManager _dbManager;
        private readonly string configPath = "directories.txt";
        private readonly string potplayerPath = GetPotPlayerPath();

        public MainViewModel()
        {
            _dbManager = new DatabaseManager();
            Directories = new ObservableCollection<string>();
            VideoItems = new ObservableCollection<VideoItem>();
            SearchHistory = new ObservableCollection<string>();
            
            AddDirectoryCommand = new AsyncRelayCommand<string>(AddDirectory);
            DeleteSelectedDirectoryCommand = new AsyncRelayCommand(DeleteSelectedDirectory);
            CommitSearchCommand = new RelayCommand(CommitSearch);
            PrevSearchCommand = new RelayCommand(PrevSearch);
            NextSearchCommand = new RelayCommand(NextSearch);
            OpenMediaCommand = new RelayCommand<VideoItem>(OpenMedia);
            OpenContainingFolderCommand = new RelayCommand<VideoItem>(OpenContainingFolder);

            LoadDirectories();
            _ = InitializeDataAsync();
        }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        private string searchKeyword = "";
        public string SearchKeyword
        {
            get => searchKeyword;
            set
            {
                SetProperty(ref searchKeyword, value);
                FilterItems();
            }
        }

        private string totalItemsText;
        public string TotalItemsText
        {
            get => totalItemsText;
            set => SetProperty(ref totalItemsText, value);
        }
        
        private string itemsAddedLast24HoursText;
        public string ItemsAddedLast24HoursText
        {
            get => itemsAddedLast24HoursText;
            set => SetProperty(ref itemsAddedLast24HoursText, value);
        }

        private string selectedDirectory;
        public string SelectedDirectory
        {
            get => selectedDirectory;
            set => SetProperty(ref selectedDirectory, value);
        }

        public ObservableCollection<string> Directories { get; }
        public ObservableCollection<VideoItem> VideoItems { get; }
        public ObservableCollection<string> SearchHistory { get; }

        public IAsyncRelayCommand<string> AddDirectoryCommand { get; }
        public IAsyncRelayCommand DeleteSelectedDirectoryCommand { get; }
        public IRelayCommand CommitSearchCommand { get; }
        public IRelayCommand PrevSearchCommand { get; }
        public IRelayCommand NextSearchCommand { get; }
        public IRelayCommand<VideoItem> OpenMediaCommand { get; }
        public IRelayCommand<VideoItem> OpenContainingFolderCommand { get; }

        private List<VideoItem> _allDatabaseItems = new List<VideoItem>();
        private int searchHistoryIndex = -1;

        private void LoadDirectories()
        {
            if (File.Exists(configPath))
            {
                var dirs = File.ReadAllLines(configPath);
                foreach (var d in dirs) Directories.Add(d);
            }
        }

        private void SaveDirectories()
        {
            File.WriteAllLines(configPath, Directories);
        }

        private async Task AddDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directories.Contains(path))
            {
                Directories.Add(path);
                SaveDirectories();
                await RefreshFromDiskAsync();
            }
        }

        private async Task DeleteSelectedDirectory()
        {
            if (!string.IsNullOrEmpty(SelectedDirectory))
            {
                _dbManager.RemoveDirectoryItems(SelectedDirectory);
                Directories.Remove(SelectedDirectory);
                SaveDirectories();
                await LoadFromDatabaseAsync();
            }
        }

        private async Task InitializeDataAsync()
        {
            await LoadFromDatabaseAsync();
            _ = RefreshFromDiskAsync();
        }

        private async Task LoadFromDatabaseAsync()
        {
            IsLoading = true;
            await Task.Run(() =>
            {
                _allDatabaseItems = _dbManager.GetAllItems();
            });
            
            FilterItems();
            UpdateStatusBar();
            IsLoading = false;
        }

        private async Task RefreshFromDiskAsync()
        {
            IsLoading = true;
            var currentFilesList = new List<VideoItem>();

            await Task.Run(() =>
            {
                foreach (var dir in Directories)
                {
                    if (Directory.Exists(dir))
                    {
                        var di = new DirectoryInfo(dir);
                        try {
                            foreach (var fi in di.EnumerateFiles("*.*"))
                            {
                                if (IsMediaFile(fi.Extension))
                                    currentFilesList.Add(new VideoItem { FileName = fi.Name, FolderName = fi.DirectoryName, FullName = fi.FullName });
                            }
                            foreach (var sdi in di.EnumerateDirectories())
                            {
                                currentFilesList.Add(new VideoItem { FileName = sdi.Name, FolderName = sdi.Parent.FullName, FullName = sdi.FullName });
                            }
                        } catch { }
                    }
                }
                _dbManager.InsertOrUpdateItems(currentFilesList);
            });
            
            await LoadFromDatabaseAsync();
        }

        private void FilterItems()
        {
            var filter = SearchKeyword?.ToLower() ?? "";
            var filtered = string.IsNullOrEmpty(filter) ? 
                _allDatabaseItems : 
                _allDatabaseItems.Where(i => i.FileName.ToLower().Contains(filter)).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                VideoItems.Clear();
                int id = 1;
                foreach (var item in filtered)
                {
                    item.Id = id++;
                    VideoItems.Add(item);
                }
            });
        }
        
        private void CommitSearch()
        {
            string text = SearchKeyword?.Trim() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                if (SearchHistory.Count == 0 || SearchHistory.Last() != text)
                {
                    SearchHistory.Add(text);
                    if (SearchHistory.Count > 5)
                    {
                        SearchHistory.RemoveAt(0);
                    }
                }
                searchHistoryIndex = SearchHistory.Count;
            }
            FilterItems();
        }

        private void PrevSearch()
        {
            if (SearchHistory.Count > 0 && searchHistoryIndex > 0)
            {
                searchHistoryIndex--;
                SearchKeyword = SearchHistory[searchHistoryIndex];
            }
        }

        private void NextSearch()
        {
            if (SearchHistory.Count > 0 && searchHistoryIndex < SearchHistory.Count - 1)
            {
                searchHistoryIndex++;
                SearchKeyword = SearchHistory[searchHistoryIndex];
            }
            else if (SearchHistory.Count > 0 && searchHistoryIndex == SearchHistory.Count - 1)
            {
                searchHistoryIndex++;
                SearchKeyword = "";
            }
        }

        private void UpdateStatusBar()
        {
            TotalItemsText = $"媒体文件或文件夹共计: {_allDatabaseItems.Count} 个";
        }

        private void OpenMedia(VideoItem item)
        {
            if (item == null) return;
            string filePath = item.FullName;
            
            if (IsIsoFile(filePath) || ContainsIsoFile(filePath))
            {
                string isoFilePath = filePath;
                if (Directory.Exists(filePath))
                {
                    isoFilePath = Directory.GetFiles(filePath, "*.iso", SearchOption.TopDirectoryOnly).FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(isoFilePath))
                {
                    if (potplayerPath != null)
                        Process.Start(potplayerPath, isoFilePath);
                    else
                        Process.Start(new ProcessStartInfo(isoFilePath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("No ISO file found in the specified directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (File.Exists(filePath) || Directory.Exists(filePath))
            {
                if (potplayerPath != null && !IsIsoFile(filePath))
                    Process.Start(potplayerPath, filePath);
                else
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("The file path does not exist or is not valid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenContainingFolder(VideoItem item)
        {
            if (item == null) return;
            string filePath = item.FullName;

            if (File.Exists(filePath))
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            else if (Directory.Exists(filePath))
                Process.Start("explorer.exe", $"\"{filePath}\"");
            else
                MessageBox.Show("指定的文件或目录不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private bool IsIsoFile(string filePath) => Path.GetExtension(filePath).Equals(".iso", StringComparison.OrdinalIgnoreCase);

        private bool ContainsIsoFile(string directoryPath) => Directory.Exists(directoryPath) && Directory.GetFiles(directoryPath, "*.iso", SearchOption.TopDirectoryOnly).Any();

        private bool IsMediaFile(string extension)
        {
            string[] mediaExtensions = { ".avi", ".mov", ".mkv", ".wmv", ".flac", ".iso", ".mp3", ".mp4" };
            return mediaExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetPotPlayerPath()
        {
            RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(@"Applications\PotPlayerMini64.exe\shell\open\command");
            string pathName = (string)registryKey?.GetValue(null);
            if (string.IsNullOrEmpty(pathName)) return null;

            int index = pathName.LastIndexOf(" ");
            return index != -1 ? pathName.Substring(0, index).Replace("\"", string.Empty) : null;
        }
    }
}
