using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using LibVideo.ViewModels;

namespace LibVideo
{
    public partial class SettingsWindow : Window
    {
        private bool _isInitialized = false;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentLanguage();
            _isInitialized = true;
        }

        private void LoadCurrentLanguage()
        {
            string lang = "zh";
            if (File.Exists("language.txt"))
            {
                lang = File.ReadAllText("language.txt").Trim();
            }

            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == lang)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
        }
        
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string langCode = item.Tag.ToString();
                App.ChangeLanguage(langCode);
                File.WriteAllText("language.txt", langCode);
            }
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

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
