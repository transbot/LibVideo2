using System;
using LiteDB;

namespace LibVideo.Models
{
    public class VideoItem
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FolderName { get; set; }
        public string FullName { get; set; }
        public DateTime CreationTime { get; set; }

        public string MetaTitle { get; set; }
        public string MetaPlot { get; set; }
        public string MetaGenre { get; set; }
        public string MetaPosterPath { get; set; }
        public bool HasScraped { get; set; }

        public VideoItem()
        {
            CreationTime = DateTime.Now;
        }
    }
}
