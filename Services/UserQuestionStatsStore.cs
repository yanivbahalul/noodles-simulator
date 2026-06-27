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

public class UserQuestionStatsStore
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly bool _enabled;

    public UserQuestionStatsStore(IConfiguration config)
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

    public async Task<Dictionary<string, UserProgressService.UserQuestionStat>> LoadForUserAsync(string username)
    {
        var results = new Dictionary<string, UserProgressService.UserQuestionStat>(StringComparer.OrdinalIgnoreCase);
        if (!_enabled || string.IsNullOrWhiteSpace(username))
            return results;

        try
        {
            var safe = Uri.EscapeDataString(username.Trim());
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_question_stats?username=eq.{safe}&select=question_id,attempts,correct,last_answered_utc,last_was_correct");
            if (!res.IsSuccessStatusCode) return results;

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var questionId = row.TryGetProperty("question_id", out var qEl) ? qEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(questionId)) continue;

                DateTime lastAnswered = DateTime.MinValue;
                if (row.TryGetProperty("last_answered_utc", out var atEl) &&
                    DateTime.TryParse(atEl.GetString(), out var parsed))
                    lastAnswered = parsed.ToUniversalTime();

                results[questionId] = new UserProgressService.UserQuestionStat
                {
                    Attempts = row.TryGetProperty("attempts", out var aEl) ? aEl.GetInt32() : 0,
                    Correct = row.TryGetProperty("correct", out var cEl) ? cEl.GetInt32() : 0,
                    LastAnsweredUtc = lastAnswered,
                    LastWasCorrect = row.TryGetProperty("last_was_correct", out var lwEl) && lwEl.GetBoolean()
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserQuestionStatsStore] LoadForUser failed for {username}: {ex.Message}");
        }

        return results;
    }

    public async Task RecordAnswerAsync(string username, string questionId, bool isCorrect)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(questionId))
            return;

        try
        {
            var existing = await LoadSingleAsync(username, questionId);
            var attempts = (existing?.Attempts ?? 0) + 1;
            var correct = (existing?.Correct ?? 0) + (isCorrect ? 1 : 0);

            var payload = new[]
            {
                new
                {
                    username = username.Trim(),
                    question_id = questionId,
                    attempts,
                    correct,
                    last_answered_utc = DateTime.UtcNow.ToString("o"),
                    last_was_correct = isCorrect
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("POST"),
                $"{_url}/rest/v1/user_question_stats?on_conflict=username,question_id")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
            var res = await _client.SendAsync(request);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[UserQuestionStatsStore] RecordAnswer failed: {res.StatusCode} | {body}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserQuestionStatsStore] RecordAnswer exception: {ex.Message}");
        }
    }

    private async Task<UserProgressService.UserQuestionStat> LoadSingleAsync(string username, string questionId)
    {
        var safeUser = Uri.EscapeDataString(username.Trim());
        var safeQuestion = Uri.EscapeDataString(questionId);
        var res = await _client.GetAsync(
            $"{_url}/rest/v1/user_question_stats?username=eq.{safeUser}&question_id=eq.{safeQuestion}&select=attempts,correct,last_answered_utc,last_was_correct&limit=1");
        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.GetArrayLength() == 0) return null;

        var row = doc.RootElement[0];
        DateTime lastAnswered = DateTime.MinValue;
        if (row.TryGetProperty("last_answered_utc", out var atEl) &&
            DateTime.TryParse(atEl.GetString(), out var parsed))
            lastAnswered = parsed.ToUniversalTime();

        return new UserProgressService.UserQuestionStat
        {
            Attempts = row.TryGetProperty("attempts", out var aEl) ? aEl.GetInt32() : 0,
            Correct = row.TryGetProperty("correct", out var cEl) ? cEl.GetInt32() : 0,
            LastAnsweredUtc = lastAnswered,
            LastWasCorrect = row.TryGetProperty("last_was_correct", out var lwEl) && lwEl.GetBoolean()
        };
    }

    public async Task ClearForUserAsync(string username)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username)) return;
        var safe = Uri.EscapeDataString(username.Trim());
        await _client.DeleteAsync($"{_url}/rest/v1/user_question_stats?username=eq.{safe}");
    }

    public async Task<List<(string QuestionId, UserProgressService.UserQuestionStat Stat)>> GetRecentAsync(string username, int limit = 10)
    {
        var results = new List<(string, UserProgressService.UserQuestionStat)>();
        if (!_enabled || string.IsNullOrWhiteSpace(username)) return results;

        try
        {
            var safe = Uri.EscapeDataString(username.Trim());
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_question_stats?username=eq.{safe}&select=question_id,attempts,correct,last_answered_utc,last_was_correct&order=last_answered_utc.desc&limit={limit}");
            if (!res.IsSuccessStatusCode) return results;

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var questionId = row.TryGetProperty("question_id", out var qEl) ? qEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(questionId)) continue;

                DateTime lastAnswered = DateTime.MinValue;
                if (row.TryGetProperty("last_answered_utc", out var atEl) &&
                    DateTime.TryParse(atEl.GetString(), out var parsed))
                    lastAnswered = parsed.ToUniversalTime();

                results.Add((questionId, new UserProgressService.UserQuestionStat
                {
                    Attempts = row.TryGetProperty("attempts", out var aEl) ? aEl.GetInt32() : 0,
                    Correct = row.TryGetProperty("correct", out var cEl) ? cEl.GetInt32() : 0,
                    LastAnsweredUtc = lastAnswered,
                    LastWasCorrect = row.TryGetProperty("last_was_correct", out var lwEl) && lwEl.GetBoolean()
                }));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserQuestionStatsStore] GetRecent failed for {username}: {ex.Message}");
        }

        return results;
    }

    public async Task SyncFromDictionaryAsync(string username, Dictionary<string, UserProgressService.UserQuestionStat> stats)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || stats == null || stats.Count == 0)
            return;

        const int batchSize = 100;
        var rows = stats
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value != null)
            .Select(kv => new
            {
                username = username.Trim(),
                question_id = kv.Key,
                attempts = kv.Value.Attempts,
                correct = kv.Value.Correct,
                last_answered_utc = kv.Value.LastAnsweredUtc.ToUniversalTime().ToString("o"),
                last_was_correct = kv.Value.LastWasCorrect
            })
            .ToList();

        for (var i = 0; i < rows.Count; i += batchSize)
        {
            var batch = rows.Skip(i).Take(batchSize).ToList();
            var content = new StringContent(JsonSerializer.Serialize(batch), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("POST"),
                $"{_url}/rest/v1/user_question_stats?on_conflict=username,question_id")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
            var res = await _client.SendAsync(request);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[UserQuestionStatsStore] Sync batch failed: {res.StatusCode} | {body}");
            }
        }
    }
}
