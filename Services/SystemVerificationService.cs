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
    private readonly IHostEnvironment _env;
    private readonly SupabaseRestClient.Context _supabase;

    public SystemVerificationService(
        SystemHealthService health,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IHostEnvironment env)
    {
        _health = health;
        _httpClientFactory = httpClientFactory;
        _env = env;
        _supabase = SupabaseRestClient.Create(config);
    }

    public IReadOnlyList<SystemVerificationPlanItem> GetPlan()
    {
        var plan = new List<SystemVerificationPlanItem>();
        plan.AddRange(SystemHealthService.InternalCheckPlan);
        plan.AddRange(new[]
        {
            new SystemVerificationPlanItem { Id = "http-online-count", Name = "מחוברים כעת", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "http-leaderboard", Name = "לוח תוצאות", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "http-question-difficulty", Name = "קושי שאלות", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "http-login-page", Name = "דף התחברות", Category = "APIs ציבוריים" },
            new SystemVerificationPlanItem { Id = "sb-user-stats", Name = "טבלת user_stats", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-user-question-stats", Name = "טבלת user_question_stats", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-user-progress", Name = "טבלת user_progress", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-user-achievements", Name = "טבלת user_achievements", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "sb-test-sessions", Name = "טבלת test_sessions", Category = "Supabase" },
            new SystemVerificationPlanItem { Id = "flow-login", Name = "Session פעיל", Category = "זרימת משתמש" },
            new SystemVerificationPlanItem { Id = "flow-stats-data", Name = "נתוני סטטיסטיקה", Category = "זרימת משתמש" },
            new SystemVerificationPlanItem { Id = "flow-next-question", Name = "שאלה הבאה", Category = "זרימת משתמש" },
        });
        return plan;
    }

    public async IAsyncEnumerable<SystemVerificationEvent> RunAsync(
        string baseUrl,
        string forwardedCookieHeader = null,
        string sessionUsername = null,
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

        var flowContext = new FlowTestContext(forwardedCookieHeader, sessionUsername);
        foreach (var flow in GetUserFlowProbes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Running(flow.Id, flow.Name, "זרימת משתמש");

            var sw = Stopwatch.StartNew();
            var flowResult = await SafeUserFlowStepAsync(flow.Id, baseUrl, flowContext, cancellationToken);

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

    private static SystemVerificationStatus ResolveCheckStatus(bool ok, bool warn) =>
        ok ? SystemVerificationStatus.Ok : warn ? SystemVerificationStatus.Warn : SystemVerificationStatus.Fail;

    private static SystemVerificationEvent Result(
        string id, string name, string category, bool ok, string detail, long elapsedMs, bool warn) => new()
    {
        Phase = "check",
        Id = id,
        Name = name,
        Category = category,
        Status = ResolveCheckStatus(ok, warn),
        Detail = detail,
        ElapsedMs = elapsedMs
    };

    private static IEnumerable<(string Id, string Name, string Category, string Url, int ExpectedStatus)> GetPublicHttpProbes(string baseUrl)
    {
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

    private sealed class FlowTestContext
    {
        public FlowTestContext(string cookieHeader, string username)
        {
            CookieHeader = cookieHeader ?? "";
            Username = username ?? "";
            SessionVerified = false;
        }

        public string CookieHeader { get; }
        public string Username { get; }
        public bool SessionVerified { get; set; }
    }

    private static HttpClient CreateFlowClient(string baseUrl, FlowTestContext ctx)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(45),
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };
        if (!string.IsNullOrWhiteSpace(ctx.CookieHeader))
            client.DefaultRequestHeaders.Add("Cookie", ctx.CookieHeader);
        return client;
    }

    private async Task<(bool Ok, string Detail, bool Warn)> SafeUserFlowStepAsync(
        string stepId,
        string baseUrl,
        FlowTestContext ctx,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RunUserFlowStepAsync(stepId, baseUrl, ctx, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, false);
        }
    }

    private async Task<(bool ok, string detail, bool warn)> ProbeSupabaseTableAsync(
        string tableName, CancellationToken cancellationToken)
    {
        try
        {
            if (!_supabase.Enabled || _supabase.Client == null)
                return (false, "חסר SUPABASE_URL או service key", false);

            var url = $"{_supabase.Url.TrimEnd('/')}/rest/v1/{tableName}?select=*&limit=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Prefer", "count=exact");

            using var response = await _supabase.Client.SendAsync(request, cancellationToken);
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
        catch (Exception ex)
        {
            return (false, ex.Message, false);
        }
    }

    private async Task<(bool ok, string detail, bool warn)> RunUserFlowStepAsync(
        string stepId,
        string baseUrl,
        FlowTestContext ctx,
        CancellationToken cancellationToken)
    {
        baseUrl = baseUrl.TrimEnd('/');

        if (stepId == "flow-login")
        {
            if (string.IsNullOrWhiteSpace(ctx.CookieHeader))
                return (false, "אין session — התחבר מחדש", true);

            using var client = CreateFlowClient(baseUrl, ctx);
            using var res = await client.GetAsync("api/stats-data", cancellationToken);
            if (!res.IsSuccessStatusCode)
                return (false, $"session לא תקף (HTTP {(int)res.StatusCode})", true);

            ctx.SessionVerified = true;
            var user = ctx.Username;
            if (string.IsNullOrWhiteSpace(user))
            {
                var json = await res.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("username", out var userEl))
                    user = userEl.GetString() ?? "";
            }

            return (true, string.IsNullOrWhiteSpace(user) ? "session פעיל" : $"session פעיל ({user})", false);
        }

        if (!ctx.SessionVerified)
            return (false, "אין session — דלג אחרי כשל login", true);

        using var flowClient = CreateFlowClient(baseUrl, ctx);

        if (stepId == "flow-stats-data")
        {
            using var res = await flowClient.GetAsync("api/stats-data", cancellationToken);
            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return (false, "HTTP 401", true);
            if (!res.IsSuccessStatusCode)
                return (false, $"HTTP {(int)res.StatusCode}", false);

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var xp = doc.RootElement.TryGetProperty("xp", out var xpEl) ? xpEl.GetInt32() : 0;
            var level = doc.RootElement.TryGetProperty("level", out var lvlEl) ? lvlEl.GetInt32() : 0;
            return (true, $"xp={xp}, level={level}", false);
        }

        if (stepId == "flow-next-question")
        {
            using var res = await flowClient.GetAsync("Index?handler=NextQuestion", cancellationToken);
            if (!res.IsSuccessStatusCode)
                return (false, $"HTTP {(int)res.StatusCode}", false);

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errEl))
                return (false, errEl.GetString() ?? "שגיאה", false);

            var hasQuestion = doc.RootElement.TryGetProperty("questionImage", out _)
                || doc.RootElement.TryGetProperty("questionImageOriginalName", out _);
            return hasQuestion
                ? (true, "התקבלה שאלה", false)
                : (false, "חסרה תמונת שאלה", false);
        }

        return (false, "שלב לא מוכר", false);
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
