using System;
using System.IO;

namespace LibVideo.Helpers
{
    public static class Logger
    {
        private static readonly object _lock = new object();

        public static void Error(Exception ex, string context = "")
        {
            if (ex == null) return;
            Log($"[ERROR] {context}: {ex}");
        }

        public static void Error(string message)
        {
            Log($"[ERROR] {message}");
        }

        public static void Warn(string message)
        {
            Log($"[WARNING] {message}");
        }

        public static void Info(string message)
        {
            Log($"[INFO] {message}");
        }

        private static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(AppPaths.LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n\r\n");
                }
            }
            catch
            {
                // Last resort fallback
            }
        }
    }
}
