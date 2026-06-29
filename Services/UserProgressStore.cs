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
/// Persists slim ProgressDataDocument to Supabase (source of truth when configured).
/// Achievements and stats live in dedicated tables.
/// </summary>
public class UserProgressStore
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly bool _enabled;
    private readonly UserStatsService _stats;

    public UserProgressStore(IConfiguration config, UserStatsService stats = null)
    {
        _stats = stats;
        var rest = SupabaseRestClient.Create(config, timeoutSeconds: 15);
        _url = rest.Url;
        _enabled = rest.Enabled;
        if (!_enabled)
        {
            Console.WriteLine("[UserProgressStore] Disabled — missing Supabase URL or service key");
            return;
        }

        _client = rest.Client!;
        Console.WriteLine("[UserProgressStore] Enabled — progress persists to Supabase");
    }

    public bool IsEnabled => _enabled;

    public class ProgressUpdateRow
    {
        public string Username { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }

    public async Task<List<ProgressUpdateRow>> FetchRecentProgressUpdatesAsync(int limit = 25)
    {
        if (!_enabled) return new List<ProgressUpdateRow>();

        try
        {
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_progress?select=Username,UpdatedAt&order=UpdatedAt.desc&limit={limit}");
            if (!res.IsSuccessStatusCode) return new List<ProgressUpdateRow>();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var rows = new List<ProgressUpdateRow>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (!row.TryGetProperty("Username", out var userEl)) continue;
                var username = userEl.GetString();
                if (string.IsNullOrWhiteSpace(username)) continue;
                if (!row.TryGetProperty("UpdatedAt", out var atEl) ||
                    !DateTime.TryParse(atEl.GetString(), out var updatedAt))
                    continue;

                rows.Add(new ProgressUpdateRow
                {
                    Username = username,
                    UpdatedAt = updatedAt.ToUniversalTime()
                });
            }

            return rows;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] FetchRecentProgressUpdates failed: {ex.Message}");
            return new List<ProgressUpdateRow>();
        }
    }

    public async Task<List<(string Username, int AchievementCount)>> GetAchievementCountLeaderboardAsync(int limit = 50)
    {
        if (!_enabled) return new List<(string, int)>();

        try
        {
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_achievements?select=username&limit=5000");
            if (!res.IsSuccessStatusCode) return new List<(string, int)>();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (!row.TryGetProperty("username", out var userEl)) continue;
                var username = userEl.GetString();
                if (string.IsNullOrWhiteSpace(username)) continue;
                counts.TryGetValue(username, out var current);
                counts[username] = current + 1;
            }

            return counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] GetAchievementCountLeaderboard failed: {ex.Message}");
            return new List<(string, int)>();
        }
    }

    public (UserProgressService.ProgressDataDocument Data, DateTime? UpdatedAt) TryLoadDocumentWithMeta(string username)
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
            UserProgressService.ProgressDataDocument data;

            using (var progressDoc = JsonDocument.Parse(progressJson))
            {
                if (progressDoc.RootElement.TryGetProperty("QuestionStats", out _) ||
                    progressDoc.RootElement.TryGetProperty("Xp", out _))
                {
                    var legacy = JsonSerializer.Deserialize<UserProgressService.UserProgressData>(progressJson, AppJson.Options);
                    data = legacy != null ? UserProgressService.ToDocument(legacy) : new UserProgressService.ProgressDataDocument();
                }
                else
                {
                    data = JsonSerializer.Deserialize<UserProgressService.ProgressDataDocument>(progressJson, AppJson.Options)
                           ?? new UserProgressService.ProgressDataDocument();
                }
            }

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

    public Dictionary<string, int> GetAllXpCached()
    {
        if (_stats?.IsEnabled == true)
        {
            var all = _stats.GetAllCachedAsync().GetAwaiter().GetResult();
            return all.ToDictionary(kv => kv.Key, kv => kv.Value.Xp, StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public void SaveDocument(string username, UserProgressService.ProgressDataDocument data)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || data == null) return;
        try
        {
            SaveDocumentAsync(username, data).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] Save failed for {username}: {ex.Message}");
        }
    }

    private async Task SaveDocumentAsync(string username, UserProgressService.ProgressDataDocument data)
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

        _stats?.InvalidateCache();
    }

    public void SyncAchievements(string username, IEnumerable<string> keys)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username)) return;
        _ = Task.Run(async () =>
        {
            try { await SyncAchievementsAsync(username, keys); }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserProgressStore] SyncAchievements failed for {username}: {ex.Message}");
            }
        });
    }

    public void ClearAchievements(string username)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username)) return;
        try
        {
            ClearAchievementsAsync(username).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressStore] ClearAchievements failed for {username}: {ex.Message}");
        }
    }

    private async Task ClearAchievementsAsync(string username)
    {
        var safe = Uri.EscapeDataString(username.Trim());
        var res = await _client.DeleteAsync($"{_url}/rest/v1/user_achievements?username=eq.{safe}");
        if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"user_achievements delete failed: {res.StatusCode} | {body}");
        }
    }

    private async Task SyncAchievementsAsync(string username, IEnumerable<string> keys)
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
            var request = new HttpRequestMessage(new HttpMethod("POST"),
                $"{_url}/rest/v1/user_achievements?on_conflict=username,achievement_key")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=ignore-duplicates,return=minimal");

            var res = await _client.SendAsync(request);
            if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.Conflict)
            {
                var body = await res.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"user_achievements sync failed: {res.StatusCode} | {body}");
            }
        }
    }
}
