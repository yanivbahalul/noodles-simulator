using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace NoodlesSimulator.Models;

public class AuthService
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly string _apiKey;
    private const string PasswordHashPrefix = "pbkdf2$";
    private int _cachedOnlineCount = -1;
    private DateTime _cachedOnlineCountAt = DateTime.MinValue;
    private static readonly TimeSpan OnlineCountCacheTtl = TimeSpan.FromSeconds(30);

    public AuthService(IConfiguration config)
    {
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

            var user = await GetUserAsync(username);
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
                LastSeen = DateTime.UtcNow,
                Xp = 0,
                Level = 1
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(newUser), Encoding.UTF8, "application/json");
        var res = await _client.PostAsync($"{_url}/rest/v1/users", content);
        if (res.IsSuccessStatusCode)
        {
            return true;
        }

        var errorBody = await res.Content.ReadAsStringAsync();
        Console.WriteLine($"[RegisterAsync Error] INSERT failed for {username}: {res.StatusCode} | {errorBody}");
        return false;
    }

    public async Task<User?> GetUserAsync(string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var trimmed = username.Trim();
            var safeUsername = Uri.EscapeDataString(trimmed);

            var res = await _client.GetAsync($"{_url}/rest/v1/users?Username=eq.{safeUsername}&select=*");
            var json = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode)
            {
                var users = JsonSerializer.Deserialize<List<User>>(json, AppJson.Options);
                var exact = users?.FirstOrDefault();
                if (exact != null)
                    return exact;
            }

            var ilikeRes = await _client.GetAsync(
                $"{_url}/rest/v1/users?Username=ilike.{safeUsername}&select=*&limit=5");
            var ilikeJson = await ilikeRes.Content.ReadAsStringAsync();
            if (!ilikeRes.IsSuccessStatusCode)
                return null;

            var matches = JsonSerializer.Deserialize<List<User>>(ilikeJson, AppJson.Options);
            return matches?.FirstOrDefault(u =>
                string.Equals(u.Username, trimmed, StringComparison.OrdinalIgnoreCase));
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
                ["LastSeen"] = (updatedUser.LastSeen ?? DateTime.UtcNow).ToString("o"),
                ["Xp"] = updatedUser.Xp,
                ["Level"] = updatedUser.Level > 0 ? updatedUser.Level : QuizGamification.LevelFromXp(updatedUser.Xp)
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

        var lastSeenUtc = seen.Kind switch
        {
            DateTimeKind.Utc => seen,
            DateTimeKind.Local => seen.ToUniversalTime(),
            _ => DateTime.SpecifyKind(seen, DateTimeKind.Utc)
        };

        return lastSeenUtc > DateTime.UtcNow.AddMinutes(-withinMinutes);
    }

    public async Task<int> GetOnlineUserCountAsync()
    {
        if (_cachedOnlineCount >= 0 && DateTime.UtcNow - _cachedOnlineCountAt < OnlineCountCacheTtl)
            return _cachedOnlineCount;

        try
        {
            var threshold = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-dd'T'HH:mm:ss");
            var safeThreshold = Uri.EscapeDataString(threshold);
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_url}/rest/v1/users?LastSeen=gte.{safeThreshold}&IsBanned=eq.false&IsCheater=eq.false&select=Username");
            request.Headers.Add("Prefer", "count=exact");

            var res = await _client.SendAsync(request);
            if (res.IsSuccessStatusCode &&
                res.Headers.TryGetValues("Content-Range", out var ranges))
            {
                var range = ranges.FirstOrDefault();
                var slash = range?.LastIndexOf('/') ?? -1;
                if (slash >= 0 && int.TryParse(range![(slash + 1)..], out var count))
                {
                    _cachedOnlineCount = count;
                    _cachedOnlineCountAt = DateTime.UtcNow;
                    return count;
                }
            }

            var users = await GetAllUsersLightAsync();
            var fallback = users.Count(u => UserIsOnline(u));
            _cachedOnlineCount = fallback;
            _cachedOnlineCountAt = DateTime.UtcNow;
            return fallback;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetOnlineUserCountAsync Exception] {ex}");
            return _cachedOnlineCount >= 0 ? _cachedOnlineCount : 0;
        }
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
                    return true;

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
            var res = await _client.GetAsync($"{_url}/rest/v1/users?select=*&order=CorrectAnswers.desc&limit={count}");
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
            var res = await _client.GetAsync($"{_url}/rest/v1/users?select=*&TotalAnswered=gte.{minAnswered}&order=CorrectAnswers.desc&limit=500");
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
        try
        {
            var safeUsername = Uri.EscapeDataString(username);
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_url}/rest/v1/users?Username=eq.{safeUsername}");
            request.Headers.Add("Prefer", "return=representation");
            var response = await _client.SendAsync(request);
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
            var res = await _client.GetAsync($"{_url}/rest/v1/users?select=Username,IsCheater,IsBanned,LastSeen,CorrectAnswers,TotalAnswered,Xp,Level,WeeklyCorrect,WeekKey,DailyCorrect,DayKey,DailyChallengeScore,DailyChallengeDate,BestExamScore,BestExamCorrect&limit=1000");
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GetAllUsersLightAsync] extended select failed: {res.StatusCode}, falling back");
                res = await _client.GetAsync($"{_url}/rest/v1/users?select=Username,IsCheater,IsBanned,LastSeen,CorrectAnswers,TotalAnswered&limit=1000");
                json = await res.Content.ReadAsStringAsync();
            }
            return JsonSerializer.Deserialize<List<User>>(json, AppJson.Options) ?? new List<User>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetAllUsersLightAsync Exception] {ex}");
            return new List<User>();
        }
    }

    public async Task<bool> SyncLeaderboardStatsAsync(string username, int weeklyCorrect, string weekKey, int dailyCorrect, string dayKey, int dailyChallengeScore, string dailyChallengeDate, int bestExamScore, int bestExamCorrect)
    {
        try
        {
            var patch = new Dictionary<string, object>
            {
                ["WeeklyCorrect"] = weeklyCorrect,
                ["WeekKey"] = weekKey ?? "",
                ["DailyCorrect"] = dailyCorrect,
                ["DayKey"] = dayKey ?? "",
                ["DailyChallengeScore"] = dailyChallengeScore,
                ["DailyChallengeDate"] = dailyChallengeDate ?? "",
                ["BestExamScore"] = bestExamScore,
                ["BestExamCorrect"] = bestExamCorrect
            };
            var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
            var safeUsername = Uri.EscapeDataString(username);
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{safeUsername}")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=minimal");
            var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[SyncLeaderboardStatsAsync] PATCH failed for {username}: {response.StatusCode} | {body}");
            }
            return response.IsSuccessStatusCode;
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
