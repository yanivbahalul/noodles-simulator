using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
            var results = await from.List(prefix);
            var list = new List<string>();
            foreach (var item in results)
            {
                var name = string.IsNullOrWhiteSpace(prefix) ? item.Name : (prefix.TrimEnd('/') + "/" + item.Name);
                if (item.Id != null)
                    list.Add(name);
                else if (!name.EndsWith("/"))
                    list.Add(name);
            }
            _listCache = list;
            _listCacheAt = DateTime.UtcNow;
            return list;
        }
    }
}
