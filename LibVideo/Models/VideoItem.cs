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

        public VideoItem()
        {
            CreationTime = DateTime.Now;
        }
    }
}
