using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class QuestionReportService
{
    private readonly string _path;
    private readonly object _lock = new();
    private List<QuestionReportEntry> _cache = new();
    private DateTime _lastLoad = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    public const string StatusOpen = "open";
    public const string StatusResolved = "resolved";

    public class QuestionReportEntry
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string QuestionId { get; set; } = "";
        public string Explanation { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public string SelectedAnswer { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
        public string Status { get; set; } = StatusOpen;
        public DateTime? ResolvedAtUtc { get; set; }
    }

    public QuestionReportService(string path)
    {
        _path = path;
        EnsureFile();
    }

    private void EnsureFile()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(_path))
            PersistLocked(new List<QuestionReportEntry>());
    }

    public QuestionReportEntry Add(ErrorReportBuilder.ReportPayload payload)
    {
        if (payload == null) return null;

        var entry = new QuestionReportEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = payload.Username ?? "Unknown",
            QuestionId = payload.QuestionImage ?? "",
            Explanation = payload.Explanation ?? "",
            CorrectAnswer = payload.CorrectAnswer ?? "",
            SelectedAnswer = payload.SelectedAnswer ?? "",
            CreatedAtUtc = payload.TimestampUtc == default ? DateTime.UtcNow : payload.TimestampUtc.ToUniversalTime(),
            Status = StatusOpen
        };

        lock (_lock)
        {
            LoadIfStaleLocked(force: true);
            _cache.Insert(0, entry);
            PersistLocked(_cache);
        }

        return entry;
    }

    public async Task SubmitAsync(
        ErrorReportBuilder.ReportPayload payload,
        string baseUrl,
        EmailService? emailService = null,
        ActivityEventService? activityEvents = null)
    {
        if (payload == null) return;

        var htmlBody = ErrorReportBuilder.BuildHtmlBody(payload, baseUrl);
        var reportSubject = ErrorReportBuilder.BuildSubject(payload.Username);
        await TrySendEmailAsync(emailService, reportSubject, htmlBody);

        Add(payload);

        activityEvents?.Log(payload.Username, ActivityEventCatalog.QuestionReport, new Dictionary<string, object>
        {
            ["questionId"] = payload.QuestionImage ?? "",
            ["explanation"] = payload.Explanation ?? ""
        });
    }

    private static Task TrySendEmailAsync(EmailService? emailService, string subject, string htmlBody)
    {
        try
        {
            if (emailService == null || !emailService.IsConfigured)
            {
                Console.WriteLine("[Report] Email service not configured, skipping email notification");
                return Task.CompletedTask;
            }

            Console.WriteLine("[Report] Sending error report email...");
            var result = emailService.Send(subject, htmlBody);
            if (result)
                Console.WriteLine("[Report] Error report email sent successfully");
            else
                Console.WriteLine("[Report] Failed to send error report email");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReportEmail Dispatch Error] {ex}");
        }

        return Task.CompletedTask;
    }

    public List<QuestionReportEntry> GetAll(int limit = 200)
    {
        lock (_lock)
        {
            LoadIfStaleLocked(force: false);
            return _cache.Take(limit).ToList();
        }
    }

    public int OpenCount
    {
        get
        {
            lock (_lock)
            {
                LoadIfStaleLocked(force: false);
                return _cache.Count(r => r.Status == StatusOpen);
            }
        }
    }

    public Dictionary<string, int> GetOpenCountsByQuestion()
    {
        lock (_lock)
        {
            LoadIfStaleLocked(force: false);
            return _cache
                .Where(r => r.Status == StatusOpen && !string.IsNullOrWhiteSpace(r.QuestionId))
                .GroupBy(r => r.QuestionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool MarkResolved(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        lock (_lock)
        {
            LoadIfStaleLocked(force: true);
            var entry = _cache.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
            if (entry == null || entry.Status == StatusResolved) return false;

            entry.Status = StatusResolved;
            entry.ResolvedAtUtc = DateTime.UtcNow;
            PersistLocked(_cache);
            return true;
        }
    }

    public bool Reopen(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        lock (_lock)
        {
            LoadIfStaleLocked(force: true);
            var entry = _cache.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
            if (entry == null || entry.Status == StatusOpen) return false;

            entry.Status = StatusOpen;
            entry.ResolvedAtUtc = null;
            PersistLocked(_cache);
            return true;
        }
    }

    private void LoadIfStaleLocked(bool force)
    {
        if (!force && DateTime.UtcNow - _lastLoad < CacheTtl && _cache.Count > 0)
            return;

        try
        {
            if (!File.Exists(_path))
            {
                _cache = new List<QuestionReportEntry>();
                _lastLoad = DateTime.UtcNow;
                return;
            }

            var json = File.ReadAllText(_path, Encoding.UTF8);
            _cache = string.IsNullOrWhiteSpace(json)
                ? new List<QuestionReportEntry>()
                : JsonSerializer.Deserialize<List<QuestionReportEntry>>(json, AppJson.Options)
                  ?? new List<QuestionReportEntry>();
        }
        catch
        {
            _cache = new List<QuestionReportEntry>();
        }

        _lastLoad = DateTime.UtcNow;
    }

    private void PersistLocked(List<QuestionReportEntry> entries)
    {
        try
        {
            var json = JsonSerializer.Serialize(entries, AppJson.Options);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Copy(tmp, _path, overwrite: true);
            File.Delete(tmp);
            _cache = entries;
            _lastLoad = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestionReportService] Persist failed: {ex.Message}");
        }
    }
}
