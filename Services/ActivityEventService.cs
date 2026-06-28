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

public class ActivityEventService
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly bool _enabled;

    public ActivityEventService(IConfiguration config)
    {
        var rest = SupabaseRestClient.Create(config);
        _url = rest.Url;
        _enabled = rest.Enabled;
        if (!_enabled)
        {
            Console.WriteLine("[ActivityEventService] Disabled — missing Supabase URL or API key");
            return;
        }

        _client = rest.Client!;
    }

    public bool IsEnabled => _enabled;

    public class ActivityEvent
    {
        public long Id { get; set; }
        public string Username { get; set; } = "";
        public string EventType { get; set; } = "";
        public Dictionary<string, object> Payload { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public void Log(string username, string eventType, Dictionary<string, object> payload = null)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(eventType))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await LogAsync(username, eventType, payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ActivityEventService] Log failed for {username}/{eventType}: {ex.Message}");
            }
        });
    }

    public async Task LogAsync(string username, string eventType, Dictionary<string, object> payload = null)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(eventType))
            return;

        var row = new
        {
            username,
            event_type = eventType,
            payload = payload ?? new Dictionary<string, object>(),
            created_at = DateTime.UtcNow.ToString("o")
        };

        var content = new StringContent(JsonSerializer.Serialize(row), Encoding.UTF8, "application/json");
        var res = await _client.PostAsync($"{_url}/rest/v1/activity_events", content);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"[ActivityEventService] Insert failed: {res.StatusCode} | {body}");
        }
    }

    public async Task<int> PurgeOlderThanAsync(int days)
    {
        if (!_enabled || days <= 0) return 0;

        try
        {
            var cutoff = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-days).ToString("o"));
            var res = await _client.DeleteAsync($"{_url}/rest/v1/activity_events?created_at=lt.{cutoff}");
            if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[ActivityEventService] PurgeOlderThan failed: {res.StatusCode} | {body}");
                return 0;
            }

            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActivityEventService] PurgeOlderThan exception: {ex.Message}");
            return 0;
        }
    }

    public async Task<(int Total, int Correct)> GetAnswerStatsSinceAsync(DateTime sinceUtc)
    {
        var activity = await GetAnswerActivitySinceAsync(sinceUtc);
        return (activity.Total, activity.Correct);
    }

    public async Task<(int Total, int Correct, HashSet<string> UsersWithCorrect)> GetAnswerActivitySinceAsync(DateTime sinceUtc)
    {
        if (!_enabled) return (0, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        try
        {
            var since = Uri.EscapeDataString(sinceUtc.ToUniversalTime().ToString("o"));
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/activity_events?select=username,payload&event_type=eq.answer&created_at=gte.{since}&limit=10000");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ActivityEventService] GetAnswerActivitySince failed: {res.StatusCode}");
                return (0, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var total = 0;
            var correct = 0;
            var usersWithCorrect = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                total++;
                var username = row.TryGetProperty("username", out var userEl)
                    ? userEl.GetString() ?? ""
                    : "";
                var isCorrect = row.TryGetProperty("payload", out var payloadEl) &&
                                payloadEl.ValueKind == JsonValueKind.Object &&
                                payloadEl.TryGetProperty("correct", out var correctEl) &&
                                correctEl.ValueKind == JsonValueKind.True;
                if (!isCorrect) continue;

                correct++;
                if (!string.IsNullOrWhiteSpace(username))
                    usersWithCorrect.Add(username);
            }

            return (total, correct, usersWithCorrect);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActivityEventService] GetAnswerActivitySince exception: {ex.Message}");
            return (0, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    public async Task<int> CountEventsSinceAsync(string eventType, DateTime sinceUtc)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(eventType))
            return 0;

        try
        {
            var since = Uri.EscapeDataString(sinceUtc.ToUniversalTime().ToString("o"));
            var type = Uri.EscapeDataString(eventType);
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/activity_events?select=id&event_type=eq.{type}&created_at=gte.{since}&limit=10000");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ActivityEventService] CountEventsSince failed: {res.StatusCode}");
                return 0;
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetArrayLength();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActivityEventService] CountEventsSince exception: {ex.Message}");
            return 0;
        }
    }

    public async Task<List<ActivityEvent>> GetByEventTypeAsync(string eventType, int limit = 200)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(eventType))
            return new List<ActivityEvent>();

        try
        {
            var type = Uri.EscapeDataString(eventType);
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/activity_events?select=id,username,event_type,payload,created_at&event_type=eq.{type}&order=created_at.desc&limit={limit}");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ActivityEventService] GetByEventType failed: {res.StatusCode}");
                return new List<ActivityEvent>();
            }

            return await ParseActivityEventsAsync(await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActivityEventService] GetByEventType exception: {ex.Message}");
            return new List<ActivityEvent>();
        }
    }

    public async Task<List<ActivityEvent>> GetRecentAsync(int limit = 50)
    {
        if (!_enabled) return new List<ActivityEvent>();

        try
        {
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/activity_events?select=id,username,event_type,payload,created_at&order=created_at.desc&limit={limit}");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ActivityEventService] GetRecent failed: {res.StatusCode}");
                return new List<ActivityEvent>();
            }

            return await ParseActivityEventsAsync(await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActivityEventService] GetRecent exception: {ex.Message}");
            return new List<ActivityEvent>();
        }
    }

    private static Task<List<ActivityEvent>> ParseActivityEventsAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<ActivityEvent>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var ev = new ActivityEvent
            {
                Id = row.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var id) ? id : 0,
                Username = row.TryGetProperty("username", out var userEl) ? userEl.GetString() ?? "" : "",
                EventType = row.TryGetProperty("event_type", out var typeEl) ? typeEl.GetString() ?? "" : "",
                CreatedAt = row.TryGetProperty("created_at", out var atEl) &&
                            DateTime.TryParse(atEl.GetString(), out var parsed)
                    ? parsed.ToUniversalTime()
                    : DateTime.MinValue
            };

            if (row.TryGetProperty("payload", out var payloadEl) &&
                payloadEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in payloadEl.EnumerateObject())
                {
                    ev.Payload[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number when prop.Value.TryGetInt32(out var n) => n,
                        JsonValueKind.String => prop.Value.GetString(),
                        _ => prop.Value.GetRawText()
                    };
                }
            }

            list.Add(ev);
        }

        return Task.FromResult(list);
    }
}
