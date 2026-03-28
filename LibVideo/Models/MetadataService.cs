using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using LibVideo.Helpers;

namespace LibVideo.Models
{
    public class VideoMetadata
    {
        public string Title { get; set; }
        public string Plot { get; set; }
        public string Genre { get; set; }
        public string PosterPath { get; set; }
        public double Rating { get; set; }
        public string RatingDisplay => Rating > 0 ? $"⭐ {Rating:F1}" : "";
    }

    public static class MetadataService
    {
        private static readonly string TMDB_API_KEY = "b5355467863d25d8a7c03c204446034c";
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly JavaScriptSerializer _jsonSerializer = new JavaScriptSerializer();

        private static bool IsEnglish()
        {
            return App.CurrentLanguageCode == "en";
        }

        public static async Task<VideoMetadata> GetMetadataAsync(string videoFilePath)
        {
            if (string.IsNullOrEmpty(videoFilePath) || (!File.Exists(videoFilePath) && !Directory.Exists(videoFilePath)))
                return null;

            string directory = File.Exists(videoFilePath) ? Path.GetDirectoryName(videoFilePath) : videoFilePath;
            string baseName = Path.GetFileNameWithoutExtension(videoFilePath);
            bool isEn = IsEnglish();

            bool isAudio = false;

            if (Directory.Exists(videoFilePath))
            {
                baseName = Path.GetFileName(videoFilePath);
                
                try
                {
                    var files = Directory.EnumerateFiles(videoFilePath, "*.*", SearchOption.AllDirectories);
                    bool hasAudio = false;
                    bool hasVideo = false;
                    
                    foreach (var f in files)
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (MediaExtensions.IsAudioFile(ext)) hasAudio = true;
                        if (MediaExtensions.IsVideoFile(ext)) hasVideo = true;
                        
                        // 发现任何一个视频文件立即阻断扫描
                        if (hasVideo) break;
                    }
                    if (hasAudio && !hasVideo) isAudio = true;
                } 
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to analyze directory {videoFilePath}");
                }
            }
            else
            {
                string extension = Path.GetExtension(videoFilePath).ToLowerInvariant();
                isAudio = MediaExtensions.IsAudioFile(extension);
            }

            if (isAudio)
            {
                var audioMeta = await Task.Run(() => GetLocalMetadata(directory, baseName, isEn));
                if (audioMeta != null)
                {
                    audioMeta.Genre = isEn ? "Audio" : "音频";
                    audioMeta.Plot = isEn ? "You have selected a local audio/music file. No plot available." : "您选择的是本地音乐/音频文件（或专有音频文件夹），目前暂无剧情简介。";
                }
                return audioMeta;
            }

            var (searchName, searchYear) = CleanFileName(baseName);
            
            string tmdbLang = isEn ? "en-US" : "zh-CN";
            var meta = await FetchFromTmdbAsync(searchName, searchYear, tmdbLang, isEn);
            
            string noPlotMessage = isEn ? "No plot overview available." : "该剧集/电影暂无对应的简介。";
            
            // 如果没搜到，或者搜到了但是中文没翻译导致简介为空（且不在英文模式下），自动追搜一遍英文数据
            if (!isEn && (meta == null || meta.Plot == noPlotMessage))
            {
                var enMeta = await FetchFromTmdbAsync(searchName, searchYear, "en-US", isEn);
                if (enMeta != null)
                {
                    if (meta == null) 
                    {
                        meta = enMeta; // 完全使用英文数据
                    }
                    else 
                    {
                        if (enMeta.Plot != noPlotMessage && enMeta.Plot != "No plot overview available.")
                            meta.Plot = enMeta.Plot; // 借用英文简介
                    }
                }
            }
            
            if (meta == null)
            {
               meta = await Task.Run(() => GetLocalMetadata(directory, baseName, isEn));
            }
            
            if (meta != null && (string.IsNullOrEmpty(meta.Genre) || meta.Genre == "电影" || meta.Genre == "Movie"))
            {
                meta.Genre = isEn ? "Video" : "视频";
            }
            
            return meta;
        }

        private static (string title, string year) CleanFileName(string fileName)
        {
            try
            {
                string cleaned = fileName;
                // 将括号替换为空格，保留括号内的中文译名
                cleaned = cleaned.Replace("[", " ").Replace("]", " ")
                                 .Replace("(", " ").Replace(")", " ")
                                 .Replace("【", " ").Replace("】", " ");

                cleaned = cleaned.Replace(".", " ").Replace("_", " ").Replace("-", " ");
                
                cleaned = Regex.Replace(cleaned, @"\b[sS]\d+(?:[eE]\d+)?\b", " ", RegexOptions.IgnoreCase);
                cleaned = Regex.Replace(cleaned, @"\b[eE]\d+\b", " ", RegexOptions.IgnoreCase);

                string[] tags = { "1080p", "720p", "2160p", "4k", "bluray", "webrip", "web-dl", "web dl", "hdrip", "x264", "x265", "hevc", "avc", "aac", "dts", "h264", "h265", "10bit", "hdr", "ddp", "atmos", "truehd", "remux", "itunes", "minibd", "cmct", "chdbits", "yify", "rarbg", "tvrip", "hdtv" };
                
                foreach (var tag in tags)
                {
                    cleaned = Regex.Replace(cleaned, $@"\b{tag}\b", " ", RegexOptions.IgnoreCase);
                }
                
                string year = "";
                // 截取到年份为止（抛弃后面所有的混杂后缀）
                var yearMatch = Regex.Match(cleaned, @"\b(19\d{2}|20\d{2})\b");
                if (yearMatch.Success)
                {
                    int yearIndex = yearMatch.Index;
                    string potentialTitle = cleaned.Substring(0, yearIndex).Trim();
                    
                    if (!string.IsNullOrWhiteSpace(potentialTitle))
                    {
                        cleaned = potentialTitle;
                        year = yearMatch.Groups[1].Value;
                    }
                    else
                    {
                        year = "";
                    }
                }
                
                string title = Regex.Replace(cleaned, @"\s+", " ").Trim();
                return (title, year);
            }
            catch
            {
                return (fileName, "");
            }
        }

        private static async Task<VideoMetadata> FetchFromTmdbAsync(string query, string year, string language, bool isEn)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            var movieMeta = await SearchTmdbCategoryAsync("movie", query, year, language, isEn);
            if (movieMeta != null) return movieMeta;

            var tvMeta = await SearchTmdbCategoryAsync("tv", query, year, language, isEn);
            return tvMeta;
        }

        private static async Task<VideoMetadata> SearchTmdbCategoryAsync(string category, string query, string year, string language, bool isEn)
        {
            try
            {
                string url = $"https://api.themoviedb.org/3/search/{category}?api_key={TMDB_API_KEY}&query={Uri.EscapeDataString(query)}&language={language}";
                if (!string.IsNullOrEmpty(year))
                {
                    if (category == "movie") url += $"&year={year}";
                    else if (category == "tv") url += $"&first_air_date_year={year}";
                }
                
                string jsonString = await _httpClient.GetStringAsync(url);
                var json = _jsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                
                var results = json.ContainsKey("results") ? json["results"] as ArrayList : null;
                if (results == null || results.Count == 0) return null;
                
                var first = results[0] as Dictionary<string, object>;
                if (first == null) return null;

                var meta = new VideoMetadata();
                meta.Title = GetJsonValue(first, "title") ?? GetJsonValue(first, "name") ?? query;
                
                string overview = GetJsonValue(first, "overview");
                meta.Plot = !string.IsNullOrWhiteSpace(overview) ? overview : (isEn ? "No plot overview available." : "该剧集/电影暂无对应的简介。");
                
                string posterPath = GetJsonValue(first, "poster_path");
                if (!string.IsNullOrEmpty(posterPath))
                {
                    meta.PosterPath = "https://image.tmdb.org/t/p/w500" + posterPath;
                }

                // Extract TMDB rating
                if (first.ContainsKey("vote_average") && first["vote_average"] != null)
                {
                    try { meta.Rating = Convert.ToDouble(first["vote_average"]); } catch { }
                }

                // Extract genre IDs
                var genreIdsArray = first.ContainsKey("genre_ids") ? first["genre_ids"] as ArrayList : null;
                if (genreIdsArray != null && genreIdsArray.Count > 0)
                {
                    var genres = genreIdsArray
                        .Cast<object>()
                        .Select(id => GetTmdbGenre(Convert.ToInt32(id), isEn))
                        .Where(g => g != null);
                    meta.Genre = string.Join(", ", genres);
                }

                return meta;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"TMDB search failed for {category}: {query}");
            }
            return null;
        }

        private static string GetJsonValue(Dictionary<string, object> dict, string key)
        {
            if (dict.ContainsKey(key) && dict[key] != null)
                return dict[key].ToString();
            return null;
        }

        private static string GetTmdbGenre(int id, bool isEn)
        {
            var dictZh = new Dictionary<int, string>
            {
                { 28, "动作" }, { 12, "冒险" }, { 16, "动画" }, { 35, "喜剧" },
                { 80, "犯罪" }, { 99, "纪录" }, { 18, "剧情" }, { 10751, "家庭" },
                { 14, "奇幻" }, { 36, "历史" }, { 27, "恐怖" }, { 10402, "音乐" },
                { 9648, "悬疑" }, { 10749, "爱情" }, { 878, "科幻" }, { 10770, "电视电影" },
                { 53, "惊悚" }, { 10752, "战争" }, { 37, "西部" }
            };
            var dictEn = new Dictionary<int, string>
            {
                { 28, "Action" }, { 12, "Adventure" }, { 16, "Animation" }, { 35, "Comedy" },
                { 80, "Crime" }, { 99, "Documentary" }, { 18, "Drama" }, { 10751, "Family" },
                { 14, "Fantasy" }, { 36, "History" }, { 27, "Horror" }, { 10402, "Music" },
                { 9648, "Mystery" }, { 10749, "Romance" }, { 878, "Science Fiction" }, { 10770, "TV Movie" },
                { 53, "Thriller" }, { 10752, "War" }, { 37, "Western" }
            };
            var dict = isEn ? dictEn : dictZh;
            return dict.ContainsKey(id) ? dict[id] : null;
        }

        private static VideoMetadata GetLocalMetadata(string directory, string baseName, bool isEn)
        {
            var meta = new VideoMetadata();
            string[] possiblePosters = {
                Path.Combine(directory, $"{baseName}-poster.jpg"),
                Path.Combine(directory, "poster.jpg"),
                Path.Combine(directory, "folder.jpg"),
                Path.Combine(directory, $"{baseName}.jpg")
            };

            meta.PosterPath = possiblePosters.FirstOrDefault(File.Exists);

            string nfoPath = null;
            if (File.Exists(Path.Combine(directory, $"{baseName}.nfo"))) nfoPath = Path.Combine(directory, $"{baseName}.nfo");
            else if (File.Exists(Path.Combine(directory, "movie.nfo"))) nfoPath = Path.Combine(directory, "movie.nfo");

            if (nfoPath != null)
            {
                try
                {
                    var xdoc = XDocument.Load(nfoPath);
                    meta.Title = xdoc.Descendants("title").FirstOrDefault()?.Value ?? baseName;
                    meta.Plot = xdoc.Descendants("plot").FirstOrDefault()?.Value ?? (isEn ? "No plot." : "无简介。");
                    var genres = xdoc.Descendants("genre").Select(x => x.Value).ToList();
                    meta.Genre = genres.Any() ? string.Join(", ", genres) : (isEn ? "Video" : "视频");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error reading NFO for {nfoPath}");
                    meta.Title = baseName;
                    meta.Plot = isEn ? "Error reading NFO." : "读取 NFO 出错。";
                    meta.Genre = isEn ? "Video" : "视频";
                }
            }
            else
            {
                meta.Title = baseName;
                meta.Plot = isEn ? "Due to unmatched online database record, no plot is currently available. You may try modifying the filename for better matching precision." : "由于该视频未能自动匹配到线上数据库资料，暂无对应简介。您可以尝试修改文件名来提升匹配精度。";
                meta.Genre = isEn ? "Video" : "视频";
            }

            return meta;
        }
    }
}
