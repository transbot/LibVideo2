using System;
using System.IO;

namespace LibVideo.Helpers
{
    public static class AppPaths
    {
        private static readonly string _baseDir;

        static AppPaths()
        {
            _baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LibVideo");

            if (!Directory.Exists(_baseDir))
                Directory.CreateDirectory(_baseDir);

            MigrateOldFiles();
        }

        public static string LanguageFile => Path.Combine(_baseDir, "language.txt");
        public static string DirectoriesFile => Path.Combine(_baseDir, "directories.txt");
        public static string PlayerFile => Path.Combine(_baseDir, "player.txt");
        public static string SearchHistoryFile => Path.Combine(_baseDir, "search_history.txt");
        public static string DatabaseFile => Path.Combine(_baseDir, "libvideo_db.db");

        /// <summary>
        /// One-time migration: copies config files from the exe directory to %AppData%\LibVideo\
        /// so existing users don't lose their settings.
        /// </summary>
        private static void MigrateOldFiles()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            MigrateFile(Path.Combine(exeDir, "language.txt"), LanguageFile);
            MigrateFile(Path.Combine(exeDir, "directories.txt"), DirectoriesFile);
            MigrateFile(Path.Combine(exeDir, "player.txt"), PlayerFile);
            MigrateFile(Path.Combine(exeDir, "libvideo_db.db"), DatabaseFile);
        }

        private static void MigrateFile(string oldPath, string newPath)
        {
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                try { File.Copy(oldPath, newPath); } catch { }
            }
        }
    }
}
