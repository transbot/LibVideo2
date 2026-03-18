using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using LibVideo.ViewModels;

namespace LibVideo
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }
        
        private void AddDir_Click(object sender, RoutedEventArgs e)
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
                (DataContext as MainViewModel)?.AddDirectoryCommand.Execute(dlg.FileName);
            }
        }
    }
}
