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
using System.Collections;
using System.ComponentModel;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;




namespace LibVideo
{

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    /// 


    /* todo: 
     * 1. (已完成)排除例外项：不显示mkv,avi之外的文件名
     * 2. (已完成)输出网格每行可选，并可播放视频或打开文件夹
     * 3. (已完成)手动添加其他目录并更新结果列表
     * 4. 异步输出，并显示视觉线索      
     * 5. (已完成)搜索和定位功能
     * 6. 挖掘子目录
     * 7. (非迫切)中英分离
     * 8. 显示结果按创建时间排序
    */

    // 字符串扩展方法
    public static class StringExtensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }
    }

    // 基于指定目录构建目录数组
    class DirectoryList
    {

        private List<string> directory = new List<string>{@"\\ds1817\Movies\", @"\\ds1517\Movies\",  @"\\ds1817\TVs\", @"\\ds1817\Documentary\" };
        
        

        public void AddDirectory(string s) {
            directory.Add(s);
            
        }
        public DirectoryList()
        {
            
        }
        //属性，获取或更新目录列表
        public List<string> DIRs
        {
            get { return this.directory; }
        }
            
          

       
    }
    // 基于提供目录数组构建目录内容列表
    class ResultList
    {
        private List<AVItems> FileOrDirectoryList = new List<AVItems>();
        public List<AVItems> RESULTLIST {get{return this.FileOrDirectoryList;} }



        public ResultList() { }

        public ResultList( List<string> dirList)
        {
            int n = 1;
            foreach (string s in dirList)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(s);


                // 以下列出文件:
                foreach (FileInfo fi in directoryInfo.GetFiles())
                {
                    if (fi.Extension.Contains("avi", StringComparison.OrdinalIgnoreCase) ||
                        fi.Extension.Contains("mov", StringComparison.OrdinalIgnoreCase) ||
                        fi.Extension.Contains("mkv", StringComparison.OrdinalIgnoreCase) ||
                        fi.Extension.Contains("wmv", StringComparison.OrdinalIgnoreCase) ||
                        fi.Extension.Contains("flac", StringComparison.OrdinalIgnoreCase) ||
                        fi.Extension.Contains("iso", StringComparison.OrdinalIgnoreCase) ||
                        fi.Extension.Contains("mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        FileOrDirectoryList.Add(new AVItems() { 
                            Id = n, FileName = fi.Name, FolderName = fi.DirectoryName, FullName = fi.FullName });
                        n++;
                    }
                }

                // 以下列出目录中的子目录:
                foreach (DirectoryInfo di in directoryInfo.GetDirectories())
                {
                    FileOrDirectoryList.Add(new AVItems() { Id = n, FileName = null, FolderName = di.FullName, FullName=di.FullName}); ;
                    n++;
                }
            
            }
        }
    }

    class AVItems
    {

        public int Id { get; set; }
        public string FileName { get; set; }
        public string FolderName { get; set; }
        public string FullName { get; set; }

    }


    public partial class MainWindow : Window
    {
        private DirectoryList dirList = new DirectoryList();
        
        
        private ResultList resultList = new ResultList();

        private string potplayerPath = GetPotPlayerPath();
        public MainWindow()
        {
            InitializeComponent();
            ImageBrush myBrush = new ImageBrush();
            myBrush.ImageSource =
                new BitmapImage(new Uri("background.jpg", UriKind.Relative));
            this.Background = myBrush;

            // 创建默认目录列表
            // DirectoryList dirList = new DirectoryList();

            // 用默认目录列表创建默认结果列表
            resultList = new ResultList(dirList.DIRs);

            // 显示默认目录列表
            DisplayDirList(dirList);

            
            
            // 更新结果列表
            outputGrid.ItemsSource = resultList.RESULTLIST;          
            
        }

        // todo: 获取目录中的一个item(文件或子目录)，返回影音文件的路径，或返回子目录中的影音文件的路径
        private static string GetAVItemPath()
        {
            return null;
        }

        private static string GetPotPlayerPath()

        {

            var regKey = @"Applications\PotPlayerMini64.exe\shell\open\command";

            RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(regKey);


            string pathName = (string)registryKey.GetValue(null);  // "(Default)"  


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




        private void DisplayDirList(DirectoryList dirList)
        {
            dirTxt.Clear();
            foreach (string s in dirList.DIRs)
            {
                dirTxt.AppendText(s + "\n");
            }
            
        }

        private void OpenItem(object sender, RoutedEventArgs e)
        {
            DataGridCellInfo cell = (DataGridCellInfo)outputGrid.CurrentCell;

            TextBlock tb = outputGrid.Columns[1].GetCellContent(cell.Item) as TextBlock;
            Process.Start(tb.Text);            

        }

        private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
        {

            DataGridCellInfo cell = (DataGridCellInfo)outputGrid.CurrentCell;
            //DataGridRow row = sender as DataGridRow;
            TextBlock item = outputGrid.Columns[1].GetCellContent(cell.Item) as TextBlock;
            if (potplayerPath != null)
            {
                Process.Start(potplayerPath, item.Text);
            }
            else
            {
                Process.Start(item.Text);
            }


        }


        private void dirTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {


        }


        

        private void keywords_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox t = (TextBox)sender;
            string filter = t.Text;
            ICollectionView cv = CollectionViewSource.GetDefaultView(outputGrid.ItemsSource);
            cv.Filter = o =>
            {
                AVItems av = o as AVItems;
                if (av.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase)){ return true; }
                return false;
            };
            }

        private void dirButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog();
            dlg.Title = "选择文件夹";
            dlg.IsFolderPicker = true;
            

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var folder = dlg.FileName;

                dirList.AddDirectory(folder);
                DisplayDirList(dirList);
                resultList = new ResultList(dirList.DIRs);

                outputGrid.ItemsSource = null;
                outputGrid.ItemsSource = resultList.RESULTLIST;
                
            }   

        }

        
    }
}
