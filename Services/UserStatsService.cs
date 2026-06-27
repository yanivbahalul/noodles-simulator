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

public class UserStatsRow
{
    public string Username { get; set; } = "";
    public int Xp { get; set; }
    public int Level { get; set; }
    public int WeeklyCorrect { get; set; }
    public string WeekKey { get; set; } = "";
    public int DailyCorrect { get; set; }
    public string DayKey { get; set; } = "";
    public int DailyChallengeScore { get; set; }
    public string DailyChallengeDate { get; set; } = "";
    public int BestExamScore { get; set; }
    public int BestExamCorrect { get; set; }
}

public class UserStatsService
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly bool _enabled;
    private Dictionary<string, UserStatsRow> _allCache;
    private DateTime _allCacheAt = DateTime.MinValue;
    private static readonly TimeSpan AllCacheTtl = TimeSpan.FromSeconds(15);

    public UserStatsService(IConfiguration config)
    {
        _url = SupabaseConfiguration.Url(config) ?? string.Empty;
        var apiKey = SupabaseConfiguration.ServiceRoleApiKey(config)
                     ?? SupabaseConfiguration.AnonApiKey(config);
        _enabled = !string.IsNullOrWhiteSpace(_url) && !string.IsNullOrWhiteSpace(apiKey);
        if (!_enabled) return;

        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("apikey", apiKey);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public bool IsEnabled => _enabled;

    private const string SelectColumns =
        "Username,Xp,Level,WeeklyCorrect,WeekKey,DailyCorrect,DayKey,DailyChallengeScore,DailyChallengeDate,BestExamScore,BestExamCorrect";

    public async Task<UserStatsRow> GetAsync(string username)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username))
            return null;

        try
        {
            var safe = Uri.EscapeDataString(username.Trim());
            var res = await _client.GetAsync($"{_url}/rest/v1/user_stats?Username=eq.{safe}&select={SelectColumns}");
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<List<UserStatsRow>>(json, AppJson.Options);
            return rows?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserStatsService] GetAsync failed for {username}: {ex.Message}");
            return null;
        }
    }

    public async Task<Dictionary<string, UserStatsRow>> GetAllCachedAsync()
    {
        if (!_enabled) return new Dictionary<string, UserStatsRow>(StringComparer.OrdinalIgnoreCase);
        if (_allCache != null && DateTime.UtcNow - _allCacheAt < AllCacheTtl)
            return _allCache;

        _allCache = await FetchAllAsync();
        _allCacheAt = DateTime.UtcNow;
        return _allCache;
    }

    public void InvalidateCache() => _allCache = null;

    private async Task<Dictionary<string, UserStatsRow>> FetchAllAsync()
    {
        var results = new Dictionary<string, UserStatsRow>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var res = await _client.GetAsync($"{_url}/rest/v1/user_stats?select={SelectColumns}&limit=1000");
            if (!res.IsSuccessStatusCode) return results;

            var json = await res.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<List<UserStatsRow>>(json, AppJson.Options) ?? new List<UserStatsRow>();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Username)) continue;
                results[row.Username] = row;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserStatsService] FetchAll failed: {ex.Message}");
        }

        return results;
    }

    public async Task<bool> UpsertAsync(UserStatsRow row)
    {
        if (!_enabled || row == null || string.IsNullOrWhiteSpace(row.Username))
            return false;

        try
        {
            if (row.Level <= 0)
                row.Level = QuizGamification.LevelFromXp(row.Xp);

            var payload = new[]
            {
                new
                {
                    row.Username,
                    row.Xp,
                    row.Level,
                    row.WeeklyCorrect,
                    WeekKey = row.WeekKey ?? "",
                    row.DailyCorrect,
                    DayKey = row.DayKey ?? "",
                    row.DailyChallengeScore,
                    DailyChallengeDate = row.DailyChallengeDate ?? "",
                    row.BestExamScore,
                    row.BestExamCorrect,
                    UpdatedAt = DateTime.UtcNow.ToString("o")
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("POST"), $"{_url}/rest/v1/user_stats")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            var res = await _client.SendAsync(request);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[UserStatsService] Upsert failed for {row.Username}: {res.StatusCode} | {body}");
            }
            else
            {
                _allCache = null;
            }

            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserStatsService] Upsert exception for {row?.Username}: {ex.Message}");
            return false;
        }
    }

    public static UserStatsRow FromProgress(string username, UserProgressService.UserProgressData data)
    {
        if (data == null) return null;
        return new UserStatsRow
        {
            Username = username,
            Xp = data.Xp,
            Level = QuizGamification.LevelFromXp(data.Xp),
            WeeklyCorrect = data.WeeklyCorrect,
            WeekKey = data.WeekKey ?? "",
            DailyCorrect = data.DailyCorrect,
            DayKey = data.DayKey ?? "",
            DailyChallengeScore = data.DailyChallengeScore,
            DailyChallengeDate = data.DailyChallengeDate ?? "",
            BestExamScore = data.BestExamScore,
            BestExamCorrect = data.BestExamCorrect
        };
    }

    public static void ApplyToProgress(UserStatsRow stats, UserProgressService.UserProgressData data)
    {
        if (stats == null || data == null) return;
        data.Xp = stats.Xp;
        data.WeeklyCorrect = stats.WeeklyCorrect;
        data.WeekKey = stats.WeekKey ?? "";
        data.DailyCorrect = stats.DailyCorrect;
        data.DayKey = stats.DayKey ?? "";
        data.DailyChallengeScore = stats.DailyChallengeScore;
        data.DailyChallengeDate = stats.DailyChallengeDate ?? "";
        data.BestExamScore = stats.BestExamScore;
        data.BestExamCorrect = stats.BestExamCorrect;
    }

    public static void ApplyToUser(UserStatsRow stats, User user)
    {
        if (stats == null || user == null) return;
        user.Xp = stats.Xp;
        user.Level = stats.Level > 0 ? stats.Level : QuizGamification.LevelFromXp(stats.Xp);
        user.WeeklyCorrect = stats.WeeklyCorrect;
        user.WeekKey = stats.WeekKey ?? "";
        user.DailyCorrect = stats.DailyCorrect;
        user.DayKey = stats.DayKey ?? "";
        user.DailyChallengeScore = stats.DailyChallengeScore;
        user.DailyChallengeDate = stats.DailyChallengeDate ?? "";
        user.BestExamScore = stats.BestExamScore;
        user.BestExamCorrect = stats.BestExamCorrect;
    }

    public static UserStatsRow FromUser(User user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.Username)) return null;
        return new UserStatsRow
        {
            Username = user.Username,
            Xp = user.Xp,
            Level = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(user.Xp),
            WeeklyCorrect = user.WeeklyCorrect,
            WeekKey = user.WeekKey ?? "",
            DailyCorrect = user.DailyCorrect,
            DayKey = user.DayKey ?? "",
            DailyChallengeScore = user.DailyChallengeScore,
            DailyChallengeDate = user.DailyChallengeDate ?? "",
            BestExamScore = user.BestExamScore,
            BestExamCorrect = user.BestExamCorrect
        };
    }

    public async Task<bool> UpsertFromUserAsync(User user)
    {
        var row = FromUser(user);
        return row != null && await UpsertAsync(row);
    }

    public static void ApplyFromUser(User user, UserProgressService.UserProgressData data)
    {
        if (user == null || data == null) return;
        if (user.Xp > data.Xp) data.Xp = user.Xp;
        if (!string.IsNullOrWhiteSpace(user.WeekKey) && user.WeeklyCorrect > data.WeeklyCorrect)
        {
            data.WeekKey = user.WeekKey;
            data.WeeklyCorrect = user.WeeklyCorrect;
        }
        if (!string.IsNullOrWhiteSpace(user.DayKey) && user.DailyCorrect > data.DailyCorrect)
        {
            data.DayKey = user.DayKey;
            data.DailyCorrect = user.DailyCorrect;
        }
        if (user.BestExamCorrect > data.BestExamCorrect)
        {
            data.BestExamCorrect = user.BestExamCorrect;
            data.BestExamScore = user.BestExamScore;
        }
    }
}
