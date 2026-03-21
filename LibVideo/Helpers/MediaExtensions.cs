using System;
using System.Linq;

namespace LibVideo.Helpers
{
    /// <summary>
    /// Single source of truth for all supported media file extensions.
    /// Eliminates duplication between MainViewModel and MetadataService.
    /// </summary>
    public static class MediaExtensions
    {
        public static readonly string[] VideoExtensions =
        {
            ".mp4", ".mkv", ".avi", ".rmvb", ".wmv", ".flv",
            ".mov", ".ts", ".iso", ".m2ts"
        };

        public static readonly string[] AudioExtensions =
        {
            ".mp3", ".flac", ".wav", ".ape", ".m4a",
            ".aac", ".wma", ".ogg", ".mid"
        };

        public static readonly string[] AllMediaExtensions;

        static MediaExtensions()
        {
            AllMediaExtensions = VideoExtensions.Concat(AudioExtensions).ToArray();
        }

        public static bool IsMediaFile(string extension)
        {
            return AllMediaExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAudioFile(string extension)
        {
            return AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsVideoFile(string extension)
        {
            return VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }
    }
}
