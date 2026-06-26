using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class SystemHealthCheck
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Ok { get; init; }
    public string Detail { get; init; } = "";
    public long ElapsedMs { get; init; }
}

public class SystemHealthReport
{
    public DateTime CheckedAtUtc { get; init; }
    public bool AllOk { get; init; }
    public string Environment { get; init; } = "";
    public IReadOnlyList<SystemHealthCheck> Checks { get; init; } = Array.Empty<SystemHealthCheck>();
}

public class SystemHealthService
{
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly AuthService _auth;
    private readonly SupabaseStorageService _storage;
    private readonly TestSessionService _testSessions;
    private readonly QuestionDifficultyService _difficulty;
    private readonly ActivityEventService _activity;
    private readonly UserProgressStore _progressStore;
    private readonly QuestionStatsService _stats;

    public SystemHealthService(
        IConfiguration config,
        IHostEnvironment env,
        AuthService auth,
        QuestionStatsService stats,
        SupabaseStorageService storage = null,
        TestSessionService testSessions = null,
        QuestionDifficultyService difficulty = null,
        ActivityEventService activity = null,
        UserProgressStore progressStore = null)
    {
        _config = config;
        _env = env;
        _auth = auth;
        _stats = stats;
        _storage = storage;
        _testSessions = testSessions;
        _difficulty = difficulty;
        _activity = activity;
        _progressStore = progressStore;
    }

    public async Task<SystemHealthReport> RunAsync()
    {
        var checks = new List<SystemHealthCheck>();
        checks.Add(CheckConfig());
        checks.Add(await CheckUsersAsync());
        checks.Add(await CheckStorageAsync());
        checks.Add(await CheckSignedUrlsAsync());
        checks.Add(await CheckTestSessionsAsync());
        checks.Add(await CheckDifficultiesAsync());
        checks.Add(await CheckActivityEventsAsync());
        checks.Add(await CheckUserProgressAsync());
        checks.Add(CheckQuestionStats());
        checks.Add(CheckEmailConfig());
        checks.Add(CheckLocalDirs());

        return new SystemHealthReport
        {
            CheckedAtUtc = DateTime.UtcNow,
            AllOk = checks.All(c => c.Ok),
            Environment = _env.EnvironmentName,
            Checks = checks
        };
    }

    private async Task<SystemHealthCheck> TimedAsync(string id, string name, Func<Task<(bool ok, string detail)>> probe)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (ok, detail) = await probe();
            return new SystemHealthCheck { Id = id, Name = name, Ok = ok, Detail = detail, ElapsedMs = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            return new SystemHealthCheck { Id = id, Name = name, Ok = false, Detail = ex.Message, ElapsedMs = sw.ElapsedMilliseconds };
        }
    }

    private SystemHealthCheck CheckConfig()
    {
        var sw = Stopwatch.StartNew();
        var url = SupabaseConfiguration.Url(_config);
        var anon = SupabaseConfiguration.AnonApiKey(_config);
        var service = SupabaseConfiguration.ServiceRoleApiKey(_config);
        var bucket = SupabaseConfiguration.Bucket(_config);

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(url)) missing.Add("SUPABASE_URL");
        if (string.IsNullOrWhiteSpace(anon)) missing.Add("anon key");
        if (string.IsNullOrWhiteSpace(service)) missing.Add("service key");
        if (string.IsNullOrWhiteSpace(bucket)) missing.Add("bucket");

        var ok = missing.Count == 0;
        var detail = ok
            ? $"bucket={bucket}, TTL={SupabaseConfiguration.SignedUrlTtlSeconds(_config)}s"
            : $"חסר: {string.Join(", ", missing)}";

        return new SystemHealthCheck { Id = "config", Name = "הגדרות Supabase", Ok = ok, Detail = detail, ElapsedMs = sw.ElapsedMilliseconds };
    }

    private async Task<SystemHealthCheck> CheckUsersAsync()
    {
        return await TimedAsync("users", "משתמשים (Supabase)", async () =>
        {
            var users = await _auth.GetAllUsersLightAsync();
            var online = users.Count(u => AuthService.UserIsOnline(u));
            return (users.Count > 0, $"{users.Count} משתמשים, {online} מחוברים");
        });
    }

    private async Task<SystemHealthCheck> CheckStorageAsync()
    {
        if (_storage == null)
            return new SystemHealthCheck { Id = "storage", Name = "אחסון תמונות", Ok = false, Detail = "שירות לא רשום (חסר service key)" };

        return await TimedAsync("storage", "אחסון תמונות", async () =>
        {
            var files = await _storage.ListFilesAsync("");
            var images = files.Count(f =>
                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
            var groups = images / 5;
            return (images > 0, $"{images} תמונות (~{groups} שאלות)");
        });
    }

    private async Task<SystemHealthCheck> CheckSignedUrlsAsync()
    {
        if (_storage == null)
            return new SystemHealthCheck { Id = "signed-urls", Name = "קישורים חתומים", Ok = false, Detail = "שירות אחסון לא זמין" };

        return await TimedAsync("signed-urls", "קישורים חתומים", async () =>
        {
            var files = await _storage.ListFilesAsync("");
            var sample = files.FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(sample))
                return (false, "לא נמצאה תמונה לבדיקה");

            var url = await _storage.GetSignedUrlAsync(sample);
            var ok = !string.IsNullOrWhiteSpace(url) && url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            return (ok, ok ? $"נוצר URL ל-{sample}" : "יצירת URL נכשלה");
        });
    }

    private async Task<SystemHealthCheck> CheckTestSessionsAsync()
    {
        if (_testSessions == null)
            return new SystemHealthCheck { Id = "test-sessions", Name = "מבחנים", Ok = false, Detail = "שירות לא רשום" };

        return await TimedAsync("test-sessions", "מבחנים", async () =>
        {
            var active = await _testSessions.GetActiveSessionsAsync(5);
            var recent = await _testSessions.GetRecentCompletedSessionsAsync(1);
            return (true, $"{active.Count} פעילים, {recent.Count}+ הושלמו לאחרונה");
        });
    }

    private async Task<SystemHealthCheck> CheckDifficultiesAsync()
    {
        if (_difficulty == null)
            return new SystemHealthCheck { Id = "difficulties", Name = "רמות קושי", Ok = false, Detail = "שירות לא רשום" };

        return await TimedAsync("difficulties", "רמות קושי", async () =>
        {
            var all = await _difficulty.GetAllQuestionsAsync(5000);
            var easy = all.Count(q => q.Difficulty == "easy");
            var medium = all.Count(q => q.Difficulty == "medium");
            var hard = all.Count(q => q.Difficulty == "hard");
            return (all.Count > 0, $"{all.Count} שאלות (קל={easy}, בינוני={medium}, קשה={hard})");
        });
    }

    private async Task<SystemHealthCheck> CheckActivityEventsAsync()
    {
        if (_activity == null || !_activity.IsEnabled)
            return new SystemHealthCheck { Id = "activity", Name = "אירועי פעילות", Ok = false, Detail = "שירות לא מופעל" };

        return await TimedAsync("activity", "אירועי פעילות", async () =>
        {
            var recent = await _activity.GetRecentAsync(1);
            return (true, recent.Count > 0 ? $"אירוע אחרון: {recent[0].EventType}" : "טבלה נגישה, אין אירועים");
        });
    }

    private async Task<SystemHealthCheck> CheckUserProgressAsync()
    {
        if (_progressStore == null || !_progressStore.IsEnabled)
            return new SystemHealthCheck { Id = "progress", Name = "התקדמות משתמשים", Ok = false, Detail = "שירות לא מופעל" };

        return await TimedAsync("progress", "התקדמות משתמשים", async () =>
        {
            var recent = await _progressStore.FetchRecentProgressUpdatesAsync(1);
            return (true, recent.Count > 0 ? $"עדכון אחרון: {recent[0].Username}" : "טבלה נגישה, אין רשומות");
        });
    }

    private SystemHealthCheck CheckQuestionStats()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var all = _stats.GetAll();
            return new SystemHealthCheck
            {
                Id = "question-stats",
                Name = "סטטיסטיקות שאלות",
                Ok = true,
                Detail = $"{all.Count} שאלות במעקב",
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            return new SystemHealthCheck { Id = "question-stats", Name = "סטטיסטיקות שאלות", Ok = false, Detail = ex.Message, ElapsedMs = sw.ElapsedMilliseconds };
        }
    }

    private SystemHealthCheck CheckEmailConfig()
    {
        var sw = Stopwatch.StartNew();
        var to = EmailConfiguration.EmailTo(_config);
        var user = EmailConfiguration.SmtpUser(_config);
        var pass = EmailConfiguration.SmtpPass(_config);
        var brevo = EmailConfiguration.BrevoApiKey();
        var hasBrevo = !string.IsNullOrWhiteSpace(to) && !string.IsNullOrWhiteSpace(brevo);
        var hasSmtp = !string.IsNullOrWhiteSpace(to) && !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass);
        var ok = hasBrevo || hasSmtp;

        string detail;
        if (!ok)
            detail = "חסרות הגדרות דוא\"ל";
        else if (hasBrevo)
            detail = "Brevo API מוגדר";
        else
            detail = "SMTP מוגדר";

        return new SystemHealthCheck { Id = "email", Name = "דוא\"ל", Ok = ok, Detail = detail, ElapsedMs = sw.ElapsedMilliseconds };
    }

    private SystemHealthCheck CheckLocalDirs()
    {
        var sw = Stopwatch.StartNew();
        var isProd = _env.IsProduction();
        var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        var reportsDir = isProd ? "/data-keys/reports" : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
        var progressDir = isProd ? "/data-keys/progress" : Path.Combine(Directory.GetCurrentDirectory(), "progress");
        var keysDir = isProd ? "/data-keys/dataprotection" : Path.Combine(Directory.GetCurrentDirectory(), "data-keys", "dataprotection");

        var parts = new List<string>();
        var ok = true;
        foreach (var (label, path) in new[] { ("reports", reportsDir), ("progress", progressDir), ("keys", keysDir) })
        {
            var exists = Directory.Exists(path);
            if (!exists) ok = false;
            parts.Add($"{label}={(exists ? "✓" : "✗")}");
        }

        var localImages = Directory.Exists(imagesDir) ? Directory.GetFiles(imagesDir).Length : 0;
        parts.Add($"local-images={localImages}");

        return new SystemHealthCheck { Id = "local-dirs", Name = "תיקיות מקומיות", Ok = ok, Detail = string.Join(", ", parts), ElapsedMs = sw.ElapsedMilliseconds };
    }
}
