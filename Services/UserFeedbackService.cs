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
        _url = SupabaseConfiguration.Url(config) ?? string.Empty;
        var apiKey = SupabaseConfiguration.ServiceRoleApiKey(config)
                     ?? SupabaseConfiguration.AnonApiKey(config);

        _enabled = !string.IsNullOrWhiteSpace(_url) && !string.IsNullOrWhiteSpace(apiKey);
        if (!_enabled)
        {
            Console.WriteLine("[UserFeedbackService] Disabled — missing Supabase URL or API key");
            return;
        }

        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _client.DefaultRequestHeaders.Add("apikey", apiKey);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
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
    }

    public async Task<bool> HasSubmittedAsync(string username, string campaignId)
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
                Console.WriteLine($"[UserFeedbackService] HasSubmitted failed: {res.StatusCode}");
                return false;
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetArrayLength() > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserFeedbackService] HasSubmitted exception: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, bool AlreadySubmitted)> SubmitAsync(
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

    public async Task<List<UserFeedbackEntry>> GetAllForCampaignAsync(string campaignId, int limit = 500)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(campaignId))
            return new List<UserFeedbackEntry>();

        try
        {
            var safeCampaign = Uri.EscapeDataString(campaignId.Trim());
            var res = await _client.GetAsync(
                $"{_url}/rest/v1/user_feedback?select=id,username,campaign_id,rating,message,created_at&campaign_id=eq.{safeCampaign}&order=created_at.desc&limit={limit}");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[UserFeedbackService] GetAllForCampaign failed: {res.StatusCode}");
                return new List<UserFeedbackEntry>();
            }

            var json = await res.Content.ReadAsStringAsync();
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
        catch (Exception ex)
        {
            Console.WriteLine($"[UserFeedbackService] GetAllForCampaign exception: {ex.Message}");
            return new List<UserFeedbackEntry>();
        }
    }
}
