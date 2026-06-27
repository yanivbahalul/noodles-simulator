using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

#nullable enable

namespace NoodlesSimulator.Services;

public class TestSessionService
{
    private const string SessionMetadataSelect =
        "Token,Username,Status,Score,MaxScore,CurrentIndex,StartedUtc,CompletedUtc,CreatedAt,UpdatedAt,QuestionCount,QuestionsStoragePath,AnswersStoragePath";

    private const string SessionFullSelect = SessionMetadataSelect + ",QuestionsJson,AnswersJson";

    private readonly HttpClient _client;
    private readonly string _url;
    private readonly string _apiKey;
    private readonly SupabaseStorageService? _storage;
    private static readonly TimeSpan TestDuration = TimeSpan.FromHours(2);

    public TestSessionService(IConfiguration config, SupabaseStorageService storage = null)
    {
        _storage = storage;
        _url = SupabaseConfiguration.Url(config) ?? string.Empty;
        _apiKey = SupabaseConfiguration.ServiceRoleApiKey(config)
                  ?? SupabaseConfiguration.AnonApiKey(config)
                  ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_url) || string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Missing Supabase ENV vars.");

        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _client.DefaultRequestHeaders.Add("apikey", _apiKey);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public string GenerateToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static int CountQuestions(string questionsJson)
    {
        if (string.IsNullOrWhiteSpace(questionsJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(questionsJson);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string QuestionsStorageKey(string token) => $"test-sessions/{token}/questions.json";
    private static string AnswersStorageKey(string token) => $"test-sessions/{token}/answers.json";

    public async Task<TestSession?> CreateSessionAsync(string username, string questionsJson)
    {
        try
        {
            var token = GenerateToken();
            var now = DateTime.UtcNow;
            var questionCount = CountQuestions(questionsJson);
            var answersJson = "[]";

            string questionsStoragePath = "";
            string answersStoragePath = "";
            var dbQuestionsJson = questionsJson;
            var dbAnswersJson = answersJson;

            if (_storage != null)
            {
                questionsStoragePath = QuestionsStorageKey(token);
                answersStoragePath = AnswersStorageKey(token);
                await _storage.UploadTextAsync(questionsStoragePath, questionsJson ?? "[]");
                await _storage.UploadTextAsync(answersStoragePath, answersJson);
                dbQuestionsJson = "";
                dbAnswersJson = "";
            }

            var session = new TestSession
            {
                Token = token,
                Username = username,
                StartedUtc = now,
                Status = "active",
                QuestionsJson = dbQuestionsJson,
                AnswersJson = dbAnswersJson,
                QuestionsStoragePath = questionsStoragePath,
                AnswersStoragePath = answersStoragePath,
                QuestionCount = questionCount,
                CurrentIndex = 0,
                Score = 0,
                MaxScore = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            var payload = new[]
            {
                new
                {
                    Token = session.Token,
                    Username = session.Username,
                    StartedUtc = session.StartedUtc.ToString("o"),
                    Status = session.Status,
                    QuestionsJson = session.QuestionsJson,
                    AnswersJson = session.AnswersJson,
                    QuestionsStoragePath = session.QuestionsStoragePath,
                    AnswersStoragePath = session.AnswersStoragePath,
                    QuestionCount = session.QuestionCount,
                    CurrentIndex = session.CurrentIndex,
                    Score = session.Score,
                    MaxScore = session.MaxScore,
                    CreatedAt = session.CreatedAt.ToString("o"),
                    UpdatedAt = session.UpdatedAt.ToString("o")
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _client.PostAsync($"{_url}/rest/v1/test_sessions", content);
            if (!res.IsSuccessStatusCode)
            {
                var error = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[TestSessionService] CreateSessionAsync Error - Status: {res.StatusCode}, body length: {error?.Length ?? 0}");
                return null;
            }

            if (_storage != null)
            {
                session.QuestionsJson = questionsJson;
                session.AnswersJson = answersJson;
            }

            return session;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TestSessionService] CreateSessionAsync Exception: {ex.Message}");
            return null;
        }
    }

    public async Task<TestSession?> GetSessionAsync(string token)
    {
        try
        {
            var session = await FetchSessionAsync(token, SessionFullSelect);
            if (session == null) return null;
            await HydrateBlobFieldsAsync(session);
            return session;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetSessionAsync Exception] {ex}");
            return null;
        }
    }

    public async Task<TestSession?> GetActiveSessionAsync(string username)
    {
        try
        {
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/test_sessions?Username=eq.{Uri.EscapeDataString(username)}&Status=eq.active&{SessionFullSelectQuery()}&order=StartedUtc.desc&limit=1");

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GetActiveSessionAsync Error] {res.StatusCode}");
                return null;
            }

            var session = DeserializeFirst(await res.Content.ReadAsStringAsync());
            if (session == null) return null;

            if (IsExpired(session))
            {
                await UpdateSessionStatusAsync(session.Token, "expired");
                return null;
            }

            await HydrateBlobFieldsAsync(session);
            return session;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetActiveSessionAsync Exception] {ex}");
            return null;
        }
    }

    public async Task<bool> UpdateSessionAsync(TestSession session)
    {
        try
        {
            session.UpdatedAt = DateTime.UtcNow;

            if (_storage != null && !string.IsNullOrWhiteSpace(session.AnswersStoragePath))
                await _storage.UploadTextAsync(session.AnswersStoragePath, session.AnswersJson ?? "[]");

            var patch = new
            {
                AnswersJson = string.IsNullOrWhiteSpace(session.AnswersStoragePath) ? session.AnswersJson : "",
                CurrentIndex = session.CurrentIndex,
                Score = session.Score,
                MaxScore = session.MaxScore,
                Status = session.Status,
                CompletedUtc = session.CompletedUtc.HasValue ? session.CompletedUtc.Value.ToString("o") : null,
                UpdatedAt = session.UpdatedAt.ToString("o")
            };

            var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"),
                $"{_url}/rest/v1/test_sessions?Token=eq.{Uri.EscapeDataString(session.Token)}")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=minimal");

            var response = await _client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TestSessionService UpdateSessionAsync] Exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateSessionStatusAsync(string token, string status)
    {
        try
        {
            var patch = new
            {
                Status = status,
                UpdatedAt = DateTime.UtcNow.ToString("o"),
                CompletedUtc = (status == "completed" || status == "expired") ? DateTime.UtcNow.ToString("o") : null
            };

            var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"),
                $"{_url}/rest/v1/test_sessions?Token=eq.{Uri.EscapeDataString(token)}")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=minimal");

            var response = await _client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateSessionStatusAsync Exception] {ex}");
            return false;
        }
    }

    public async Task<List<TestSession>> GetUserSessionsAsync(string username, int limit = 50, bool includeBlobs = false)
    {
        try
        {
            var select = includeBlobs ? SessionFullSelect : SessionMetadataSelect;
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/test_sessions?Username=eq.{Uri.EscapeDataString(username)}&select={select}&order=StartedUtc.desc&limit={limit}");

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GetUserSessionsAsync Error] {res.StatusCode}");
                return new List<TestSession>();
            }

            var sessions = DeserializeList(await res.Content.ReadAsStringAsync());
            if (includeBlobs)
            {
                foreach (var session in sessions)
                    await HydrateBlobFieldsAsync(session);
            }

            return sessions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetUserSessionsAsync Exception] {ex}");
            return new List<TestSession>();
        }
    }

    public bool IsExpired(TestSession session)
    {
        if (session == null) return true;
        var end = session.StartedUtc.Add(TestDuration);
        return DateTime.UtcNow >= end;
    }

    public TimeSpan GetRemainingTime(TestSession session)
    {
        if (session == null) return TimeSpan.Zero;
        var end = session.StartedUtc.Add(TestDuration);
        var remaining = end - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public async Task<List<TestSession>> GetActiveSessionsAsync(int limit = 20)
    {
        try
        {
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/test_sessions?Status=eq.active&select={SessionMetadataSelect}&order=UpdatedAt.desc&limit={limit}");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GetActiveSessionsAsync Error] {res.StatusCode}");
                return new List<TestSession>();
            }

            var sessions = DeserializeList(await res.Content.ReadAsStringAsync());
            return sessions.Where(s => s != null && !IsExpired(s)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetActiveSessionsAsync Exception] {ex}");
            return new List<TestSession>();
        }
    }

    public async Task<List<TestSession>> GetRecentCompletedSessionsAsync(int limit = 25)
    {
        try
        {
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/test_sessions?Status=eq.completed&select={SessionMetadataSelect}&order=CompletedUtc.desc&limit={limit}");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GetRecentCompletedSessionsAsync Error] {res.StatusCode}");
                return new List<TestSession>();
            }

            return DeserializeList(await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetRecentCompletedSessionsAsync Exception] {ex}");
            return new List<TestSession>();
        }
    }

    public async Task<List<(string Username, int ExamCount)>> GetExamCountLeaderboardAsync(int limit = 50)
    {
        try
        {
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/test_sessions?Status=eq.completed&select=Username&limit=5000");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GetExamCountLeaderboardAsync] Error: {res.StatusCode}");
                return new List<(string, int)>();
            }

            var sessions = DeserializeList(await res.Content.ReadAsStringAsync());
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var session in sessions)
            {
                if (string.IsNullOrWhiteSpace(session.Username)) continue;
                counts.TryGetValue(session.Username, out var current);
                counts[session.Username] = current + 1;
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
            Console.WriteLine($"[GetExamCountLeaderboardAsync Exception] {ex}");
            return new List<(string, int)>();
        }
    }

    public async Task<int> PurgeOldSessionsAsync(int olderThanDays = 180)
    {
        if (olderThanDays <= 0) return 0;

        try
        {
            var cutoff = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-olderThanDays).ToString("o"));
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/test_sessions?Status=in.(completed,expired)&UpdatedAt=lt.{cutoff}&select=Token,QuestionsStoragePath,AnswersStoragePath&limit=500");

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[PurgeOldSessionsAsync] list failed: {res.StatusCode}");
                return 0;
            }

            var sessions = DeserializeList(await res.Content.ReadAsStringAsync());
            if (sessions.Count == 0) return 0;

            foreach (var session in sessions)
            {
                if (_storage == null) continue;
                if (!string.IsNullOrWhiteSpace(session.QuestionsStoragePath))
                {
                    try { await _storage.DeleteAsync(session.QuestionsStoragePath); }
                    catch (Exception ex) { Console.WriteLine($"[PurgeOldSessionsAsync] storage: {ex.Message}"); }
                }

                if (!string.IsNullOrWhiteSpace(session.AnswersStoragePath))
                {
                    try { await _storage.DeleteAsync(session.AnswersStoragePath); }
                    catch (Exception ex) { Console.WriteLine($"[PurgeOldSessionsAsync] storage: {ex.Message}"); }
                }
            }

            var deleteRes = await _client.DeleteAsync(
                $"{_url}/rest/v1/test_sessions?Status=in.(completed,expired)&UpdatedAt=lt.{cutoff}");
            if (!deleteRes.IsSuccessStatusCode && deleteRes.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await deleteRes.Content.ReadAsStringAsync();
                Console.WriteLine($"[PurgeOldSessionsAsync] delete failed: {deleteRes.StatusCode} | {body}");
                return 0;
            }

            return sessions.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PurgeOldSessionsAsync Exception] {ex}");
            return 0;
        }
    }

    public async Task DeleteUserSessionsAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;

        try
        {
            var sessions = await GetUserSessionsAsync(username, 500, includeBlobs: false);
            foreach (var session in sessions)
            {
                if (_storage != null)
                {
                    if (!string.IsNullOrWhiteSpace(session.QuestionsStoragePath))
                    {
                        try { await _storage.DeleteAsync(session.QuestionsStoragePath); }
                        catch (Exception ex) { Console.WriteLine($"[DeleteUserSessionsAsync] storage questions: {ex.Message}"); }
                    }

                    if (!string.IsNullOrWhiteSpace(session.AnswersStoragePath))
                    {
                        try { await _storage.DeleteAsync(session.AnswersStoragePath); }
                        catch (Exception ex) { Console.WriteLine($"[DeleteUserSessionsAsync] storage answers: {ex.Message}"); }
                    }
                }
            }

            var safe = Uri.EscapeDataString(username.Trim());
            var res = await _client.DeleteAsync($"{_url}/rest/v1/test_sessions?Username=eq.{safe}");
            if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[DeleteUserSessionsAsync Error] {res.StatusCode} | {body}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeleteUserSessionsAsync Exception] {ex}");
        }
    }

    private static string SessionFullSelectQuery() => $"select={SessionFullSelect}";

    private async Task<TestSession?> FetchSessionAsync(string token, string select)
    {
        var res = await _client.GetAsync(
            $"{_url}/rest/v1/test_sessions?Token=eq.{Uri.EscapeDataString(token)}&select={select}");
        if (!res.IsSuccessStatusCode)
        {
            Console.WriteLine($"[FetchSessionAsync Error] {res.StatusCode}");
            return null;
        }

        return DeserializeFirst(await res.Content.ReadAsStringAsync());
    }

    private async Task HydrateBlobFieldsAsync(TestSession session)
    {
        if (session == null || _storage == null) return;

        try
        {
            if (string.IsNullOrWhiteSpace(session.QuestionsJson) &&
                !string.IsNullOrWhiteSpace(session.QuestionsStoragePath))
            {
                session.QuestionsJson = await _storage.DownloadTextAsync(session.QuestionsStoragePath);
            }

            if (string.IsNullOrWhiteSpace(session.AnswersJson) &&
                !string.IsNullOrWhiteSpace(session.AnswersStoragePath))
            {
                session.AnswersJson = await _storage.DownloadTextAsync(session.AnswersStoragePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TestSessionService] HydrateBlobFields failed for {session.Token}: {ex.Message}");
        }
    }

    private static TestSession? DeserializeFirst(string json)
    {
        var sessions = DeserializeList(json);
        return sessions.FirstOrDefault();
    }

    private static List<TestSession> DeserializeList(string json)
    {
        return JsonSerializer.Deserialize<List<TestSession>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<TestSession>();
    }
}
