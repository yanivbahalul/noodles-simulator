using System;
using System.Collections.Concurrent;
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

public sealed class QuestionExplanationService
{
    private readonly HttpClient? _client;
    private readonly string _url;
    private readonly SupabaseStorageService? _storage;
    private readonly ConcurrentDictionary<string, (string url, DateTime cachedAt)> _urlCache = new();
    private static readonly TimeSpan UrlCacheTtl = TimeSpan.FromMinutes(4);
    private HashSet<string>? _readyQuestionFiles;
    private DateTime _readyListCachedAt = DateTime.MinValue;
    private static readonly TimeSpan ReadyListTtl = TimeSpan.FromMinutes(2);

    public bool IsEnabled => _client != null;

    public QuestionExplanationService(IConfiguration config, SupabaseStorageService? storage = null)
    {
        var rest = SupabaseRestClient.Create(config);
        _url = rest.Url;
        _client = rest.Client;
        _storage = storage;
    }

    public static string VideoObjectPath(string questionFile) =>
        $"explanations/{SanitizeFileName(questionFile)}.mp4";

    public async Task<QuestionExplanation?> GetAsync(string questionFile)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(questionFile))
            return null;

        try
        {
            var res = await _client!.GetAsync(
                $"{_url}/rest/v1/question_explanations?QuestionFile=eq.{Uri.EscapeDataString(questionFile)}&select=*");
            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<QuestionExplanation>>(json, AppJson.Options);
            return items?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationService] GetAsync: {ex.Message}");
            return null;
        }
    }

    public async Task<(bool hasExplanation, string? videoUrl)> GetVideoUrlAsync(string questionFile)
    {
        if (!await HasReadyExplanationAsync(questionFile))
            return (false, null);

        if (_urlCache.TryGetValue(questionFile, out var cached) && DateTime.UtcNow - cached.cachedAt < UrlCacheTtl)
            return (true, cached.url);

        var row = await GetAsync(questionFile);
        if (row == null || _storage == null)
            return (false, null);

        try
        {
            var url = await _storage.GetSignedUrlAsync(row.VideoPath);
            _urlCache[questionFile] = (url, DateTime.UtcNow);
            return (true, url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationService] GetVideoUrlAsync: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<bool> HasReadyExplanationAsync(string questionFile)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(questionFile))
            return false;

        await EnsureReadyFilesAsync();
        return _readyQuestionFiles?.Contains(questionFile.Trim()) == true;
    }

    public bool TryHasReadyExplanation(string questionFile)
    {
        if (string.IsNullOrWhiteSpace(questionFile) || _readyQuestionFiles == null)
            return false;
        return _readyQuestionFiles.Contains(questionFile.Trim());
    }

    public Task WarmReadyFilesAsync() => EnsureReadyFilesAsync();

    private async Task EnsureReadyFilesAsync()
    {
        if (_readyQuestionFiles != null && DateTime.UtcNow - _readyListCachedAt < ReadyListTtl)
            return;

        try
        {
            var res = await _client!.GetAsync(
                $"{_url}/rest/v1/question_explanations?Status=eq.ready&select=QuestionFile");
            if (!res.IsSuccessStatusCode)
                return;

            var json = await res.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<QuestionExplanation>>(json, AppJson.Options) ?? new();
            _readyQuestionFiles = new HashSet<string>(
                items.Select(i => i.QuestionFile).Where(f => !string.IsNullOrWhiteSpace(f)),
                StringComparer.OrdinalIgnoreCase);
            _readyListCachedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationService] EnsureReadyFilesAsync: {ex.Message}");
        }
    }

    public async Task<ExplanationStatusSummary> GetStatusSummaryAsync()
    {
        if (!IsEnabled)
            return new ExplanationStatusSummary();

        try
        {
            var res = await _client!.GetAsync(
                $"{_url}/rest/v1/question_explanations?select=Status");
            if (!res.IsSuccessStatusCode)
                return new ExplanationStatusSummary();

            var json = await res.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<List<QuestionExplanation>>(json, AppJson.Options)
                       ?? new List<QuestionExplanation>();

            return new ExplanationStatusSummary
            {
                Ready = rows.Count(r => string.Equals(r.Status, QuestionExplanationStatus.Ready, StringComparison.OrdinalIgnoreCase)),
                Pending = rows.Count(r => string.Equals(r.Status, QuestionExplanationStatus.Pending, StringComparison.OrdinalIgnoreCase)),
                Failed = rows.Count(r => string.Equals(r.Status, QuestionExplanationStatus.Failed, StringComparison.OrdinalIgnoreCase)),
                NeedsReview = rows.Count(r => string.Equals(r.Status, QuestionExplanationStatus.NeedsReview, StringComparison.OrdinalIgnoreCase)),
                Total = rows.Count
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationService] GetStatusSummaryAsync: {ex.Message}");
            return new ExplanationStatusSummary();
        }
    }

    public async Task<List<QuestionExplanation>> ListAsync(string? status = null, int limit = 200)
    {
        if (!IsEnabled)
            return new List<QuestionExplanation>();

        try
        {
            var filter = string.IsNullOrWhiteSpace(status)
                ? ""
                : $"&Status=eq.{Uri.EscapeDataString(status)}";
            var res = await _client!.GetAsync(
                $"{_url}/rest/v1/question_explanations?select=*&order=GeneratedAt.desc.nullslast&limit={limit}{filter}");
            if (!res.IsSuccessStatusCode)
                return new List<QuestionExplanation>();

            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<QuestionExplanation>>(json, AppJson.Options)
                   ?? new List<QuestionExplanation>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationService] ListAsync: {ex.Message}");
            return new List<QuestionExplanation>();
        }
    }

    public async Task<bool> UpsertAsync(QuestionExplanation record)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(record.QuestionFile))
            return false;

        _urlCache.TryRemove(record.QuestionFile, out _);

        try
        {
            var payload = new[]
            {
                new
                {
                    QuestionFile = record.QuestionFile,
                    VideoPath = record.VideoPath ?? "",
                    ScriptJson = record.ScriptJson ?? "",
                    Status = record.Status ?? QuestionExplanationStatus.Pending,
                    ErrorMessage = record.ErrorMessage ?? "",
                    GeneratedAt = record.GeneratedAt?.ToUniversalTime().ToString("o")
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/question_explanations")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            var response = await _client!.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionExplanationService] UpsertAsync: {ex.Message}");
            return false;
        }
    }

    private static string SanitizeFileName(string questionFile)
    {
        var name = questionFile.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    public sealed class ExplanationStatusSummary
    {
        public int Ready { get; init; }
        public int Pending { get; init; }
        public int Failed { get; init; }
        public int NeedsReview { get; init; }
        public int Total { get; init; }
    }
}
