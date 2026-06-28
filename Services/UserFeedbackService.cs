using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class UserFeedbackService
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly bool _enabled;

    public UserFeedbackService(IConfiguration config)
    {
        var rest = SupabaseRestClient.Create(config);
        _url = rest.Url;
        _enabled = rest.Enabled;
        if (!_enabled)
        {
            Console.WriteLine("[UserFeedbackService] Disabled — missing Supabase URL or API key");
            return;
        }

        _client = rest.Client!;
    }

    public bool IsEnabled => _enabled;

    public class UserFeedbackEntry
    {
        public long Id { get; set; }
        public string Username { get; set; } = "";
        public string CampaignId { get; set; } = "";
        public int Rating { get; set; }
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public bool IsLater => Rating < 1;
        public int Milestone => FeedbackCampaigns.ParseMilestoneFromCampaignId(CampaignId);
    }

    /// <summary>True if the user ever submitted a rated response (not merely "later").</summary>
    public async Task<bool> HasSubmittedFeedbackAsync(string username)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username))
            return false;

        try
        {
            var safeUser = Uri.EscapeDataString(username.Trim());
            var prefix = Uri.EscapeDataString($"{FeedbackCampaigns.MilestoneCampaignPrefix}%");
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_feedback?select=id&username=eq.{safeUser}&rating=gte.1&campaign_id=like.{prefix}&limit=1");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[UserFeedbackService] HasSubmittedFeedback failed: {res.StatusCode}");
                return false;
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetArrayLength() > 0)
                return true;

            var legacy = Uri.EscapeDataString(FeedbackCampaigns.LegacyCampaignId);
            var legacyRes = await _client.GetAsync(
                $"{_url}/rest/v1/user_feedback?select=id&username=eq.{safeUser}&rating=gte.1&campaign_id=eq.{legacy}&limit=1");
            if (!legacyRes.IsSuccessStatusCode)
                return false;

            var legacyJson = await legacyRes.Content.ReadAsStringAsync();
            using var legacyDoc = JsonDocument.Parse(legacyJson);
            return legacyDoc.RootElement.GetArrayLength() > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserFeedbackService] HasSubmittedFeedback exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> HasRespondedAsync(string username, string campaignId)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(campaignId))
            return false;

        try
        {
            var safeUser = Uri.EscapeDataString(username.Trim());
            var safeCampaign = Uri.EscapeDataString(campaignId.Trim());
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_feedback?select=id&username=eq.{safeUser}&campaign_id=eq.{safeCampaign}&limit=1");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[UserFeedbackService] HasResponded failed: {res.StatusCode}");
                return false;
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetArrayLength() > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserFeedbackService] HasResponded exception: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, bool AlreadyResponded)> RecordLaterAsync(string username, string campaignId)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(campaignId))
            return (false, false);

        if (!FeedbackCampaigns.IsMilestoneCampaignId(campaignId))
            return (false, false);

        try
        {
            if (await HasRespondedAsync(username, campaignId))
                return (false, true);

            var row = new
            {
                username = username.Trim(),
                campaign_id = campaignId.Trim(),
                rating = 0,
                message = "",
                created_at = DateTime.UtcNow.ToString("o")
            };

            var content = new StringContent(JsonSerializer.Serialize(row), Encoding.UTF8, "application/json");
            var res = await _client.PostAsync($"{_url}/rest/v1/user_feedback", content);
            if (res.IsSuccessStatusCode)
                return (true, false);

            if (res.StatusCode == HttpStatusCode.Conflict)
                return (false, true);

            var body = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"[UserFeedbackService] RecordLater failed: {res.StatusCode} | {body}");
            return (false, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserFeedbackService] RecordLater exception: {ex.Message}");
            return (false, false);
        }
    }

    public async Task<(bool Success, bool AlreadyResponded)> SubmitAsync(
        string username,
        string campaignId,
        int rating,
        string message)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(campaignId))
            return (false, false);

        if (rating < 1 || rating > 5)
            return (false, false);

        message ??= "";
        if (message.Length > 2000)
            message = message[..2000];

        try
        {
            var row = new
            {
                username = username.Trim(),
                campaign_id = campaignId.Trim(),
                rating,
                message,
                created_at = DateTime.UtcNow.ToString("o")
            };

            var content = new StringContent(JsonSerializer.Serialize(row), Encoding.UTF8, "application/json");
            var res = await _client.PostAsync($"{_url}/rest/v1/user_feedback", content);
            if (res.IsSuccessStatusCode)
                return (true, false);

            if (res.StatusCode == HttpStatusCode.Conflict)
                return (false, true);

            var body = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"[UserFeedbackService] Submit failed: {res.StatusCode} | {body}");
            return (false, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserFeedbackService] Submit exception: {ex.Message}");
            return (false, false);
        }
    }

    public async Task<List<UserFeedbackEntry>> GetSubmittedFeedbackAsync(int limit = 500)
    {
        if (!_enabled)
            return new List<UserFeedbackEntry>();

        try
        {
            var milestoneLike = Uri.EscapeDataString($"{FeedbackCampaigns.MilestoneCampaignPrefix}*");
            var legacyCampaign = Uri.EscapeDataString(FeedbackCampaigns.LegacyCampaignId);
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_feedback?select=id,username,campaign_id,rating,message,created_at" +
                $"&rating=gte.1" +
                $"&or=(campaign_id.like.{milestoneLike},campaign_id.eq.{legacyCampaign})" +
                $"&order=created_at.desc&limit={limit}");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[UserFeedbackService] GetSubmittedFeedback failed: {res.StatusCode}");
                return new List<UserFeedbackEntry>();
            }

            var json = await res.Content.ReadAsStringAsync();
            return ParseEntries(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserFeedbackService] GetSubmittedFeedback exception: {ex.Message}");
            return new List<UserFeedbackEntry>();
        }
    }

    private static List<UserFeedbackEntry> ParseEntries(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<UserFeedbackEntry>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            list.Add(new UserFeedbackEntry
            {
                Id = row.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var id) ? id : 0,
                Username = row.TryGetProperty("username", out var userEl) ? userEl.GetString() ?? "" : "",
                CampaignId = row.TryGetProperty("campaign_id", out var campEl) ? campEl.GetString() ?? "" : "",
                Rating = row.TryGetProperty("rating", out var ratingEl) && ratingEl.TryGetInt32(out var rating)
                    ? rating
                    : 0,
                Message = row.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "" : "",
                CreatedAt = row.TryGetProperty("created_at", out var atEl) &&
                            DateTime.TryParse(atEl.GetString(), out var parsed)
                    ? parsed.ToUniversalTime()
                    : DateTime.MinValue
            });
        }

        return list;
    }
}
