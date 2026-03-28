using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVideo.Models;
using LibVideo.Data;
using LibVideo.Helpers;
using LibVideo.Services;
using System.Windows.Input;

namespace LibVideo.ViewModels
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private readonly DatabaseManager _dbManager;
        private readonly PlayerService _playerService;
        private readonly SearchHistoryService _searchHistoryService;
        private readonly FileScanService _fileScanService;
        private readonly Debouncer _searchDebouncer;
        private readonly Debouncer _fileChangeDebouncer;

        public MainViewModel()
        {
            _dbManager = new DatabaseManager();
            _playerService = new PlayerService(File.Exists(AppPaths.PlayerFile) ? File.ReadAllText(AppPaths.PlayerFile) : null);
            _searchHistoryService = new SearchHistoryService();
            _fileScanService = new FileScanService();
            
            _searchDebouncer = new Debouncer(TimeSpan.FromMilliseconds(300));
            _fileChangeDebouncer = new Debouncer(TimeSpan.FromSeconds(1));

            Directories = new ObservableCollection<string>();
            VideoItems = new ObservableCollection<VideoItem>();
            SearchHistory = _searchHistoryService.History;
            
            AddDirectoryCommand = new AsyncRelayCommand<string>(AddDirectory);
            DeleteSelectedDirectoryCommand = new AsyncRelayCommand(DeleteSelectedDirectory);
            CommitSearchCommand = new RelayCommand(CommitSearch);
            PrevSearchCommand = new RelayCommand(PrevSearch);
            NextSearchCommand = new RelayCommand(NextSearch);
            OpenMediaCommand = new RelayCommand<VideoItem>(OpenMedia);
            OpenContainingFolderCommand = new RelayCommand<VideoItem>(OpenContainingFolder);
            SelectPlayerCommand = new RelayCommand(SelectPlayer);
            RefreshCacheCommand = new AsyncRelayCommand(RefreshCacheAsync);
            OpenCoverImageCommand = new RelayCommand(() => IsImagePopupVisible = true);
            CloseCoverImageCommand = new RelayCommand(() => IsImagePopupVisible = false);
            SaveCoverImageCommand = new AsyncRelayCommand(SaveCoverImageAsync);

            _fileScanService.FilesChanged += (s, e) => 
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    _fileChangeDebouncer.Debounce(async () => await RefreshFromDiskAsync());
                });
            };

            Directories.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsHintVisible));

            LoadDirectories();
            _fileScanService.SetupWatchers(Directories);
            InitializeDataAsync().SafeFireAndForget();
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
                        CommitSearchText(maxTypedKeyword);
                        maxTypedKeyword = "";
                    }
                    
                    // Task 8 & 5: Debounce filtering for better performance
                    _searchDebouncer.Debounce(() => FilterItems());
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

        public string CustomPlayerPath
        {
            get => _playerService.CustomPlayerPath;
            set 
            {
                _playerService.CustomPlayerPath = value;
                OnPropertyChanged(nameof(CustomPlayerPath));
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

        private bool _isImagePopupVisible;
        public bool IsImagePopupVisible
        {
            get => _isImagePopupVisible;
            set => SetProperty(ref _isImagePopupVisible, value);
        }

        public bool IsHintVisible => Directories.Count == 0;

        private VideoItem selectedVideo;
        public VideoItem SelectedVideo
        {
            get => selectedVideo;
            set
            {
                if (SetProperty(ref selectedVideo, value))
                {
                    IsImagePopupVisible = false;
                    if (value != null)
                        LoadMetadataAsync(value.FullName).SafeFireAndForget();
                    else
                        CurrentMetadata = null;
                }
            }
        }

        private async Task LoadMetadataAsync(string path)
        {
            try
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
                            PosterPath = item.MetaPosterPath,
                            Rating = item.MetaRating
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
                        item.MetaRating = fetchedMeta.Rating;
                    }
                    item.HasScraped = true;
                    
                    Task.Run(() => _dbManager.UpdateItemMetadata(item)).SafeFireAndForget();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load metadata for {path}");
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
        public IRelayCommand OpenCoverImageCommand { get; }
        public IRelayCommand CloseCoverImageCommand { get; }
        public IAsyncRelayCommand SaveCoverImageCommand { get; }

        private List<VideoItem> _allDatabaseItems = new List<VideoItem>();

        private void LoadDirectories()
        {
            if (File.Exists(AppPaths.DirectoriesFile))
            {
                try
                {
                    var dirs = File.ReadAllLines(AppPaths.DirectoriesFile);
                    foreach (var d in dirs) Directories.Add(d);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load directories.");
                }
            }
        }

        private void SaveDirectories()
        {
            try { File.WriteAllLines(AppPaths.DirectoriesFile, Directories); }
            catch (Exception ex) { Logger.Error(ex, "Failed to save directories."); }
        }

        private async Task AddDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directories.Contains(path))
            {
                Directories.Add(path);
                SaveDirectories();
                _fileScanService.SetupWatchers(Directories);
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
                _fileScanService.SetupWatchers(Directories);
                await LoadFromDatabaseAsync();
            }
        }

        private async Task RefreshCacheAsync()
        {
            string msg = Application.Current.TryFindResource("DialogRefreshCacheMsg") as string ?? "Clear all cache?";
            string title = Application.Current.TryFindResource("DialogRefreshCacheTitle") as string ?? "Clear Cache";
            
            if (MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                IsLoading = true;
                await Task.Run(() => _dbManager.ClearItems());
                CurrentMetadata = null;
                await RefreshFromDiskAsync();
                IsLoading = false;
            }
        }

        private async Task InitializeDataAsync()
        {
            await LoadFromDatabaseAsync();
            RefreshFromDiskAsync().SafeFireAndForget();
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
            var scannedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                foreach (var dir in Directories)
                {
                    if (Directory.Exists(dir))
                    {
                        string safeDir = dir;
                        if (!safeDir.EndsWith("\\")) safeDir += "\\";
                        scannedRoots.Add(safeDir);
                        
                        var di = new DirectoryInfo(dir);
                        try {
                            foreach (var fi in di.EnumerateFiles("*.*"))
                            {
                                if (MediaExtensions.IsMediaFile(fi.Extension))
                                    currentFilesList.Add(new VideoItem { FileName = fi.Name, FolderName = fi.DirectoryName, FullName = fi.FullName });
                            }
                            foreach (var sdi in di.EnumerateDirectories())
                            {
                                currentFilesList.Add(new VideoItem { FileName = sdi.Name, FolderName = sdi.Parent.FullName, FullName = sdi.FullName });
                            }
                        } 
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"Directory enumeration failed for {dir}");
                        }
                    }
                }
                try
                {
                    _dbManager.SyncDiskItems(currentFilesList, scannedRoots);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to sync disk items to DB");
                }
            });
            
            await LoadFromDatabaseAsync();
        }

        private void FilterItems()
        {
            var filter = SearchKeyword?.ToLower() ?? "";
            var filtered = string.IsNullOrEmpty(filter) ? 
                _allDatabaseItems : 
                _allDatabaseItems.Where(i => 
                    i.FileName.ToLower().Contains(filter) || 
                    PinyinHelper.GetInitials(i.FileName).ToLower().Contains(filter)
                ).ToList();

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
            _searchHistoryService.CommitSearchText(text);
        }

        private void CommitSearch()
        {
            CommitSearchText(SearchKeyword);
            maxTypedKeyword = SearchKeyword ?? "";
            FilterItems();
        }

        private void PrevSearch()
        {
            var prev = _searchHistoryService.GetPrevious();
            if (prev != null)
            {
                SearchKeyword = prev;
            }
        }

        private void NextSearch()
        {
            var next = _searchHistoryService.GetNext();
            if (next != null)
            {
                SearchKeyword = next;
            }
        }

        private void UpdateStatusBar()
        {
            string fmt = Application.Current.TryFindResource("StatusTotalItems") as string ?? "Total items: {0}";
            TotalItemsText = string.Format(fmt, _allDatabaseItems.Count);
        }

        private void SelectPlayer()
        {
            _playerService.SelectPlayer(path => OnPropertyChanged(nameof(CustomPlayerPath)));
        }

        private void OpenMedia(VideoItem item)
        {
            if (item == null) return;
            try
            {
                _playerService.OpenMedia(item.FullName);
            }
            catch (Exception ex)
            {
                HandlePlayerError(ex);
            }
        }

        private void OpenContainingFolder(VideoItem item)
        {
            if (item == null) return;
            try
            {
                _playerService.OpenContainingFolder(item.FullName);
            }
            catch (Exception ex)
            {
                HandlePlayerError(ex);
            }
        }

        private void HandlePlayerError(Exception ex)
        {
            Logger.Error(ex, "Player or folder open error");
            string msg = Application.Current.TryFindResource("DialogPathInvalid") as string ?? "Path invalid or operation failed.";
            if (ex.Message.Contains("ISO"))
            {
                msg = Application.Current.TryFindResource("DialogNoIsoFound") as string ?? "No ISO file found in directory.";
            }

            string title = Application.Current.TryFindResource("DialogErrorTitle") as string ?? "Error";
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task SaveCoverImageAsync()
        {
            if (CurrentMetadata == null || string.IsNullOrEmpty(CurrentMetadata.PosterPath))
                return;

            string posterPath = CurrentMetadata.PosterPath;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileName(posterPath) ?? "poster.jpg",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "保存封面图片"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    if (posterPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                        posterPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var httpClient = new System.Net.Http.HttpClient())
                        {
                            var imageBytes = await httpClient.GetByteArrayAsync(posterPath);
                            File.WriteAllBytes(dlg.FileName, imageBytes);
                        }
                    }
                    else if (File.Exists(posterPath))
                    {
                        File.Copy(posterPath, dlg.FileName, true);
                    }
                    MessageBox.Show("图片已成功保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to save cover image");
                    MessageBox.Show("图片保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void Dispose()
        {
            _searchDebouncer?.Cancel();
            _fileChangeDebouncer?.Cancel();
            _fileScanService?.Dispose();
            _dbManager?.Dispose();
        }
    }
}
