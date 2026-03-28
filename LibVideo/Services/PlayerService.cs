using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace LibVideo.Services
{
    public class PlayerService
    {
        private readonly string potplayerPath;
        private string customPlayerPath;

        public PlayerService(string initialCustomPlayerPath)
        {
            this.customPlayerPath = initialCustomPlayerPath;
            this.potplayerPath = GetPotPlayerPath();
        }

        public string CustomPlayerPath
        {
            get => customPlayerPath;
            set
            {
                customPlayerPath = value;
                try { File.WriteAllText(Helpers.AppPaths.PlayerFile, value ?? ""); } catch { }
            }
        }

        public void SelectPlayer(Action<string> onCustomPlayerSelected)
        {
            string title = System.Windows.Application.Current.TryFindResource("DialogSelectPlayerTitle") as string ?? "Select Custom Player";
            var dlg = new OpenFileDialog { Filter = "Executable Files (*.exe)|*.exe", Title = title };
            if (dlg.ShowDialog() == true)
            {
                CustomPlayerPath = dlg.FileName;
                onCustomPlayerSelected?.Invoke(dlg.FileName);
            }
        }

        public void OpenMedia(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
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
                    throw new FileNotFoundException("No ISO file found in directory.", filePath);
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
                throw new FileNotFoundException("Path invalid.", filePath);
            }
        }

        public void OpenContainingFolder(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (File.Exists(filePath))
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            else if (Directory.Exists(filePath))
                Process.Start("explorer.exe", $"\"{filePath}\"");
            else
                throw new FileNotFoundException("Path invalid.", filePath);
        }

        private bool IsIsoFile(string filePath) => Path.GetExtension(filePath).Equals(".iso", StringComparison.OrdinalIgnoreCase);

        private bool ContainsIsoFile(string directoryPath) => Directory.Exists(directoryPath) && Directory.GetFiles(directoryPath, "*.iso", SearchOption.TopDirectoryOnly).Any();

        private static string GetPotPlayerPath()
        {
            try
            {
                RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(@"Applications\PotPlayerMini64.exe\shell\open\command");
                string pathName = (string)registryKey?.GetValue(null);
                if (string.IsNullOrEmpty(pathName)) return null;

                int index = pathName.LastIndexOf(" ");
                return index != -1 ? pathName.Substring(0, index).Replace("\"", string.Empty) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
