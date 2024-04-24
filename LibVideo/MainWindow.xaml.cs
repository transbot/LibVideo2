/* todo: 
 * 1. 允许删除目录
 * 2. 显示splash screen
 * 3. 优化UI，添加前后图标，显示之前和之后搜索的结果
 * 4. 列出最近和频繁搜索的关键字
 * 5. （已完成）定时更新显示
 * 6. 为什么水平滚动条不显示？ * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace LibVideo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private DirectoryList dirList = new DirectoryList();
        private ObservableCollection<AVItems> resultList = new ObservableCollection<AVItems>();
        private string potplayerPath = GetPotPlayerPath();
        private int initialItemCount = 0;  // 在首次加载数据后设置这个值

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            outputGrid.ItemsSource = resultList;  // 只设置一次
                                                  
            DispatcherTimer timer = new DispatcherTimer(); // 设置定时器
            timer.Interval = TimeSpan.FromSeconds(60); // 每60秒检查一次
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            await CheckAndUpdateFilesAsync();
        }

        private async Task CheckAndUpdateFilesAsync()
        {
            // 假设 directoryList 和 previousItemList 是之前加载项的记录
            var currentItems = new List<AVItems>();
            foreach (var dir in dirList.DIRs)
            {
                currentItems.AddRange(await GetFilesAsync(dir));
            }

            // 比较当前项和之前项
            if (!AreListsEqual(currentItems, resultList))
            {
                Dispatcher.Invoke(() =>
                {
                    resultList.Clear();
                    foreach (var item in currentItems)
                    {
                        resultList.Add(item);
                    }

                    UpdateStatusBar(); // 更新状态栏信息
                });

                
            }
        }


        private void UpdateStatusBar()
        {
            // 更新总项数
            totalItemsText.Text = $"媒体文件或文件夹共计: {resultList.Count} 个";

            // 计算从首次加载后新增的项数
            if (initialItemCount > 0)
            {
                int newItemsCount = resultList.Count - initialItemCount;
                itemsAddedLast24HoursText.Text = $"首次加载后新增了: {newItemsCount} 个";
            }
        }



        private bool AreListsEqual(List<AVItems> currentItems, ObservableCollection<AVItems> previousItems)
        {
            if (currentItems.Count != previousItems.Count)
                return false;

            var set = new HashSet<AVItems>(currentItems, new AVItemComparer());
            return previousItems.All(set.Contains);
        }

        class AVItemComparer : IEqualityComparer<AVItems>
        {
            public bool Equals(AVItems x, AVItems y)
            {
                return x.FileName == y.FileName && x.FolderName == y.FolderName && x.FullName == y.FullName;
            }

            public int GetHashCode(AVItems obj)
            {
                return obj.FullName.GetHashCode();
            }
        }


        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ImageBrush myBrush = new ImageBrush();
            myBrush.ImageSource = new BitmapImage(new Uri("background.jpg", UriKind.Relative));
            this.Background = myBrush;

            DisplayDirList(dirList);
            await LoadFilesAsync();
            outputGrid.ItemsSource = resultList;
        }

        private void dirTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 获取当前文本框中的文本
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                // 假设dirTxt是一个显示目录地址的TextBlock或类似控件
                dirTxt.Text = textBox.Text;
            }
        }

        private void keywords_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string filter = textBox.Text.ToLower();
            ICollectionView collectionView = CollectionViewSource.GetDefaultView(outputGrid.ItemsSource);
            collectionView.Filter = (item) =>
            {
                AVItems avItem = item as AVItems;
                return avItem != null && avItem.FileName.ToLower().Contains(filter);
            };
        }
        private async Task LoadFilesAsync()
        {
            resultList.Clear();
            List<Task<List<AVItems>>> loadTasks = new List<Task<List<AVItems>>>();

            foreach (string dir in dirList.DIRs)
            {
                if (Directory.Exists(dir))  // 确保目录存在
                {
                    loadTasks.Add(GetFilesAsync(dir));
                }
                else
                {
                    Debug.WriteLine($"Directory does not exist: {dir}");
                }
            }

            // 等待所有目录加载任务完成
            var results = await Task.WhenAll(loadTasks);


            // 使用Dispatcher确保UI线程上操作
            Dispatcher.Invoke(() =>
            {
                foreach (var items in results)
                {
                    foreach (var item in items)
                    {
                        resultList.Add(item);  // 在UI线程添加到ObservableCollection
                    }
                }
            });

            // 首次加载后设置初始项数
            if (initialItemCount == 0) // 保证只在首次加载时设置
            {
                initialItemCount = resultList.Count;
            }

            // 更新状态栏
            UpdateStatusBar();
        }


        private async Task<List<AVItems>> GetFilesAsync(string path)
        {
            var items = new List<AVItems>();

            try
            {
                await Task.Run(() =>
                {
                    var directoryInfo = new DirectoryInfo(path);

                    // 获取当前目录下所有文件
                    foreach (FileInfo fi in directoryInfo.EnumerateFiles("*.*"))
                    {
                        if (IsMediaFile(fi.Extension))
                        {
                            items.Add(new AVItems
                            {
                                FileName = fi.Name,
                                FolderName = fi.DirectoryName,
                                FullName = fi.FullName
                            });
                        }
                    }

                    // 获取当前目录下所有子目录
                    foreach (DirectoryInfo di in directoryInfo.EnumerateDirectories())
                    {
                        items.Add(new AVItems
                        {

                            FileName = di.Name,
                            FolderName = di.Parent.FullName,
                            FullName = di.FullName
                        });
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Access denied to {path}: {ex.Message}", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (DirectoryNotFoundException ex)
            {
                MessageBox.Show($"Directory not found: {path}: {ex.Message}", "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while processing files and directories in {path}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return items;
        }








        private bool IsMediaFile(string extension)
        {
            string[] mediaExtensions = { ".avi", ".mov", ".mkv", ".wmv", ".flac", ".iso", ".mp3" };
            return mediaExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private void DisplayDirList(DirectoryList dirList)
        {
            dirTxt.Clear();
            foreach (string s in dirList.DIRs)
            {
                dirTxt.AppendText(s + "\n");
            }
        }

        private static string GetPotPlayerPath()
        {
            var regKey = @"Applications\PotPlayerMini64.exe\shell\open\command";
            RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(regKey);
            string pathName = (string)registryKey.GetValue(null);
            if (string.IsNullOrEmpty(pathName))
            {
                return null;
            }

            int index = pathName.LastIndexOf(" ");
            if (index != -1)
            {
                return pathName.Substring(0, index).Replace("\"", string.Empty);
            }

            return null;
        }

        private void OpenMediaOrFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                // 检查路径是否指向ISO文件或目录中是否存在ISO文件
                if (IsIsoFile(filePath) || ContainsIsoFile(filePath))
                {
                    // 如果是ISO文件或目录中有ISO文件，使用特定的处理逻辑
                    StartProcessForIso(filePath);
                }
                else if (File.Exists(filePath) || Directory.Exists(filePath))
                {
                    // 处理其他类型的文件或目录
                    StartProcess(filePath);
                }
                else
                {
                    MessageBox.Show("The file path does not exist or is not valid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("The file path is empty or invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsIsoFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".iso", StringComparison.OrdinalIgnoreCase);
        }

        private bool ContainsIsoFile(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                return Directory.GetFiles(directoryPath, "*.iso", SearchOption.TopDirectoryOnly).FirstOrDefault() != null;
            }
            return false;
        }

        private void StartProcessForIso(string filePath)
        {
            string isoFilePath = filePath;

            if (Directory.Exists(filePath))
            {
                // 如果是目录，尝试找到第一个ISO文件
                isoFilePath = Directory.GetFiles(filePath, "*.iso", SearchOption.TopDirectoryOnly).FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(isoFilePath))
            {
                Process.Start(isoFilePath); // 使用默认关联应用打开ISO文件
            }
            else
            {
                MessageBox.Show("No ISO file found in the specified directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartProcess(string filePath)
        {
            if (potplayerPath != null && !IsIsoFile(filePath))
            {
                // 如果配置了 potplayerPath 并且文件不是.iso，使用 PotPlayer 打开
                Process.Start(potplayerPath, filePath);
            }
            else
            {
                // 否则使用系统默认程序打开文件
                Process.Start(filePath);
            }
        }




        private void PlayOrOpenItem(object sender, RoutedEventArgs e)
        {
            DataGridCellInfo cell = (DataGridCellInfo)outputGrid.CurrentCell;
            if (cell.Item is AVItems avItem)
            {
                OpenMediaOrFile(avItem.FullName);
            }

        }

        private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridCellInfo cell = (DataGridCellInfo)outputGrid.CurrentCell;
            if (cell.Item is AVItems avItem)
            {
                OpenMediaOrFile(avItem.FullName);
            }
        }


        private void OpenContainingFolder(object sender, RoutedEventArgs e)
        {
            // 获取当前选中的单元格信息
            DataGridCellInfo cell = outputGrid.CurrentCell;

            // 尝试获取单元格内容作为TextBlock
            TextBlock tb = outputGrid.Columns[1].GetCellContent(cell.Item) as TextBlock;

            if (tb != null)
            {
                string filePath = tb.Text;

                // 检查文件是否存在
                if (File.Exists(filePath))
                {
                    // 使用explorer.exe的/select,参数打开文件所在的目录并选中该文件
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(filePath))
                {
                    // 如果是文件夹，直接打开该文件夹
                    Process.Start("explorer.exe", $"\"{filePath}\"");
                }
                else
                {
                    MessageBox.Show("指定的文件或目录不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void dirButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "选择文件夹",
                IsFolderPicker = true,
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            };

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var folder = dlg.FileName;
                dirList.AddDirectory(folder);
                DisplayDirList(dirList);
                await LoadFilesAsync();
            }
        }
    }

    public class DirectoryList
    {
        private List<string> directories = new List<string> { @"\\ds1817\Movies\", @"\\ds1517\Movies\", @"\\ds1817\TVs\", @"\\ds1817\Documentary\", @"\\DISKSTATION2\Movies" };

        public void AddDirectory(string directory)
        {
            if (!directories.Contains(directory))
            {

                directories.Add(directory);
            }
        }

        public List<string> DIRs
        {
            get { return directories; }
        }
    }

    public class AVItems
    {
        private static int nextId = 1;
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FolderName { get; set; }
        public string FullName { get; set; }

        public AVItems()
        {
            Id = nextId++;  // 构造函数中分配ID，并递增计数器
            CreationTime = DateTime.Now; // 初始化时设置创建时间
        }

        public DateTime CreationTime { get; private set; }
    }
}
