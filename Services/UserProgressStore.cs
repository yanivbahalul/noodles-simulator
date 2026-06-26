using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

/// <summary>
/// Persists UserProgressData to Supabase (source of truth when configured).
/// Local progress/*.json is a cache; DB wins on load when available.
/// </summary>
public class UserProgressStore
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly bool _enabled;

    public UserProgressStore(IConfiguration config)
    {
        _url = SupabaseConfiguration.Url(config) ?? string.Empty;
        var apiKey = SupabaseConfiguration.ServiceRoleApiKey(config)
                     ?? SupabaseConfiguration.AnonApiKey(config);

        _enabled = !string.IsNullOrWhiteSpace(_url) && !string.IsNullOrWhiteSpace(apiKey);
        if (!_enabled)
        {
            Console.WriteLine("[UserProgressStore] Disabled — missing Supabase URL or service key");
            return;
        }

        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("apikey", apiKey);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        Console.WriteLine("[UserProgressStore] Enabled — progress and achievements persist to Supabase");
    }

    public bool IsEnabled => _enabled;

    public (UserProgressService.UserProgressData Data, DateTime? UpdatedAt) TryLoadWithMeta(string username)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username))
            return (null, null);

        try
        {
            var safe = Uri.EscapeDataString(username);
            var res = _client
                .GetAsync($"{_url}/rest/v1/user_progress?Username=eq.{safe}&select=ProgressData,UpdatedAt")
                .GetAwaiter()
                .GetResult();
            if (!res.IsSuccessStatusCode)
                return (null, null);

            var json = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetArrayLength() == 0)
                return (null, null);

            var row = doc.RootElement[0];
            var progressJson = row.GetProperty("ProgressData").GetRawText();
            var data = JsonSerializer.Deserialize<UserProgressService.UserProgressData>(progressJson, AppJson.Options);
            DateTime? updatedAt = null;
            if (row.TryGetProperty("UpdatedAt", out var updatedEl) &&
                DateTime.TryParse(updatedEl.GetString(), out var parsed))
                updatedAt = parsed.ToUniversalTime();

            return (data, updatedAt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] Load failed for {username}: {ex.Message}");
            return (null, null);
        }
    }

    public List<string> TryLoadAchievementKeys(string username)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username))
            return new List<string>();

        try
        {
            var safe = Uri.EscapeDataString(username);
            var res = _client
                .GetAsync($"{_url}/rest/v1/user_achievements?username=eq.{safe}&select=achievement_key")
                .GetAwaiter()
                .GetResult();
            if (!res.IsSuccessStatusCode)
                return new List<string>();

            var json = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var keys = new List<string>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.TryGetProperty("achievement_key", out var keyEl))
                {
                    var key = keyEl.GetString();
                    if (!string.IsNullOrWhiteSpace(key))
                        keys.Add(key);
                }
            }
            return keys;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] Achievement load failed for {username}: {ex.Message}");
            return new List<string>();
        }
    }

    public static void MergeAchievementKeys(UserProgressService.UserProgressData data, IEnumerable<string> keys)
    {
        if (data == null || keys == null) return;
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!data.Achievements.Contains(key, StringComparer.OrdinalIgnoreCase))
                data.Achievements.Add(key);
        }
    }

    private Dictionary<string, int> _allXpCache;
    private DateTime _allXpCacheAt = DateTime.MinValue;
    private static readonly TimeSpan AllXpCacheTtl = TimeSpan.FromSeconds(3);

    public Dictionary<string, int> GetAllXpCached()
    {
        if (!_enabled) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (_allXpCache != null && DateTime.UtcNow - _allXpCacheAt < AllXpCacheTtl)
            return _allXpCache;

        _allXpCache = FetchAllXpFromDb();
        _allXpCacheAt = DateTime.UtcNow;
        return _allXpCache;
    }

    private Dictionary<string, int> FetchAllXpFromDb()
    {
        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var res = _client
                .GetAsync($"{_url}/rest/v1/user_progress?select=Username,ProgressData&limit=1000")
                .GetAwaiter()
                .GetResult();
            if (!res.IsSuccessStatusCode) return results;

            var json = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (!row.TryGetProperty("Username", out var userEl)) continue;
                var username = userEl.GetString();
                if (string.IsNullOrWhiteSpace(username)) continue;
                if (!row.TryGetProperty("ProgressData", out var progressEl)) continue;
                if (!progressEl.TryGetProperty("Xp", out var xpEl)) continue;
                if (!xpEl.TryGetInt32(out var xp) || xp <= 0) continue;
                if (!results.TryGetValue(username, out var current) || xp > current)
                    results[username] = xp;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] FetchAllXp failed: {ex.Message}");
        }

        return results;
    }

    public void Save(string username, UserProgressService.UserProgressData data)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || data == null) return;
        try
        {
            SaveAsync(username, data).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] Save failed for {username}: {ex.Message}");
        }
    }

    private async Task SaveAsync(string username, UserProgressService.UserProgressData data)
    {
        var progressJson = JsonSerializer.Serialize(data, AppJson.Options);
        var payload = new[]
        {
            new
            {
                Username = username,
                ProgressData = JsonSerializer.Deserialize<JsonElement>(progressJson),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(new HttpMethod("POST"), $"{_url}/rest/v1/user_progress")
        {
            Content = content
        };
        request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

        var res = await _client.SendAsync(request);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"user_progress upsert failed: {res.StatusCode} | {body}");
        }

        _allXpCache = null;

        if (data.Achievements?.Count > 0)
            await SyncAllAchievementsAsync(username, data.Achievements);
    }

    private async Task SyncAllAchievementsAsync(string username, IReadOnlyList<string> keys)
    {
        var rows = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(k => new { username, achievement_key = k })
            .ToList();
        if (rows.Count == 0) return;

        const int batchSize = 50;
        for (var i = 0; i < rows.Count; i += batchSize)
        {
            var batch = rows.Skip(i).Take(batchSize).ToList();
            var content = new StringContent(JsonSerializer.Serialize(batch), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("POST"), $"{_url}/rest/v1/user_achievements")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=ignore-duplicates,return=minimal");

            var res = await _client.SendAsync(request);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"user_achievements sync failed: {res.StatusCode} | {body}");
            }
        }
    }
}
