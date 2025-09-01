using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Supabase;

namespace NoodlesSimulator.Services
{
    public class SupabaseStorageService
    {
        private readonly Client _client;
        private readonly string _bucket;
        private readonly int _ttlSeconds;
        private bool _initialized;
        private List<string> _listCache;
        private DateTime _listCacheAt;
        private readonly TimeSpan _listTtl = TimeSpan.FromMinutes(5);
        private readonly Dictionary<string, (string url, DateTime cachedAt)> _signedUrlCache = new();
        private readonly TimeSpan _signedUrlTtl;

        public SupabaseStorageService(string url, string serviceRoleKey, string bucket, int ttlSeconds = 3600)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Supabase URL is required.", nameof(url));
            if (string.IsNullOrWhiteSpace(serviceRoleKey))
                throw new ArgumentException("Service Role Key is required.", nameof(serviceRoleKey));
            if (string.IsNullOrWhiteSpace(bucket))
                throw new ArgumentException("Bucket name is required.", nameof(bucket));

            _client = new Client(url, serviceRoleKey);
            _bucket = bucket;
            _ttlSeconds = ttlSeconds > 0 ? ttlSeconds : 3600;
            _signedUrlTtl = TimeSpan.FromSeconds(Math.Max(60, _ttlSeconds - 60));
        }

        private async Task EnsureInitAsync()
        {
            if (_initialized) return;
            await _client.InitializeAsync();
            _initialized = true;
        }

        public async Task<string> GetSignedUrlAsync(string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
                throw new ArgumentException("objectPath is required.", nameof(objectPath));

            // Return cached if fresh
            if (_signedUrlCache.TryGetValue(objectPath, out var entry))
            {
                if (DateTime.UtcNow - entry.cachedAt < _signedUrlTtl)
                    return entry.url;
            }

            await EnsureInitAsync();
            var from = _client.Storage.From(_bucket);
            var url = await from.CreateSignedUrl(objectPath, _ttlSeconds);
            _signedUrlCache[objectPath] = (url, DateTime.UtcNow);
            return url;
        }

        public async Task<Dictionary<string, string>> GetSignedUrlsAsync(IEnumerable<string> objectPaths)
        {
            await EnsureInitAsync();
            var from = _client.Storage.From(_bucket);

            var dict = new Dictionary<string, string>();
            var tasks = new List<Task>();
            foreach (var p in objectPaths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;

                if (_signedUrlCache.TryGetValue(p, out var entry) && (DateTime.UtcNow - entry.cachedAt < _signedUrlTtl))
                {
                    dict[p] = entry.url;
                    continue;
                }

                tasks.Add(Task.Run(async () =>
                {
                    var url = await from.CreateSignedUrl(p, _ttlSeconds);
                    _signedUrlCache[p] = (url, DateTime.UtcNow);
                    lock (dict)
                    {
                        dict[p] = url;
                    }
                }));
            }
            await Task.WhenAll(tasks);
            return dict;
        }

        public async Task UploadAsync(Stream fileStream, string objectPath, string contentType = "application/octet-stream", bool overwrite = false)
        {
            if (fileStream is null) throw new ArgumentNullException(nameof(fileStream));
            if (string.IsNullOrWhiteSpace(objectPath))
                throw new ArgumentException("objectPath is required.", nameof(objectPath));

            await EnsureInitAsync();
            var from = _client.Storage.From(_bucket);

            using (var ms = new MemoryStream())
            {
                await fileStream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                await from.Upload(bytes, objectPath, new Supabase.Storage.FileOptions
                {
                    Upsert = overwrite,
                    ContentType = contentType
                });
            }
        }

        public async Task DeleteAsync(string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
                throw new ArgumentException("objectPath is required.", nameof(objectPath));

            await EnsureInitAsync();
            var from = _client.Storage.From(_bucket);
            await from.Remove(objectPath);
        }

        public async Task<List<string>> ListFilesAsync(string prefix = "")
        {
            // Use simple in-memory cache to avoid listing on every request
            if (_listCache != null && (DateTime.UtcNow - _listCacheAt) < _listTtl)
                return _listCache;

            await EnsureInitAsync();
            var from = _client.Storage.From(_bucket);
            var list = new List<string>();

            // Paginate to fetch all files (SDK default may be limited)
            int limit = 1000;
            int offset = 0;
            while (true)
            {
                Supabase.Storage.SearchOptions options = new Supabase.Storage.SearchOptions
                {
                    Limit = limit,
                    Offset = offset
                };

                var page = await from.List(prefix, options);
                if (page == null || page.Count == 0)
                    break;

                foreach (var item in page)
                {
                    var name = string.IsNullOrWhiteSpace(prefix) ? item.Name : (prefix.TrimEnd('/') + "/" + item.Name);
                    if (item.Id != null)
                        list.Add(name);
                    else if (!name.EndsWith("/"))
                        list.Add(name);
                }

                if (page.Count < limit)
                    break;
                offset += page.Count;
            }
            _listCache = list;
            _listCacheAt = DateTime.UtcNow;
            return list;
        }

        /// <summary>
        /// Extracts the original file name from a signed URL
        /// </summary>
        /// <param name="signedUrl">The signed URL from Supabase Storage</param>
        /// <returns>The original file name or null if extraction fails</returns>
        public static string ExtractFileNameFromSignedUrl(string signedUrl)
        {
            if (string.IsNullOrWhiteSpace(signedUrl))
                return null;

            try
            {
                // Parse the JWT token from the URL
                var uri = new Uri(signedUrl);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var token = query["token"];
                
                if (string.IsNullOrWhiteSpace(token))
                    return null;

                // Decode the JWT token (we only need the payload)
                var parts = token.Split('.');
                if (parts.Length != 3)
                    return null;

                var payload = parts[1];
                // Add padding if needed
                payload = payload.PadRight(4 * ((payload.Length + 3) / 4), '=');
                
                // Decode base64url to base64
                payload = payload.Replace('-', '+').Replace('_', '/');
                
                var jsonBytes = Convert.FromBase64String(payload);
                var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                
                // Parse the JSON to get the "url" field
                var tokenData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (tokenData.TryGetValue("url", out var urlObj))
                {
                    var url = urlObj.ToString();
                    // Extract the file name from the URL
                    var fileName = System.IO.Path.GetFileName(url);
                    return fileName;
                }
            }
            catch (Exception)
            {
                // If extraction fails, return null
            }
            
            return null;
        }
    }
}
