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
using System.Windows.Input;

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
            SelectPlayerCommand = new RelayCommand(SelectPlayer);
            RefreshCacheCommand = new AsyncRelayCommand(RefreshCacheAsync);

            LoadDirectories();
            if (File.Exists("player.txt")) customPlayerPath = File.ReadAllText("player.txt");
            _ = InitializeDataAsync();
        }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        private string maxTypedKeyword = "";
        private string searchKeyword = "";
        public string SearchKeyword
        {
            get => searchKeyword;
            set
            {
                if (SetProperty(ref searchKeyword, value))
                {
                    bool isDeleting = Keyboard.IsKeyDown(Key.Back) || Keyboard.IsKeyDown(Key.Delete);
                    
                    if (isDeleting)
                    {
                        // 用户主动执行了清除/退格，立即丢弃所有的记录准备（不要缓存残缺片段）
                        maxTypedKeyword = "";
                    }
                    else if (!string.IsNullOrEmpty(value))
                    {
                        if (value.Length >= maxTypedKeyword.Length || !value.StartsWith(maxTypedKeyword, StringComparison.OrdinalIgnoreCase))
                        {
                            maxTypedKeyword = value;
                        }
                    }
                    else if (string.IsNullOrEmpty(value) && !string.IsNullOrWhiteSpace(maxTypedKeyword))
                    {
                        // 点击X按钮（值变空且不是通过退格/Delete发生）
                        CommitSearchText(maxTypedKeyword);
                        maxTypedKeyword = "";
                    }
                    FilterItems();
                }
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

        private string customPlayerPath;
        public string CustomPlayerPath
        {
            get => customPlayerPath;
            set 
            {
                if (SetProperty(ref customPlayerPath, value))
                {
                    File.WriteAllText("player.txt", value ?? "");
                }
            }
        }

        private VideoMetadata currentMetadata;
        public VideoMetadata CurrentMetadata
        {
            get => currentMetadata;
            set
            {
                SetProperty(ref currentMetadata, value);
                OnPropertyChanged(nameof(IsMetadataVisible));
            }
        }
        public bool IsMetadataVisible => CurrentMetadata != null;

        private VideoItem selectedVideo;
        public VideoItem SelectedVideo
        {
            get => selectedVideo;
            set
            {
                if (SetProperty(ref selectedVideo, value))
                {
                    if (value != null)
                        LoadMetadata(value.FullName);
                    else
                        CurrentMetadata = null;
                }
            }
        }

        private async void LoadMetadata(string path)
        {
            var item = _allDatabaseItems.FirstOrDefault(v => v.FullName == path);
            if (item != null && item.HasScraped)
            {
                if (!string.IsNullOrEmpty(item.MetaTitle) || !string.IsNullOrEmpty(item.MetaPlot))
                {
                    CurrentMetadata = new VideoMetadata
                    {
                        Title = item.MetaTitle,
                        Plot = item.MetaPlot,
                        Genre = item.MetaGenre,
                        PosterPath = item.MetaPosterPath
                    };
                }
                else
                {
                    CurrentMetadata = null;
                }
                return;
            }

            var fetchedMeta = await MetadataService.GetMetadataAsync(path);
            CurrentMetadata = fetchedMeta;

            if (item != null)
            {
                if (fetchedMeta != null)
                {
                    item.MetaTitle = fetchedMeta.Title;
                    item.MetaPlot = fetchedMeta.Plot;
                    item.MetaGenre = fetchedMeta.Genre;
                    item.MetaPosterPath = fetchedMeta.PosterPath;
                }
                item.HasScraped = true;
                
                // Fire-and-forget DB update in background thread
                _ = Task.Run(() => _dbManager.UpdateItemMetadata(item));
            }
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
        public IRelayCommand SelectPlayerCommand { get; }
        public IAsyncRelayCommand RefreshCacheCommand { get; }

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

        private async Task RefreshCacheAsync()
        {
            if (MessageBox.Show("确定要清空所有已脱机保存的海报与文本缓存，并重新全盘同步吗？\n（您添加的目录将会被完好保留）", "清空元数据缓存", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                IsLoading = true;
                // Delete everything in LiteDB "videos" collection
                await Task.Run(() => _dbManager.ClearItems());
                CurrentMetadata = null;
                // Re-scan from the existing directories
                await RefreshFromDiskAsync();
                IsLoading = false;
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
        
        private void CommitSearchText(string text)
        {
            text = text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                // 深度清除所有的重复项（极其严谨地预防任何冗余残留）
                var existingItems = SearchHistory.Where(s => s.Equals(text, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var item in existingItems)
                {
                    SearchHistory.Remove(item);
                }
                
                // 置顶最新搜索结果
                SearchHistory.Insert(0, text);
                
                if (SearchHistory.Count > 10)
                {
                    SearchHistory.RemoveAt(SearchHistory.Count - 1);
                }
                searchHistoryIndex = -1; // -1表示未在历史记录中选中任何一项
            }
        }

        private void CommitSearch()
        {
            CommitSearchText(SearchKeyword);
            maxTypedKeyword = SearchKeyword ?? "";
            FilterItems();
        }

        private void PrevSearch()
        {
            if (SearchHistory.Count > 0)
            {
                if (searchHistoryIndex < SearchHistory.Count - 1)
                {
                    searchHistoryIndex++;
                    // 为了防止触发setter中的重置逻辑，先临时关闭历史记录指针或直接赋予
                    SearchKeyword = SearchHistory[searchHistoryIndex];
                }
            }
        }

        private void NextSearch()
        {
            if (SearchHistory.Count > 0)
            {
                if (searchHistoryIndex > 0)
                {
                    searchHistoryIndex--;
                    SearchKeyword = SearchHistory[searchHistoryIndex];
                }
                else if (searchHistoryIndex == 0)
                {
                    searchHistoryIndex = -1;
                    SearchKeyword = "";
                }
            }
        }

        private void UpdateStatusBar()
        {
            TotalItemsText = $"媒体文件或文件夹共计: {_allDatabaseItems.Count} 个";
        }

        private void SelectPlayer()
        {
            var dlg = new OpenFileDialog { Filter = "Executable Files (*.exe)|*.exe", Title = "选择自定义播放器" };
            if (dlg.ShowDialog() == true)
            {
                CustomPlayerPath = dlg.FileName;
            }
        }

        private void OpenMedia(VideoItem item)
        {
            if (item == null) return;
            string filePath = item.FullName;
            string executable = !string.IsNullOrEmpty(CustomPlayerPath) && File.Exists(CustomPlayerPath) ? CustomPlayerPath : potplayerPath;
            
            if (IsIsoFile(filePath) || ContainsIsoFile(filePath))
            {
                string isoFilePath = filePath;
                if (Directory.Exists(filePath))
                {
                    isoFilePath = Directory.GetFiles(filePath, "*.iso", SearchOption.TopDirectoryOnly).FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(isoFilePath))
                {
                    if (executable != null)
                        Process.Start(executable, $"\"{isoFilePath}\"");
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
                if (executable != null && !IsIsoFile(filePath))
                    Process.Start(executable, $"\"{filePath}\"");
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
