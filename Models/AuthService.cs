using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Services;

#nullable enable

namespace NoodlesSimulator.Models;

public class AuthService
{
    public const string UserSelectPublic =
        "Username,IsCheater,IsBanned,LastSeen,CorrectAnswers,TotalAnswered,DismissedNotices";

    public const string UserSelectLeaderboard =
        "Username,CorrectAnswers,TotalAnswered,LastSeen,IsCheater,IsBanned";

    private const string UserSelectAuth = UserSelectPublic + ",Password";

    private readonly HttpClient _client;
    private readonly HttpClient _adminClient;
    private readonly string _url;
    private readonly string _apiKey;
    private const string PasswordHashPrefix = "pbkdf2$";
    private readonly UserStatsService? _stats;
    private int _cachedOnlineCount = -1;
    private DateTime _cachedOnlineCountAt = DateTime.MinValue;
    private static readonly TimeSpan OnlineCountCacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PresenceTouchMinInterval = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, DateTime> _lastPresenceByUser = new();

    public AuthService(IConfiguration config, UserStatsService? stats = null)
    {
        _stats = stats;
        _url = SupabaseConfiguration.Url(config)
               ?? throw new Exception("Missing Supabase URL ENV var (SUPABASE_URL).");

        _apiKey = SupabaseConfiguration.AnonApiKey(config)
                  ?? throw new Exception(
                      "Missing Supabase anon key (set SUPABASE_ANON_KEY, ANON_PUBLIC, or SUPABASE_KEY).");

        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6)
        };
        _client.DefaultRequestHeaders.Add("apikey", _apiKey);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var serviceKey = SupabaseConfiguration.ServiceRoleApiKey(config);
        if (!string.IsNullOrWhiteSpace(serviceKey) && serviceKey != _apiKey)
        {
            _adminClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _adminClient.DefaultRequestHeaders.Add("apikey", serviceKey);
            _adminClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");
        }
        else
        {
            _adminClient = _client;
        }
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            username = username.Trim();
            password = password.Trim();

            var user = await GetUserForAuthAsync(username);
            if (user == null || string.IsNullOrWhiteSpace(user.Password))
            {
                return null;
            }

            var stored = user.Password;
            var isValid = VerifyPassword(stored, password);
            if (!isValid)
            {
                return null;
            }

            if (!IsHashedPassword(stored))
            {
                user.Password = HashPassword(password);
                _ = UpdateUserAsync(user);
            }

            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthenticateAsync Exception] {ex}");
            return null;
        }
    }

    public async Task<bool> RegisterAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
            return false;

        if (username.Length < 5 || password.Length < 5)
            return false;

        // Username is intentionally restricted to keep URLs/filters safe and consistent.
        // Password should allow common symbols; we store a PBKDF2 hash, not the raw password.
        if (!Regex.IsMatch(username, "^[a-zA-Z0-9א-ת]+$"))
            return false;

        var existingUser = await GetUserAsync(username);
        if (existingUser != null)
            return false;

        // Keep the insert payload aligned to actual DB columns.
        // (Some deployments don't have an 'IsAdmin' column; admin is derived elsewhere.)
        var newUser = new[]
        {
            new
            {
                Username = username,
                Password = HashPassword(password),
                CorrectAnswers = 0,
                TotalAnswered = 0,
                IsCheater = false,
                IsBanned = false,
                LastSeen = DateTime.UtcNow
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(newUser), Encoding.UTF8, "application/json");
        var res = await _adminClient.PostAsync($"{_url}/rest/v1/users", content);
        if (res.IsSuccessStatusCode)
        {
            if (_stats?.IsEnabled == true)
                await _stats.UpsertAsync(new UserStatsRow { Username = username, Level = 1 });
            return true;
        }

        var errorBody = await res.Content.ReadAsStringAsync();
        Console.WriteLine($"[RegisterAsync Error] INSERT failed for {username}: {res.StatusCode} | {errorBody}");
        return false;
    }

    public async Task<User?> GetUserAsync(string username) =>
        await FetchUserAsync(username, includePassword: false);

    public async Task<User?> GetUserForAuthAsync(string username) =>
        await FetchUserAsync(username, includePassword: true);

    private async Task MergeStatsIntoUserAsync(User user)
    {
        if (user == null || _stats?.IsEnabled != true)
            return;

        var row = await _stats.GetAsync(user.Username);
        if (row != null)
            UserStatsService.ApplyToUser(row, user);
    }

    private async Task<User?> FetchUserAsync(string username, bool includePassword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var trimmed = username.Trim();
            var safeUsername = Uri.EscapeDataString(trimmed);
            var select = includePassword ? UserSelectAuth : UserSelectPublic;

            var res = await _client.GetAsync($"{_url}/rest/v1/users?Username=eq.{safeUsername}&select={select}");
            var json = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode)
            {
                var users = JsonSerializer.Deserialize<List<User>>(json, AppJson.Options);
                var exact = users?.FirstOrDefault();
                if (exact != null)
                {
                    await MergeStatsIntoUserAsync(exact);
                    return exact;
                }
            }

            var ilikeRes = await _client.GetAsync(
                $"{_url}/rest/v1/users?Username=ilike.{safeUsername}&select={select}&limit=5");
            var ilikeJson = await ilikeRes.Content.ReadAsStringAsync();
            if (!ilikeRes.IsSuccessStatusCode)
                return null;

            var matches = JsonSerializer.Deserialize<List<User>>(ilikeJson, AppJson.Options);
            var match = matches?.FirstOrDefault(u =>
                string.Equals(u.Username, trimmed, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                await MergeStatsIntoUserAsync(match);
            return match;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetUserAsync Exception] {ex}");
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(User updatedUser)
    {
        try
        {
            var patch = new Dictionary<string, object>
            {
                ["CorrectAnswers"] = updatedUser.CorrectAnswers,
                ["TotalAnswered"] = updatedUser.TotalAnswered,
                ["IsCheater"] = updatedUser.IsCheater,
                ["IsBanned"] = updatedUser.IsBanned,
                ["LastSeen"] = (updatedUser.LastSeen ?? DateTime.UtcNow).ToString("o")
            };
            if (!string.IsNullOrWhiteSpace(updatedUser.Password))
            {
                patch["Password"] = updatedUser.Password;
            }

            var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
            var safeUsername = Uri.EscapeDataString(updatedUser.Username);
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{safeUsername}")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[UpdateUserAsync Error] PATCH failed for {updatedUser.Username}: {response.StatusCode} | {errorBody}");
                return false;
            }

            if (_stats?.IsEnabled == true)
                await _stats.UpsertFromUserAsync(updatedUser);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateUserAsync Exception] {ex}");
            return false;
        }
    }

    public static bool UserIsOnline(User? user, int withinMinutes = 5)
    {
        if (user?.LastSeen is not DateTime seen) return false;
        if (user.IsBanned || user.IsCheater) return false;

        var lastSeenUtc = seen.Kind switch
        {
            DateTimeKind.Utc => seen,
            DateTimeKind.Local => seen.ToUniversalTime(),
            _ => DateTime.SpecifyKind(seen, DateTimeKind.Utc)
        };

        return lastSeenUtc > DateTime.UtcNow.AddMinutes(-withinMinutes);
    }

    public static int CountOnlineUsers(IEnumerable<User> users) =>
        users.Count(u => UserIsOnline(u));

    public void InvalidateOnlineCountCache()
    {
        _cachedOnlineCount = -1;
        _cachedOnlineCountAt = DateTime.MinValue;
    }

    public async Task<int> GetOnlineUserCountAsync()
    {
        if (_cachedOnlineCount >= 0 && DateTime.UtcNow - _cachedOnlineCountAt < OnlineCountCacheTtl)
            return _cachedOnlineCount;

        try
        {
            var users = await GetAllUsersLightAsync();
            var count = CountOnlineUsers(users);
            _cachedOnlineCount = count;
            _cachedOnlineCountAt = DateTime.UtcNow;
            return count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetOnlineUserCountAsync Exception] {ex}");
            return _cachedOnlineCount >= 0 ? _cachedOnlineCount : 0;
        }
    }

    public async Task<bool> TouchLastSeenIfDueAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        var now = DateTime.UtcNow;
        if (_lastPresenceByUser.TryGetValue(username, out var last) && now - last < PresenceTouchMinInterval)
            return false;

        if (!await TouchLastSeenAsync(username, now))
            return false;

        _lastPresenceByUser[username] = now;
        InvalidateOnlineCountCache();
        return true;
    }

    public bool HasDismissedNotice(User? user, string noticeId)
    {
        if (user?.DismissedNotices == null || string.IsNullOrWhiteSpace(noticeId))
        {
            return false;
        }

        return user.DismissedNotices.Contains(noticeId, StringComparer.Ordinal);
    }

    public async Task<bool> DismissNoticeAsync(string username, string noticeId)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(noticeId))
        {
            return false;
        }

        var user = await GetUserAsync(username);
        if (user == null)
        {
            return false;
        }

        var notices = user.DismissedNotices ?? new List<string>();
        if (notices.Contains(noticeId, StringComparer.Ordinal))
        {
            return true;
        }

        notices = new List<string>(notices) { noticeId };
        var safeUsername = Uri.EscapeDataString(username);
        var candidateColumns = new[] { "DismissedNotices", "dismissed_notices" };

        foreach (var col in candidateColumns)
        {
            try
            {
                var patch = new Dictionary<string, object> { [col] = notices };
                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{safeUsername}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DismissNoticeAsync] PATCH with '{col}' failed for {username}: {response.StatusCode} | {errorBody}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DismissNoticeAsync] Exception with '{col}' for {username}: {ex.Message}");
            }
        }

        return false;
    }

    public async Task<bool> TouchLastSeenAsync(string username, DateTime? at = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        var when = (at ?? DateTime.UtcNow).ToString("o");
        var safeUsername = Uri.EscapeDataString(username);
        var candidateColumns = new[] { "LastSeen", "last_seen" };

        foreach (var col in candidateColumns)
        {
            try
            {
                var patch = new Dictionary<string, string> { [col] = when };
                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{safeUsername}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    InvalidateOnlineCountCache();
                    return true;
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[TouchLastSeenAsync] PATCH with '{col}' failed for {username}: {response.StatusCode} | {errorBody}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TouchLastSeenAsync] Exception with '{col}' for {username}: {ex.Message}");
            }
        }

        return false;
    }

    public async Task<List<User>> GetTopUsersAsync(int count = 5)
    {
        try
        {
            var res = await _client.GetAsync($"{_url}/rest/v1/users?select={UserSelectLeaderboard}&order=CorrectAnswers.desc&limit={count}");
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<User>>(json, AppJson.Options) ?? new List<User>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetTopUsersAsync Exception] {ex}");
            return new List<User>();
        }
    }

    public async Task<List<User>> GetTopUsersBySuccessRateAsync(int count = 50, int minAnswered = 10)
    {
        try
        {
            var res = await _client.GetAsync($"{_url}/rest/v1/users?select={UserSelectLeaderboard}&TotalAnswered=gte.{minAnswered}&order=CorrectAnswers.desc&limit={Math.Max(count * 3, 100)}");
            var json = await res.Content.ReadAsStringAsync();
            var users = JsonSerializer.Deserialize<List<User>>(json, AppJson.Options) ?? new List<User>();
            return users
                .Where(u => u.TotalAnswered >= minAnswered)
                .OrderByDescending(u => (double)u.CorrectAnswers / u.TotalAnswered)
                .ThenByDescending(u => u.CorrectAnswers)
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetTopUsersBySuccessRateAsync Exception] {ex}");
            return new List<User>();
        }
    }

    public async Task<bool> DeleteUserAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        try
        {
            var safeUsername = Uri.EscapeDataString(username.Trim());
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_url}/rest/v1/users?Username=eq.{safeUsername}");
            request.Headers.Add("Prefer", "return=minimal");
            var response = await _adminClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DeleteUserAsync Error] {response.StatusCode} | {body}");
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeleteUserAsync Exception] {ex}");
            return false;
        }
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            var res = await _client.GetAsync($"{_url}/rest/v1/users?select=Username&limit=1");
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CheckConnectionAsync Exception] {ex}");
            return false;
        }
    }

    public async Task<List<User>> GetAllUsersLightAsync()
    {
        try
        {
            var res = await _client.GetAsync($"{_url}/rest/v1/users?select=Username,IsCheater,IsBanned,LastSeen,CorrectAnswers,TotalAnswered,DismissedNotices&limit=1000");
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GetAllUsersLightAsync] select failed: {res.StatusCode}");
                return new List<User>();
            }

            var users = JsonSerializer.Deserialize<List<User>>(json, AppJson.Options) ?? new List<User>();
            if (_stats?.IsEnabled == true)
            {
                var statsMap = await _stats.GetAllCachedAsync();
                foreach (var user in users)
                {
                    if (string.IsNullOrWhiteSpace(user.Username)) continue;
                    if (!statsMap.TryGetValue(user.Username, out var row)) continue;
                    UserStatsService.ApplyToUser(row, user);
                }
            }

            return users;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetAllUsersLightAsync Exception] {ex}");
            return new List<User>();
        }
    }

    public async Task<bool> SyncLeaderboardStatsAsync(string username, int weeklyCorrect, string weekKey, int dailyCorrect, string dayKey, int dailyChallengeScore, string dailyChallengeDate, int bestExamScore, int bestExamCorrect)
    {
        if (_stats?.IsEnabled != true)
            return false;

        try
        {
            var row = await _stats.GetAsync(username) ?? new UserStatsRow { Username = username };
            row.WeeklyCorrect = weeklyCorrect;
            row.WeekKey = weekKey ?? "";
            row.DailyCorrect = dailyCorrect;
            row.DayKey = dayKey ?? "";
            row.DailyChallengeScore = dailyChallengeScore;
            row.DailyChallengeDate = dailyChallengeDate ?? "";
            row.BestExamScore = bestExamScore;
            row.BestExamCorrect = bestExamCorrect;
            return await _stats.UpsertAsync(row);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SyncLeaderboardStatsAsync Exception] {ex}");
            return false;
        }
    }

    public List<User> GetWeeklyLeaderboardFromUsers(IEnumerable<User> users, int limit = 50)
    {
        var weekKey = Services.UserProgressService.GetWeekKey();
        return users
            .Where(u => !u.IsBanned && !u.IsCheater && u.WeekKey == weekKey && u.WeeklyCorrect > 0)
            .OrderByDescending(u => u.WeeklyCorrect)
            .Take(limit)
            .ToList();
    }

    public List<User> GetDailyLeaderboardFromUsers(IEnumerable<User> users, string date, int limit = 50)
    {
        return users
            .Where(u => !u.IsBanned && !u.IsCheater && u.DayKey == date && u.DailyCorrect > 0)
            .OrderByDescending(u => u.DailyCorrect)
            .Take(limit)
            .ToList();
    }

    public List<User> GetExamLeaderboardFromUsers(IEnumerable<User> users, int limit = 50)
    {
        return users
            .Where(u => !u.IsBanned && !u.IsCheater && u.BestExamScore > 0)
            .OrderByDescending(u => u.BestExamScore)
            .ThenByDescending(u => u.BestExamCorrect)
            .Take(limit)
            .ToList();
    }

    private static bool IsHashedPassword(string storedPassword)
    {
        return !string.IsNullOrWhiteSpace(storedPassword) && storedPassword.StartsWith(PasswordHashPrefix, StringComparison.Ordinal);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
        return $"{PasswordHashPrefix}{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string storedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(storedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        if (!IsHashedPassword(storedPassword))
        {
            return string.Equals(storedPassword, providedPassword, StringComparison.Ordinal);
        }

        var parts = storedPassword.Split('$');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expectedHash = Convert.FromBase64String(parts[2]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(providedPassword, salt, 100000, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
