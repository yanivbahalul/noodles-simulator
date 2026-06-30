using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
    public static IReadOnlyList<SystemVerificationPlanItem> InternalCheckPlan { get; } = new[]
    {
        new SystemVerificationPlanItem { Id = "config", Name = "הגדרות Supabase", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "users", Name = "משתמשים (Supabase)", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "storage", Name = "אחסון תמונות", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "media", Name = "פרוקסי מדיה (/media)", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "test-sessions", Name = "מבחנים", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "difficulties", Name = "רמות קושי", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "activity", Name = "אירועי פעילות", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "progress", Name = "התקדמות משתמשים", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "question-stats", Name = "סטטיסטיקות שאלות", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "email", Name = "דוא\"ל", Category = "מערכת פנימית" },
        new SystemVerificationPlanItem { Id = "local-dirs", Name = "תיקיות מקומיות", Category = "מערכת פנימית" }
    };

    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly AuthService _auth;
    private readonly SupabaseStorageService _storage;
    private readonly TestSessionService _testSessions;
    private readonly QuestionDifficultyService _difficulty;
    private readonly ActivityEventService _activity;
    private readonly UserProgressStore _progressStore;

    public SystemHealthService(
        IConfiguration config,
        IHostEnvironment env,
        AuthService auth,
        QuestionDifficultyService difficulty,
        SupabaseStorageService storage = null,
        TestSessionService testSessions = null,
        ActivityEventService activity = null,
        UserProgressStore progressStore = null)
    {
        _config = config;
        _env = env;
        _auth = auth;
        _difficulty = difficulty;
        _storage = storage;
        _testSessions = testSessions;
        _activity = activity;
        _progressStore = progressStore;
    }

    public async Task<SystemHealthReport> RunAsync()
    {
        var checks = new List<SystemHealthCheck>();
        await foreach (var check in StreamChecksAsync())
            checks.Add(check);

        return new SystemHealthReport
        {
            CheckedAtUtc = DateTime.UtcNow,
            AllOk = checks.All(c => c.Ok),
            Environment = _env.EnvironmentName,
            Checks = checks
        };
    }

    public async IAsyncEnumerable<SystemHealthCheck> StreamChecksAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return CheckConfig();
        yield return await CheckUsersAsync();
        yield return await CheckStorageAsync();
        yield return await CheckMediaProxyAsync();
        yield return await CheckTestSessionsAsync();
        yield return await CheckDifficultiesAsync();
        yield return await CheckActivityEventsAsync();
        yield return await CheckUserProgressAsync();
        yield return CheckQuestionStats();
        yield return CheckEmailConfig();
        yield return CheckLocalDirs();
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

    private async Task<SystemHealthCheck> CheckMediaProxyAsync()
    {
        if (_storage == null)
            return new SystemHealthCheck { Id = "media", Name = "פרוקסי מדיה (/media)", Ok = false, Detail = "שירות אחסון לא זמין" };

        return await TimedAsync("media", "פרוקסי מדיה (/media)", async () =>
        {
            var files = await _storage.ListFilesAsync("");
            var sample = files.FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(sample))
                return (false, "לא נמצאה תמונה לבדיקה");

            try
            {
                var bytes = await _storage.DownloadBytesAsync(sample);
                var url = MediaUrl.ForStoragePath(sample);
                var ok = bytes != null && bytes.Length > 0 && url.StartsWith("/media/", StringComparison.Ordinal);
                return (ok, ok ? $"{url} ({bytes.Length} bytes)" : "הורדה נכשלה");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
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
            var all = _difficulty?.GetAllQuestionsAsync().GetAwaiter().GetResult()
                ?? new List<QuestionDifficulty>();
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
