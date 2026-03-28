using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using LibVideo.Models;
using LibVideo.Helpers;

namespace LibVideo.Data
{
    public class DatabaseManager : IDisposable
    {
        private readonly string dbPath;
        private readonly LiteDatabase _db;

        public DatabaseManager()
        {
            dbPath = AppPaths.DatabaseFile;
            // LiteDatabase instance is thread-safe.
            _db = new LiteDatabase($"Filename={dbPath};Connection=Shared");
        }

        public void SyncDiskItems(IEnumerable<VideoItem> currentFilesList, HashSet<string> scannedRoots)
        {
            var col = _db.GetCollection<VideoItem>("videos");
            col.EnsureIndex(x => x.FullName, true);
            
            var allDb = col.FindAll().ToList();
            var currentPaths = new HashSet<string>(currentFilesList.Select(x => x.FullName), StringComparer.OrdinalIgnoreCase);

            foreach(var dbItem in allDb)
            {
                bool isUnderScannedRoot = scannedRoots.Any(root => dbItem.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase));
                if (isUnderScannedRoot)
                {
                    if (!currentPaths.Contains(dbItem.FullName))
                    {
                        col.Delete(dbItem.Id);
                    }
                }
            }
            
            var existingFiles = new HashSet<string>(col.FindAll().Select(v => v.FullName), StringComparer.OrdinalIgnoreCase);
            var newItems = currentFilesList.Where(i => !existingFiles.Contains(i.FullName)).ToList();

            if (newItems.Count > 0)
            {
                col.InsertBulk(newItems);
            }
        }

        public List<VideoItem> GetAllItems()
        {
            var col = _db.GetCollection<VideoItem>("videos");
            return col.FindAll().ToList();
        }
        
        public void ClearItems()
        {
            _db.DropCollection("videos");
        }
        
        public void RemoveDirectoryItems(string directoryPath)
        {
            var col = _db.GetCollection<VideoItem>("videos");
            var itemsToDelete = col.Find(x => x.FullName.StartsWith(directoryPath)).Select(x => x.Id).ToList();
            foreach (var id in itemsToDelete)
            {
                col.Delete(id);
            }
        }

        public void UpdateItemMetadata(VideoItem memItem)
        {
            var col = _db.GetCollection<VideoItem>("videos");
            var dbItem = col.FindOne(x => x.FullName == memItem.FullName);
            if (dbItem != null)
            {
                dbItem.MetaTitle = memItem.MetaTitle;
                dbItem.MetaPlot = memItem.MetaPlot;
                dbItem.MetaGenre = memItem.MetaGenre;
                dbItem.MetaPosterPath = memItem.MetaPosterPath;
                dbItem.MetaRating = memItem.MetaRating;
                dbItem.HasScraped = memItem.HasScraped;
                col.Update(dbItem);
            }
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
