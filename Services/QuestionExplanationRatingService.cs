using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

#nullable enable

namespace NoodlesSimulator.Services;

public sealed class QuestionExplanationRatingService
{
    private readonly HttpClient? _client;
    private readonly string _url;

    public bool IsEnabled => _client != null;

    public QuestionExplanationRatingService(IConfiguration config)
    {
        var rest = SupabaseRestClient.Create(config);
        _url = rest.Url;
        _client = rest.Client;
    }

    public async Task<bool> SubmitAsync(string questionFile, string username, int stars, string? feedback)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(questionFile) || string.IsNullOrWhiteSpace(username))
            return false;
        if (stars is < 1 or > 5)
            return false;

        var now = DateTime.UtcNow.ToString("o");
        var text = (feedback ?? "").Trim();
        if (text.Length > 500)
            text = text[..500];

        try
        {
            var payload = new[]
            {
                new
                {
                    QuestionFile = questionFile.Trim(),
                    Username = username.Trim(),
                    Stars = stars,
                    Feedback = text,
                    UpdatedAt = now
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/question_explanation_ratings")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            var response = await _client!.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationRatingService] SubmitAsync: {ex.Message}");
            return false;
        }
    }

    public async Task<List<QuestionExplanationRatingSummary>> ListSummariesAsync(int limit = 100)
    {
        if (!IsEnabled)
            return new List<QuestionExplanationRatingSummary>();

        try
        {
            var res = await _client!.GetAsync(
                $"{_url}/rest/v1/question_explanation_ratings?select=QuestionFile,Stars,Feedback,UpdatedAt&order=UpdatedAt.desc&limit=2000");
            if (!res.IsSuccessStatusCode)
                return new List<QuestionExplanationRatingSummary>();

            var json = await res.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<List<QuestionExplanationRating>>(json, AppJson.Options)
                       ?? new List<QuestionExplanationRating>();

            return rows
                .GroupBy(r => r.QuestionFile, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var list = g.ToList();
                    var avg = list.Average(r => r.Stars);
                    var low = list.Count(r => r.Stars <= 2);
                    var feedback = list
                        .Select(r => (r.Feedback ?? "").Trim())
                        .Where(f => f.Length > 0)
                        .Take(3)
                        .ToArray();
                    var urgency = (5.0 - avg) * list.Count + low * 2.0;
                    return new QuestionExplanationRatingSummary
                    {
                        QuestionFile = g.Key,
                        AvgStars = Math.Round(avg, 2),
                        RatingCount = list.Count,
                        LowCount = low,
                        RecentFeedback = feedback,
                        UrgencyScore = Math.Round(urgency, 2)
                    };
                })
                .OrderByDescending(s => s.UrgencyScore)
                .ThenBy(s => s.AvgStars)
                .ThenByDescending(s => s.RatingCount)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationRatingService] ListSummariesAsync: {ex.Message}");
            return new List<QuestionExplanationRatingSummary>();
        }
    }
}
