using System.Collections.Generic;
using System.Linq;
using LiteDB;
using LibVideo.Models;

namespace LibVideo.Data
{
    public class DatabaseManager
    {
        private readonly string dbPath;

        public DatabaseManager(string path = "libvideo_db.db")
        {
            dbPath = path;
        }

        public void InsertOrUpdateItems(IEnumerable<VideoItem> items)
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var col = db.GetCollection<VideoItem>("videos");
                col.EnsureIndex(x => x.FullName, true);
                
                var existingFiles = new HashSet<string>(col.FindAll().Select(v => v.FullName));
                var newItems = items.Where(i => !existingFiles.Contains(i.FullName)).ToList();

                if (newItems.Count > 0)
                {
                    col.InsertBulk(newItems);
                }
            }
        }

        public List<VideoItem> GetAllItems()
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var col = db.GetCollection<VideoItem>("videos");
                return col.FindAll().ToList();
            }
        }
        
        public void ClearItems()
        {
            using (var db = new LiteDatabase(dbPath))
            {
                db.DropCollection("videos");
            }
        }
        
        public void RemoveDirectoryItems(string directoryPath)
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var col = db.GetCollection<VideoItem>("videos");
                var itemsToDelete = col.Find(x => x.FullName.StartsWith(directoryPath)).Select(x => x.Id).ToList();
                foreach (var id in itemsToDelete)
                {
                    col.Delete(id);
                }
            }
        }

        public void UpdateItemMetadata(VideoItem memItem)
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var col = db.GetCollection<VideoItem>("videos");
                var dbItem = col.FindOne(x => x.FullName == memItem.FullName);
                if (dbItem != null)
                {
                    dbItem.MetaTitle = memItem.MetaTitle;
                    dbItem.MetaPlot = memItem.MetaPlot;
                    dbItem.MetaGenre = memItem.MetaGenre;
                    dbItem.MetaPosterPath = memItem.MetaPosterPath;
                    dbItem.HasScraped = memItem.HasScraped;
                    col.Update(dbItem);
                }
            }
        }
    }
}
