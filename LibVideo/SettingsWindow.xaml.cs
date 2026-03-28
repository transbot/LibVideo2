using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using LibVideo.ViewModels;
using LibVideo.Helpers;

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
            string title = Application.Current.TryFindResource("DialogSelectFolderTitle") as string ?? "Select Folder";
            var dlg = new CommonOpenFileDialog
            {
                Title = title,
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

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
