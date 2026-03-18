using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using LibVideo.Models;
using LibVideo.ViewModels;

namespace LibVideo
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            System.Windows.Media.ImageBrush myBrush = new System.Windows.Media.ImageBrush();
            myBrush.ImageSource = new System.Windows.Media.Imaging.BitmapImage(new System.Uri("pack://application:,,,/background.jpg", System.UriKind.Absolute));
            this.Background = myBrush;
        }

        private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (outputGrid.SelectedItem is VideoItem avItem)
            {
                (DataContext as MainViewModel)?.OpenMediaCommand.Execute(avItem);
            }
        }

        private void PlayOrOpenItem(object sender, RoutedEventArgs e)
        {
            if (outputGrid.SelectedItem is VideoItem avItem)
            {
                (DataContext as MainViewModel)?.OpenMediaCommand.Execute(avItem);
            }
        }

        private void OpenContainingFolder(object sender, RoutedEventArgs e)
        {
            if (outputGrid.SelectedItem is VideoItem avItem)
            {
                (DataContext as MainViewModel)?.OpenContainingFolderCommand.Execute(avItem);
            }
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = this;
            settingsWin.DataContext = this.DataContext;
            settingsWin.ShowDialog();
        }
    }
}
