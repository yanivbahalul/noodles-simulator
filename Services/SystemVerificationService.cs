using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public enum SystemVerificationStatus
{
    Pending,
    Running,
    Ok,
    Fail,
    Warn
}

public class SystemVerificationPlanItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
}

public class SystemVerificationEvent
{
    public string Phase { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public SystemVerificationStatus Status { get; init; }
    public string Detail { get; init; } = "";
    public long ElapsedMs { get; init; }
    public bool? AllOk { get; init; }
    public int? Passed { get; init; }
    public int? Failed { get; init; }
    public int? Warnings { get; init; }
    public IReadOnlyList<SystemVerificationPlanItem> Plan { get; init; }
    public string Environment { get; init; } = "";
}

public class SystemVerificationService
{
    private static readonly Regex CsrfTokenRegex = new(
        @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SystemHealthService _health;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public SystemVerificationService(
        SystemHealthService health,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IHostEnvironment env)
    {
        _health = health;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _env = env;
    }

    public IReadOnlyList<SystemVerificationPlanItem> GetPlan()
    {
        var plan = new List<SystemVerificationPlanItem>();
        plan.AddRange(new[]
        {
            new SystemVerificationPlanItem { Id = "config", Name = "הגדרות Supabase", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "users", Name = "משתמשים (Supabase)", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "storage", Name = "אחסון תמונות", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "signed-urls", Name = "קישורים חתומים", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "test-sessions", Name = "מבחנים", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "difficulties", Name = "רמות קושי", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "activity", Name = "אירועי פעילות", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "progress", Name = "התקדמות משתמשים", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "question-stats", Name = "סטטיסטיקות שאלות", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "email", Name = "דוא\"ל", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "local-dirs", Name = "תיקיות מקומיות", Category = "מערכת פנימית" },
            new SystemVerificationPlanItem { Id = "http-health", Name = "Health API", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "http-online-count", Name = "מחוברים כעת", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "http-leaderboard", Name = "לוח תוצאות", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "http-question-difficulty", Name = "קושי שאלות", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "http-login-page", Name = "דף התחברות", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "sb-user-stats", Name = "טבלת user_stats", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-user-question-stats", Name = "טבלת user_question_stats", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-user-progress", Name = "טבלת user_progress", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-user-achievements", Name = "טבלת user_achievements", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-test-sessions", Name = "טבלת test_sessions", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "flow-login", Name = "התחברות בדיקה", Category = "זרימת משתמש" },
            new SystemVerificationPlanItem { Id = "flow-stats-data", Name = "נתוני סטטיסטיקה", Category = "זרימת משתמש" },
            new SystemVerificationPlanItem { Id = "flow-next-question", Name = "שאלה הבאה", Category = "זרימת משתמש" },
        });
        return plan;
    }

    public async IAsyncEnumerable<SystemVerificationEvent> RunAsync(
        string baseUrl,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var plan = GetPlan();
        var passed = 0;
        var failed = 0;
        var warnings = 0;

        yield return new SystemVerificationEvent
        {
            Phase = "start",
            Plan = plan,
            Environment = _env.EnvironmentName
        };

        await foreach (var health in _health.StreamChecksAsync(cancellationToken))
        {
            foreach (var evt in EmitRunningThenResult(health.Id, health.Name, "מערכת פנימית", health.Ok, health.Detail, health.ElapsedMs, false))
            {
                yield return evt;
                if (evt.Phase == "check")
                {
                    if (health.Ok) passed++;
                    else failed++;
                }
            }
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(45);
        baseUrl = baseUrl.TrimEnd('/');

        foreach (var probe in GetPublicHttpProbes(baseUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Running(probe.Id, probe.Name, probe.Category);

            var (ok, detail, elapsed, warn) = await ProbePublicHttpAsync(httpClient, probe, cancellationToken);
            yield return Result(probe.Id, probe.Name, probe.Category, ok, detail, elapsed, warn);
            if (ok) passed++;
            else if (warn) warnings++;
            else failed++;
        }

        foreach (var table in GetSupabaseTables())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Running(table.Id, table.Name, "Supabase");

            var sw = Stopwatch.StartNew();
            var (ok, detail, warn) = await ProbeSupabaseTableAsync(table.TableName, cancellationToken);
            yield return Result(table.Id, table.Name, "Supabase", ok, detail, sw.ElapsedMilliseconds, warn);
            if (ok) passed++;
            else if (warn) warnings++;
            else failed++;
        }

        CookieContainer cookies = null;
        foreach (var flow in GetUserFlowProbes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Running(flow.Id, flow.Name, "זרימת משתמש");

            var sw = Stopwatch.StartNew();
            var flowResult = await SafeUserFlowStepAsync(flow.Id, baseUrl, cookies, cancellationToken);
            cookies = flowResult.Cookies ?? cookies;

            yield return Result(flow.Id, flow.Name, "זרימת משתמש", flowResult.Ok, flowResult.Detail, sw.ElapsedMilliseconds, flowResult.Warn);
            if (flowResult.Ok) passed++;
            else if (flowResult.Warn) warnings++;
            else failed++;
        }

        yield return new SystemVerificationEvent
        {
            Phase = "complete",
            AllOk = failed == 0,
            Passed = passed,
            Failed = failed,
            Warnings = warnings
        };
    }

    private static IEnumerable<SystemVerificationEvent> EmitRunningThenResult(
        string id, string name, string category, bool ok, string detail, long elapsedMs, bool warn)
    {
        yield return Running(id, name, category);
        yield return Result(id, name, category, ok, detail, elapsedMs, warn);
    }

    private static SystemVerificationEvent Running(string id, string name, string category) => new()
    {
        Phase = "running",
        Id = id,
        Name = name,
        Category = category,
        Status = SystemVerificationStatus.Running
    };

    private static SystemVerificationEvent Result(
        string id, string name, string category, bool ok, string detail, long elapsedMs, bool warn) => new()
    {
        Phase = "check",
        Id = id,
        Name = name,
        Category = category,
        Status = ok ? SystemVerificationStatus.Ok : (warn ? SystemVerificationStatus.Warn : SystemVerificationStatus.Fail),
        Detail = detail,
        ElapsedMs = elapsedMs
    };

    private static IEnumerable<(string Id, string Name, string Category, string Url, int ExpectedStatus)> GetPublicHttpProbes(string baseUrl)
    {
        yield return ("http-health", "Health API", "APIs ציבוריים", $"{baseUrl}/health", 200);
        yield return ("http-online-count", "מחוברים כעת", "APIs ציבוריים", $"{baseUrl}/api/online-count", 200);
        yield return ("http-leaderboard", "לוח תוצאות", "APIs ציבוריים", $"{baseUrl}/api/leaderboard-data?tab=total", 200);
        yield return ("http-question-difficulty", "קושי שאלות", "APIs ציבוריים", $"{baseUrl}/api/question-difficulty", 200);
        yield return ("http-login-page", "דף התחברות", "APIs ציבוריים", $"{baseUrl}/Login", 200);
    }

    private static IEnumerable<(string Id, string Name, string TableName)> GetSupabaseTables()
    {
        yield return ("sb-user-stats", "טבלת user_stats", "user_stats");
        yield return ("sb-user-question-stats", "טבלת user_question_stats", "user_question_stats");
        yield return ("sb-user-progress", "טבלת user_progress", "user_progress");
        yield return ("sb-user-achievements", "טבלת user_achievements", "user_achievements");
        yield return ("sb-test-sessions", "טבלת test_sessions", "test_sessions");
    }

    private static IEnumerable<(string Id, string Name)> GetUserFlowProbes()
    {
        yield return ("flow-login", "התחברות בדיקה");
        yield return ("flow-stats-data", "נתוני סטטיסטיקה");
        yield return ("flow-next-question", "שאלה הבאה");
    }

    private async Task<(bool ok, string detail, long elapsed, bool warn)> ProbePublicHttpAsync(
        HttpClient httpClient,
        (string Id, string Name, string Category, string Url, int ExpectedStatus) probe,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await httpClient.GetAsync(probe.Url, cancellationToken);
            var elapsed = sw.ElapsedMilliseconds;
            var ok = (int)response.StatusCode == probe.ExpectedStatus;
            var detail = ok
                ? $"HTTP {(int)response.StatusCode} ({elapsed} ms)"
                : $"HTTP {(int)response.StatusCode}, צפוי {probe.ExpectedStatus}";

            if (probe.Id == "http-health" && ok)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                detail = SummarizeHealthJson(body, elapsed);
                ok = body.Contains("\"ok\":true", StringComparison.OrdinalIgnoreCase);
            }

            if (probe.Id == "http-login-page" && ok)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                ok = CsrfTokenRegex.IsMatch(body);
                detail = ok ? $"HTTP 200, CSRF קיים ({elapsed} ms)" : "חסר CSRF token";
            }

            return (ok, detail, elapsed, false);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, sw.ElapsedMilliseconds, false);
        }
    }

    private async Task<(bool Ok, string Detail, bool Warn, CookieContainer Cookies)> SafeUserFlowStepAsync(
        string stepId,
        string baseUrl,
        CookieContainer cookies,
        CancellationToken cancellationToken)
    {
        try
        {
            var (ok, detail, warn, updatedCookies) = await RunUserFlowStepAsync(
                stepId, baseUrl, cookies, cancellationToken);
            return (ok, detail, warn, updatedCookies);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, false, cookies);
        }
    }

    private async Task<(bool ok, string detail, bool warn)> ProbeSupabaseTableAsync(
        string tableName, CancellationToken cancellationToken)
    {
        var url = SupabaseConfiguration.Url(_config);
        var key = SupabaseConfiguration.ServiceRoleApiKey(_config);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            return (false, "חסר SUPABASE_URL או service key", false);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/rest/v1/{tableName}?select=*&limit=1");
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {key}");
        request.Headers.TryAddWithoutValidation("Prefer", "count=exact");

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.PartialContent)
        {
            var count = "?";
            if (response.Headers.TryGetValues("Content-Range", out var values))
            {
                var range = values.FirstOrDefault();
                if (!string.IsNullOrEmpty(range) && range.Contains('/'))
                    count = range.Split('/').Last();
            }
            return (true, $"נגיש, שורות={count}", false);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, $"HTTP {(int)response.StatusCode}: {Trim(body, 120)}", false);
    }

    private async Task<(bool ok, string detail, bool warn, CookieContainer cookies)> RunUserFlowStepAsync(
        string stepId,
        string baseUrl,
        CookieContainer cookies,
        CancellationToken cancellationToken)
    {
        cookies ??= new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };

        var username = Environment.GetEnvironmentVariable("SYSTEM_CHECK_USER")
            ?? _config["SystemCheck:Username"]
            ?? "e2etestuser99";
        var password = Environment.GetEnvironmentVariable("SYSTEM_CHECK_PASS")
            ?? _config["SystemCheck:Password"]
            ?? "testpass99";

        if (stepId == "flow-login")
        {
            var loginHtml = await client.GetStringAsync($"{baseUrl}/Login", cancellationToken);
            var token = ExtractCsrf(loginHtml);
            if (string.IsNullOrEmpty(token))
                return (false, "לא נמצא CSRF בדף Login", false, cookies);

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Username"] = username,
                ["Password"] = password,
                ["action"] = "login"
            });
            using var loginRes = await client.PostAsync($"{baseUrl}/Login", form, cancellationToken);
            var hasSession = cookies.GetCookies(new Uri(baseUrl)).Cast<Cookie>()
                .Any(c => c.Name.StartsWith(".Noodles.Session", StringComparison.Ordinal));
            var ok = hasSession || loginRes.StatusCode == HttpStatusCode.Redirect;
            if (!ok)
            {
                var tryRegister = await TryRegisterAsync(client, baseUrl, username, password, cancellationToken);
                ok = tryRegister.ok;
                if (!ok)
                    return (false, $"Login נכשל עבור {username}", true, cookies);
            }
            return (true, $"מחובר כ-{username}", false, cookies);
        }

        if (cookies.GetCookies(new Uri(baseUrl)).Count == 0)
            return (false, "אין session — דלג אחרי כשל login", true, cookies);

        if (stepId == "flow-stats-data")
        {
            using var res = await client.GetAsync($"{baseUrl}/api/stats-data", cancellationToken);
            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return (false, "HTTP 401", true, cookies);
            if (!res.IsSuccessStatusCode)
                return (false, $"HTTP {(int)res.StatusCode}", false, cookies);

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var xp = doc.RootElement.TryGetProperty("xp", out var xpEl) ? xpEl.GetInt32() : 0;
            var level = doc.RootElement.TryGetProperty("level", out var lvlEl) ? lvlEl.GetInt32() : 0;
            return (true, $"xp={xp}, level={level}", false, cookies);
        }

        if (stepId == "flow-next-question")
        {
            using var res = await client.GetAsync($"{baseUrl}/Index?handler=NextQuestion", cancellationToken);
            if (!res.IsSuccessStatusCode)
                return (false, $"HTTP {(int)res.StatusCode}", false, cookies);

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errEl))
                return (false, errEl.GetString() ?? "שגיאה", false, cookies);

            var hasQuestion = doc.RootElement.TryGetProperty("questionImage", out _)
                || doc.RootElement.TryGetProperty("questionImageOriginalName", out _);
            return hasQuestion
                ? (true, "התקבלה שאלה", false, cookies)
                : (false, "חסרה תמונת שאלה", false, cookies);
        }

        return (false, "שלב לא מוכר", false, cookies);
    }

    private static async Task<(bool ok, string detail)> TryRegisterAsync(
        HttpClient client, string baseUrl, string username, string password, CancellationToken cancellationToken)
    {
        var loginHtml = await client.GetStringAsync($"{baseUrl}/Login", cancellationToken);
        var token = ExtractCsrf(loginHtml);
        if (string.IsNullOrEmpty(token))
            return (false, "no csrf");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Username"] = username,
            ["Password"] = password,
            ["action"] = "register"
        });
        using var regRes = await client.PostAsync($"{baseUrl}/Login", form, cancellationToken);
        if (!regRes.IsSuccessStatusCode && regRes.StatusCode != HttpStatusCode.Redirect)
            return (false, "register failed");

        loginHtml = await client.GetStringAsync($"{baseUrl}/Login", cancellationToken);
        token = ExtractCsrf(loginHtml);
        form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Username"] = username,
            ["Password"] = password,
            ["action"] = "login"
        });
        using var loginRes = await client.PostAsync($"{baseUrl}/Login", form, cancellationToken);
        return (loginRes.IsSuccessStatusCode || loginRes.StatusCode == HttpStatusCode.Redirect, "registered");
    }

    private static string ExtractCsrf(string html)
    {
        var m = CsrfTokenRegex.Match(html);
        return m.Success ? m.Groups[1].Value : "";
    }

    private static string SummarizeHealthJson(string body, long elapsedMs)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var parts = new List<string> { $"{elapsedMs} ms" };
            foreach (var key in new[] { "supabaseUrl", "supabaseAnon", "supabaseService", "supabaseBucket" })
            {
                if (root.TryGetProperty(key, out var val))
                    parts.Add($"{key}={val.GetString()}");
            }
            return string.Join(", ", parts);
        }
        catch
        {
            return $"HTTP 200 ({elapsedMs} ms)";
        }
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
