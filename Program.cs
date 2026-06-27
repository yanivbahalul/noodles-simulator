
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoodlesSimulator.Models;
using NoodlesSimulator.Middleware;
using NoodlesSimulator.Services;

static void LoadDotEnv(string path = ".env")
{
    if (!File.Exists(path)) return;
    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        var eq = line.IndexOf('=');
        if (eq <= 0) continue;
        var key = line[..eq].Trim();
        var val = line[(eq + 1)..].Trim();
        if (val.Length >= 2 && val.StartsWith('"') && val.EndsWith('"'))
            val = val[1..^1];
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, val);
    }
}

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<UserStatsService>();
builder.Services.AddSingleton<UserQuestionStatsStore>();
builder.Services.AddSingleton<AuthService>(sp =>
    new AuthService(sp.GetRequiredService<IConfiguration>(), sp.GetService<UserStatsService>()));
builder.Services.AddSingleton<QuestionDifficultyService>();

builder.Services.AddSingleton<EmailService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new EmailService(configuration);
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var isProd = builder.Environment.IsProduction();

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Noodles.Session.v3";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isProd ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.MaxAge = RememberMeService.Duration;
});

var dataProtectionPath = isProd
    ? "/data-keys/dataprotection"
    : Path.Combine(Directory.GetCurrentDirectory(), "data-keys", "dataprotection");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("NoodlesSimulator");

builder.Services.AddHttpClient();
var disableRateLimit = !isProd
    && string.Equals(Environment.GetEnvironmentVariable("LOAD_TEST_DISABLE_RATE_LIMIT"), "true", StringComparison.OrdinalIgnoreCase);
if (disableRateLimit)
{
    Console.WriteLine("[LoadTest] Rate limiter disabled (LOAD_TEST_DISABLE_RATE_LIMIT=true)");
}
else
{
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Health checks should never be throttled.
        if (context.Request.Path.StartsWithSegments("/health"))
            return RateLimitPartition.GetNoLimiter("health");

        // After session middleware: one bucket per login session (not per IP).
        var username = context.Session.GetString("Username");
        if (!string.IsNullOrWhiteSpace(username))
        {
            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"session:{context.Session.Id}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 400,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 8
                });
        }

        var ip = context.Connection.RemoteIpAddress;
        if (ip != null && (System.Net.IPAddress.IsLoopback(ip) || ip.ToString() == "::1"))
            return RateLimitPartition.GetNoLimiter("loopback");

        var ipKey = ip?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"ip:{ipKey}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 400,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 16
            });
    });
});
}

var statsPath = isProd 
    ? Path.Combine("/data-keys", "question_stats.json")
    : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "question_stats.json");
builder.Services.AddSingleton(new QuestionStatsService(statsPath));

var questionReportsPath = isProd
    ? Path.Combine("/data-keys", "question_reports.json")
    : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "question_reports.json");
builder.Services.AddSingleton(new QuestionReportService(questionReportsPath));

var progressDir = isProd
    ? Path.Combine("/data-keys", "progress")
    : Path.Combine(Directory.GetCurrentDirectory(), "progress");
builder.Services.AddSingleton<RememberMeService>();
builder.Services.AddSingleton<UserProgressStore>(sp =>
    new UserProgressStore(sp.GetRequiredService<IConfiguration>(), sp.GetService<UserStatsService>()));
builder.Services.AddSingleton<UserProgressService>(sp =>
    new UserProgressService(
        progressDir,
        sp.GetRequiredService<AuthService>(),
        sp.GetService<UserProgressStore>(),
        sp.GetService<UserStatsService>(),
        sp.GetService<UserQuestionStatsStore>()));
builder.Services.AddSingleton<AchievementService>();
builder.Services.AddSingleton<LeaderboardDataService>();
builder.Services.AddSingleton<ActivityEventService>();
builder.Services.AddSingleton<UserFeedbackService>();
builder.Services.AddSingleton<DashboardDataService>();
builder.Services.AddSingleton<AdminUserSupportService>();
builder.Services.AddSingleton<UserDeletionService>(sp =>
    new UserDeletionService(
        sp.GetRequiredService<AuthService>(),
        sp.GetService<TestSessionService>(),
        sp.GetService<UserProgressService>(),
        sp.GetService<UserStatsService>()));
builder.Services.AddSingleton<DataRetentionService>(sp =>
    new DataRetentionService(
        sp.GetService<ActivityEventService>(),
        sp.GetService<TestSessionService>()));
builder.Services.AddHostedService<DataRetentionHostedService>();
builder.Services.AddSingleton<SystemHealthService>();
builder.Services.AddSingleton<SystemVerificationService>();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = isProd ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var sbUrl = SupabaseConfiguration.Url(builder.Configuration);
var sbService = SupabaseConfiguration.ServiceRoleApiKey(builder.Configuration);
var sbBucket = SupabaseConfiguration.Bucket(builder.Configuration);
var sbTtl = SupabaseConfiguration.SignedUrlTtlSeconds(builder.Configuration);

if (!string.IsNullOrWhiteSpace(sbUrl) && !string.IsNullOrWhiteSpace(sbService))
{
    builder.Services.AddSingleton(new SupabaseStorageService(sbUrl!, sbService!, sbBucket, sbTtl));
}

var sbKeyForTests = sbService ?? SupabaseConfiguration.AnonApiKey(builder.Configuration);
if (!string.IsNullOrWhiteSpace(sbUrl) && !string.IsNullOrWhiteSpace(sbKeyForTests))
{
    builder.Services.AddSingleton<TestSessionService>(sp =>
        new TestSessionService(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetService<SupabaseStorageService>(),
            sp.GetService<ActivityEventService>()));
}
else
{
    Console.WriteLine("Test session service disabled — missing SUPABASE_URL or API key.");
}

if (string.IsNullOrWhiteSpace(sbUrl) || string.IsNullOrWhiteSpace(sbService))
{
    Console.WriteLine("Missing SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY (or SERVICE_ROLE_SECRET). Signed URLs won't work.");
}

var emailTo = NoodlesSimulator.Models.EmailConfiguration.EmailTo(builder.Configuration);
var emailUser = NoodlesSimulator.Models.EmailConfiguration.SmtpUser(builder.Configuration);
var emailPass = NoodlesSimulator.Models.EmailConfiguration.SmtpPass(builder.Configuration);

Console.WriteLine("Email configuration (set each once: EMAIL_TO, Email__SmtpUser, Email__SmtpPass):");
Console.WriteLine($"   EMAIL_TO: {(string.IsNullOrEmpty(emailTo) ? "NOT SET" : "SET")}");
Console.WriteLine($"   Email__SmtpUser: {(string.IsNullOrEmpty(emailUser) ? "NOT SET" : "SET")}");
Console.WriteLine($"   Email__SmtpPass: {(string.IsNullOrEmpty(emailPass) ? "NOT SET" : "SET")}");
if (string.IsNullOrEmpty(emailTo) || string.IsNullOrEmpty(emailUser) || string.IsNullOrEmpty(emailPass))
{
    Console.WriteLine("Email notifications will not work - missing configuration");
}
else
{
    Console.WriteLine("Email configuration looks good");
}

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

static bool IsAdminSession(HttpContext context)
{
    return string.Equals(context.Session.GetString("IsAdmin"), "1", StringComparison.Ordinal);
}

static bool IsAuthenticated(HttpContext context)
{
    var username = context.Session.GetString("Username");
    return !string.IsNullOrWhiteSpace(username);
}

static Task WritePlainError(HttpContext context, int statusCode, string message)
{
    context.Response.StatusCode = statusCode;
    return context.Response.WriteAsync(message);
}

static Task WriteJson(HttpContext context, object payload)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(payload));
}

static Task WriteServerError(HttpContext context, string prefix, Exception ex)
{
    Console.WriteLine($"[{prefix}] {ex}");
    return WritePlainError(context, 500, "Server error");
}

static bool TryResolveAuthService(HttpContext context, out AuthService authService)
{
    authService = context.RequestServices.GetService<AuthService>();
    return authService != null;
}

static void InvalidateDashboardCaches(IServiceProvider services)
{
    services.GetService<DashboardDataService>()?.InvalidateCache();
    services.GetService<UserStatsService>()?.InvalidateCache();
}

static void RestoreUserStatsFromProgress(HttpContext context, User user)
{
    var progress = context.RequestServices.GetService<UserProgressService>();
    if (progress == null) return;

    var (total, correct) = progress.GetAnswerTotals(user.Username);
    if (total > user.TotalAnswered)
        user.TotalAnswered = total;
    if (correct > user.CorrectAnswers)
        user.CorrectAnswers = correct;

    var data = progress.Load(user.Username);
    if (data.Xp > user.Xp)
        user.Xp = data.Xp;
    if (user.Xp > 0)
        user.Level = QuizGamification.LevelFromXp(user.Xp);
}

app.UseForwardedHeaders();
if (!isProd)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: https:; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
app.Use(async (context, next) =>
{
    var reqCookies = context.Request.Cookies;
    if (reqCookies.ContainsKey(".Noodles.Session"))
    {
        context.Response.Cookies.Delete(".Noodles.Session");
    }
    if (reqCookies.ContainsKey(".Noodles.Antiforgery"))
    {
        context.Response.Cookies.Delete(".Noodles.Antiforgery");
    }
    await next();
});
app.UseCookiePolicy();
app.UseSession();
app.UseMiddleware<SessionRestoreMiddleware>();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    finally
    {
        context.RequestServices.GetService<UserProgressService>()?.ClearRequestCache();
    }
});
if (!disableRateLimit)
{
    app.UseRateLimiter();
}
app.UseAuthorization();
app.MapRazorPages();

app.MapPost("/clear-session", async context =>
{
    try
    {
        if (!IsAuthenticated(context))
        {
            await WritePlainError(context, 401, "Unauthorized");
            return;
        }

        context.Session.Clear();
        RememberMeService.Clear(context.Response);
        context.Response.StatusCode = 200;
        await context.Response.CompleteAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ClearSession Error] {ex}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Server error");
    }
});

app.MapGet("/health", async context =>
{
    var config = context.RequestServices.GetRequiredService<IConfiguration>();
    var url = SupabaseConfiguration.Url(config);
    var anon = SupabaseConfiguration.AnonApiKey(config);
    var service = SupabaseConfiguration.ServiceRoleApiKey(config);
    var bucket = SupabaseConfiguration.Bucket(config);
    var ttl = SupabaseConfiguration.SignedUrlTtlSeconds(config).ToString();

    var isProdEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
    var imagesDir   = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
    var reportsDir  = isProdEnv ? "/data-keys/reports" : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
    var progressDir = isProdEnv ? "/data-keys/progress" : Path.Combine(Directory.GetCurrentDirectory(), "progress");

    var payload = new
    {
        ok = true,
        env = app.Environment.EnvironmentName,
        supabaseUrl = string.IsNullOrWhiteSpace(url) ? "missing" : "ok",
        supabaseAnon = string.IsNullOrWhiteSpace(anon) ? "missing" : "ok",
        supabaseService = string.IsNullOrWhiteSpace(service) ? "missing" : "ok",
        supabaseBucket = string.IsNullOrWhiteSpace(bucket) ? "missing" : "ok",
        supabaseTtlSeconds = ttl,
        imagesDir = Directory.Exists(imagesDir),
        reportsDir = Directory.Exists(reportsDir),
        progressDir = Directory.Exists(progressDir)
    };

    context.Response.StatusCode = StatusCodes.Status200OK;
    await WriteJson(context, payload);
});

app.MapGet("/signed", async (HttpContext ctx) =>
{
    if (!IsAuthenticated(ctx))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("Unauthorized");
        return;
    }

    var storage = ctx.RequestServices.GetService<SupabaseStorageService>();
    if (storage == null)
    {
        ctx.Response.StatusCode = 503;
        await ctx.Response.WriteAsync("Supabase Storage Service not available");
        return;
    }
    
    var path = ctx.Request.Query["path"].ToString();
    if (string.IsNullOrWhiteSpace(path))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("query ?path=<objectPath> is required");
        return;
    }
    // Restrict to known safe object path format and prevent path traversal-like patterns.
    if (path.Contains("..", StringComparison.Ordinal) || path.StartsWith("/", StringComparison.Ordinal))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("Invalid path");
        return;
    }

    var signedUrl = await storage.GetSignedUrlAsync(path);
    await ctx.Response.WriteAsync(signedUrl);
});

app.MapGet("/debug-random", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var (tracked, throttled) = NoodlesSimulator.Pages.IndexModel.GetThrottleSnapshot();
        var hist = NoodlesSimulator.Pages.IndexModel.GetGroupShownHistogramSnapshot();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Randomizer Debug");
        sb.AppendLine($"trackedQuestions: {tracked}");
        sb.AppendLine($"throttledNow: {throttled}");
        sb.AppendLine("groupShownHistogram: count->groups");
        foreach (var kv in hist.OrderBy(k => k.Key))
            sb.AppendLine($"  {kv.Key}: {kv.Value}");

        var session = context.Session;
        var recentJson = session.GetString("RecentQuestions") ?? "[]";
        sb.AppendLine($"recentSessionQuestions: {recentJson}");

        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(sb.ToString());
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"debug error: {ex.Message}");
    }
});

var api = app.MapGroup("/api");

api.MapGet("/dashboard-data", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var dashboard = context.RequestServices.GetService<DashboardDataService>();
        if (dashboard == null)
        {
            await WritePlainError(context, 503, "Dashboard service not available");
            return;
        }

        var forceRefresh = context.Request.Query.ContainsKey("fresh");
        var snapshot = await dashboard.GetSnapshotAsync(forceRefresh);
        await WriteJson(context, dashboard.ToApiPayload(snapshot));
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard API Error", ex);
    }
});

api.MapGet("/dashboard-activity", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var activity = context.RequestServices.GetService<ActivityEventService>();
        if (activity == null || !activity.IsEnabled)
        {
            await WriteJson(context, new { items = Array.Empty<object>() });
            return;
        }

        var limit = 50;
        if (int.TryParse(context.Request.Query["limit"], out var parsed) && parsed > 0 && parsed <= 100)
            limit = parsed;

        var events = await activity.GetRecentAsync(limit);
        await WriteJson(context, new
        {
            items = events.Select(e => new
            {
                id = e.Id,
                username = e.Username,
                eventType = e.EventType,
                payload = e.Payload,
                createdAt = e.CreatedAt.ToUniversalTime().ToString("o")
            })
        });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard Activity API Error", ex);
    }
});

api.MapGet("/dashboard-user", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var username = context.Request.Query["username"].ToString();
        if (string.IsNullOrWhiteSpace(username))
        {
            await WritePlainError(context, 400, "Missing username");
            return;
        }

        var dashboard = context.RequestServices.GetService<DashboardDataService>();
        if (dashboard == null)
        {
            await WritePlainError(context, 503, "Dashboard service not available");
            return;
        }

        var detail = await dashboard.GetUserDetailAsync(username);
        if (detail == null)
        {
            await WritePlainError(context, 404, "User not found");
            return;
        }

        await WriteJson(context, dashboard.ToApiUserDetail(detail));
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard User API Error", ex);
    }
});

api.MapPost("/dashboard-user-action", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        if (!TryResolveAuthService(context, out var authService))
        {
            await WritePlainError(context, 503, "AuthService not available");
            return;
        }

        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        var root = doc.RootElement;
        var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(username))
        {
            await WritePlainError(context, 400, "Missing username");
            return;
        }

        var user = await authService.GetUserAsync(username);
        if (user == null)
        {
            await WritePlainError(context, 404, "User not found");
            return;
        }

        var activity = context.RequestServices.GetService<ActivityEventService>();
        var adminUsername = context.Session.GetString("Username") ?? "Admin";

        if (root.TryGetProperty("isCheater", out var cheaterEl) &&
            (cheaterEl.ValueKind == System.Text.Json.JsonValueKind.True ||
             cheaterEl.ValueKind == System.Text.Json.JsonValueKind.False))
        {
            var wasCheater = user.IsCheater;
            user.IsCheater = cheaterEl.GetBoolean();
            if (wasCheater != user.IsCheater)
            {
                ActivityEventCatalog.LogAdminAction(
                    activity,
                    adminUsername,
                    user.Username,
                    user.IsCheater ? "cheater_mark" : "cheater_unmark");
            }
            if (wasCheater && !user.IsCheater)
                RestoreUserStatsFromProgress(context, user);
        }

        if (root.TryGetProperty("isBanned", out var bannedEl) &&
            (bannedEl.ValueKind == System.Text.Json.JsonValueKind.True ||
             bannedEl.ValueKind == System.Text.Json.JsonValueKind.False))
        {
            var wasBanned = user.IsBanned;
            user.IsBanned = bannedEl.GetBoolean();
            if (wasBanned != user.IsBanned)
            {
                ActivityEventCatalog.LogAdminAction(
                    activity,
                    adminUsername,
                    user.Username,
                    user.IsBanned ? "ban" : "unban");
            }
        }

        var ok = await authService.UpdateUserAsync(user);
        if (ok)
            InvalidateDashboardCaches(context.RequestServices);

        await WriteJson(context, new { success = ok, username = user.Username, isCheater = user.IsCheater, isBanned = user.IsBanned });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard User Action API Error", ex);
    }
});

api.MapPost("/dashboard-user-delete", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var deletion = context.RequestServices.GetService<UserDeletionService>();
        if (deletion == null)
        {
            await WritePlainError(context, 503, "User deletion service not available");
            return;
        }

        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        var root = doc.RootElement;
        var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(username))
        {
            await WritePlainError(context, 400, "Missing username");
            return;
        }

        var (success, error) = await deletion.DeleteUserCompletelyAsync(username);
        if (!success)
        {
            var status = string.Equals(error, "User not found", StringComparison.Ordinal) ? 404 : 400;
            await WritePlainError(context, status, error ?? "Delete failed");
            return;
        }

        InvalidateDashboardCaches(context.RequestServices);
        await WriteJson(context, new { success = true, username = username.Trim() });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard User Delete API Error", ex);
    }
});

api.MapPost("/dashboard-report-status", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var reports = context.RequestServices.GetService<QuestionReportService>();
        if (reports == null)
        {
            await WritePlainError(context, 503, "Report service not available");
            return;
        }

        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status))
        {
            await WritePlainError(context, 400, "Missing id or status");
            return;
        }

        var ok = string.Equals(status, QuestionReportService.StatusResolved, StringComparison.OrdinalIgnoreCase)
            ? reports.MarkResolved(id)
            : string.Equals(status, QuestionReportService.StatusOpen, StringComparison.OrdinalIgnoreCase)
                ? reports.Reopen(id)
                : false;

        if (!ok)
        {
            await WritePlainError(context, 404, "Report not found or invalid status");
            return;
        }

        InvalidateDashboardCaches(context.RequestServices);
        await WriteJson(context, new { success = true, id, status = status.ToLowerInvariant() });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard Report Status API Error", ex);
    }
});

api.MapPost("/dashboard-user-reset", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var support = context.RequestServices.GetService<AdminUserSupportService>();
        if (support == null)
        {
            await WritePlainError(context, 503, "Support service not available");
            return;
        }

        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        var username = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(username))
        {
            await WritePlainError(context, 400, "Missing username");
            return;
        }

        var (success, error) = await support.ResetUserProgressAsync(username);
        if (!success)
        {
            var status = string.Equals(error, "User not found", StringComparison.Ordinal) ? 404 : 400;
            await WritePlainError(context, status, error ?? "Reset failed");
            return;
        }

        InvalidateDashboardCaches(context.RequestServices);
        await WriteJson(context, new { success = true, username = username.Trim() });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard User Reset API Error", ex);
    }
});

api.MapPost("/dashboard-exam-expire", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var support = context.RequestServices.GetService<AdminUserSupportService>();
        if (support == null)
        {
            await WritePlainError(context, 503, "Support service not available");
            return;
        }

        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            await WritePlainError(context, 400, "Missing token");
            return;
        }

        var (success, error) = await support.ExpireExamAsync(token);
        if (!success)
        {
            var status = string.Equals(error, "Exam not found", StringComparison.Ordinal) ? 404 : 400;
            await WritePlainError(context, status, error ?? "Expire failed");
            return;
        }

        InvalidateDashboardCaches(context.RequestServices);
        await WriteJson(context, new { success = true, token = token.Trim() });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard Exam Expire API Error", ex);
    }
});

api.MapGet("/leaderboard-data", async context =>
{
    try
    {
        var leaderboard = context.RequestServices.GetService<LeaderboardDataService>();
        if (leaderboard == null)
        {
            await WritePlainError(context, 503, "Leaderboard service not available");
            return;
        }

        var currentUsername = context.Session.GetString("Username") ?? "";
        var tab = context.Request.Query["tab"].ToString();
        if (string.IsNullOrWhiteSpace(tab)) tab = "total";

        var (rows, hint) = await leaderboard.GetRowsAsync(tab);
        var data = rows.Select((u, index) => new
        {
            rank = index + 1,
            username = u.Username ?? "",
            scoreDisplay = u.ScoreDisplay,
            correctAnswers = u.ScoreDisplay,
            isOnline = u.IsOnline,
            isCurrentUser = u.Username == currentUsername
        }).Cast<object>().ToList();

        var response = new
        {
            users = data,
            tab,
            hint,
            currentUsername,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        await WriteJson(context, response);
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Leaderboard API Error", ex);
    }
});

api.MapPost("/notices/dismiss", async context =>
{
    if (!IsAuthenticated(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        if (!doc.RootElement.TryGetProperty("noticeId", out var noticeIdEl))
        {
            await WritePlainError(context, 400, "noticeId required");
            return;
        }

        var noticeId = noticeIdEl.GetString();
        if (!AppNotices.IsValid(noticeId) && !GitHubStarPrompt.IsGitHubStarNotice(noticeId))
        {
            await WritePlainError(context, 400, "Invalid noticeId");
            return;
        }

        if (!TryResolveAuthService(context, out var authService))
        {
            await WritePlainError(context, 503, "AuthService not available");
            return;
        }

        var username = context.Session.GetString("Username")!;
        var ok = await authService.DismissNoticeAsync(username, noticeId);
        if (!ok)
        {
            await WritePlainError(context, 500, "Failed to save");
            return;
        }

        var activity = context.RequestServices.GetService<ActivityEventService>();
        if (AppNotices.IsValid(noticeId))
            PromptActivityEvents.LogAppNoticeDismiss(activity, username, noticeId);
        else if (string.Equals(noticeId, GitHubStarPrompt.OptedInNoticeId, StringComparison.Ordinal))
            PromptActivityEvents.LogGitHubStarAccept(activity, username);
        else if (GitHubStarPrompt.IsGitHubStarNotice(noticeId))
        {
            var suffix = noticeId!.Length > "github-star-".Length
                ? noticeId["github-star-".Length..]
                : "";
            if (int.TryParse(suffix, out var milestone))
                PromptActivityEvents.LogGitHubStarLater(activity, username, milestone);
        }

        await WriteJson(context, new { ok = true });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dismiss Notice API Error", ex);
    }
});

api.MapPost("/feedback/submit", async context =>
{
    if (!IsAuthenticated(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        if (!doc.RootElement.TryGetProperty("campaignId", out var campaignIdEl) ||
            !doc.RootElement.TryGetProperty("rating", out var ratingEl))
        {
            await WritePlainError(context, 400, "campaignId and rating required");
            return;
        }

        var campaignId = campaignIdEl.GetString();
        var isAdmin = IsAdminSession(context);
        var progress = context.RequestServices.GetService<UserProgressService>();
        var achievementCount = progress?.Load(context.Session.GetString("Username")!)?.Achievements?.Count ?? 0;
        var expectedCampaignId = FeedbackCampaigns.GetActiveCampaignIdForUser(DateTime.UtcNow, isAdmin, achievementCount);
        if (string.IsNullOrWhiteSpace(campaignId) ||
            string.IsNullOrWhiteSpace(expectedCampaignId) ||
            !string.Equals(campaignId, expectedCampaignId, StringComparison.Ordinal))
        {
            await WritePlainError(context, 400, "Invalid or inactive campaign");
            return;
        }

        if (!ratingEl.TryGetInt32(out var rating) || rating < 1 || rating > 5)
        {
            await WritePlainError(context, 400, "rating must be between 1 and 5");
            return;
        }

        var message = doc.RootElement.TryGetProperty("message", out var messageEl)
            ? messageEl.GetString() ?? ""
            : "";

        var feedbackService = context.RequestServices.GetService<UserFeedbackService>();
        if (feedbackService == null || !feedbackService.IsEnabled)
        {
            await WritePlainError(context, 503, "Feedback service not available");
            return;
        }

        var username = context.Session.GetString("Username")!;

        var (success, alreadyResponded) = await feedbackService.SubmitAsync(username, campaignId, rating, message);
        if (alreadyResponded)
        {
            await WritePlainError(context, 409, "Already responded");
            return;
        }

        if (!success)
        {
            await WritePlainError(context, 500, "Failed to save feedback");
            return;
        }

        var activity = context.RequestServices.GetService<ActivityEventService>();
        PromptActivityEvents.LogFeedbackSubmit(activity, username, rating, campaignId!);

        await WriteJson(context, new { ok = true });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Feedback Submit API Error", ex);
    }
});

api.MapPost("/feedback/later", async context =>
{
    if (!IsAuthenticated(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        if (!doc.RootElement.TryGetProperty("campaignId", out var campaignIdEl))
        {
            await WritePlainError(context, 400, "campaignId required");
            return;
        }

        var campaignId = campaignIdEl.GetString();
        var isAdmin = IsAdminSession(context);
        var progress = context.RequestServices.GetService<UserProgressService>();
        var achievementCount = progress?.Load(context.Session.GetString("Username")!)?.Achievements?.Count ?? 0;
        var expectedCampaignId = FeedbackCampaigns.GetActiveCampaignIdForUser(DateTime.UtcNow, isAdmin, achievementCount);
        if (string.IsNullOrWhiteSpace(campaignId) ||
            string.IsNullOrWhiteSpace(expectedCampaignId) ||
            !string.Equals(campaignId, expectedCampaignId, StringComparison.Ordinal))
        {
            await WritePlainError(context, 400, "Invalid or inactive campaign");
            return;
        }

        var feedbackService = context.RequestServices.GetService<UserFeedbackService>();
        if (feedbackService == null || !feedbackService.IsEnabled)
        {
            await WritePlainError(context, 503, "Feedback service not available");
            return;
        }

        var username = context.Session.GetString("Username")!;
        var (success, alreadyResponded) = await feedbackService.RecordLaterAsync(username, campaignId);
        if (alreadyResponded)
        {
            await WritePlainError(context, 409, "Already responded");
            return;
        }

        if (!success)
        {
            await WritePlainError(context, 500, "Failed to save response");
            return;
        }

        var activity = context.RequestServices.GetService<ActivityEventService>();
        var milestone = FeedbackCampaigns.ParseMilestoneFromCampaignId(campaignId);
        PromptActivityEvents.LogFeedbackLater(activity, username, campaignId!, milestone);

        await WriteJson(context, new { ok = true });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Feedback Later API Error", ex);
    }
});

api.MapPost("/activity/prompt-shown", async context =>
{
    if (!IsAuthenticated(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
        if (!doc.RootElement.TryGetProperty("prompt", out var promptEl))
        {
            await WritePlainError(context, 400, "prompt required");
            return;
        }

        var prompt = promptEl.GetString();
        var username = context.Session.GetString("Username")!;
        var activity = context.RequestServices.GetService<ActivityEventService>();
        if (activity == null || !activity.IsEnabled)
        {
            await WriteJson(context, new { ok = true });
            return;
        }

        switch (prompt)
        {
            case "feedback":
            {
                var campaignId = doc.RootElement.TryGetProperty("campaignId", out var cEl)
                    ? cEl.GetString() ?? ""
                    : "";
                var milestone = doc.RootElement.TryGetProperty("milestone", out var mEl) &&
                                mEl.TryGetInt32(out var m)
                    ? m
                    : FeedbackCampaigns.ParseMilestoneFromCampaignId(campaignId);
                PromptActivityEvents.LogFeedbackPrompt(activity, username, milestone, campaignId);
                break;
            }
            case "github_star":
            {
                var milestone = doc.RootElement.TryGetProperty("milestone", out var mEl) &&
                                mEl.TryGetInt32(out var m)
                    ? m
                    : 0;
                PromptActivityEvents.LogGitHubStarPrompt(activity, username, milestone);
                break;
            }
            case "app_notice":
            {
                if (!doc.RootElement.TryGetProperty("noticeId", out var nEl))
                {
                    await WritePlainError(context, 400, "noticeId required");
                    return;
                }

                var noticeId = nEl.GetString();
                if (!AppNotices.IsValid(noticeId))
                {
                    await WritePlainError(context, 400, "Invalid noticeId");
                    return;
                }

                PromptActivityEvents.LogAppNoticePrompt(activity, username, noticeId!);
                break;
            }
            default:
                await WritePlainError(context, 400, "Invalid prompt");
                return;
        }

        await WriteJson(context, new { ok = true });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Prompt Shown API Error", ex);
    }
});
api.MapGet("/dashboard-feedback", async context =>
{
    if (!IsAdminSession(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        var feedbackService = context.RequestServices.GetService<UserFeedbackService>();
        if (feedbackService == null || !feedbackService.IsEnabled)
        {
            await WritePlainError(context, 503, "Feedback service not available");
            return;
        }

        var entries = await feedbackService.GetSubmittedFeedbackAsync();
        await WriteJson(context, new
        {
            campaignId = FeedbackCampaigns.MilestoneCampaignPrefix,
            entries = entries.Select(e => new
            {
                e.Id,
                e.Username,
                e.Rating,
                e.Message,
                e.Milestone,
                isLater = e.IsLater,
                createdAt = e.CreatedAt.ToString("o")
            })
        });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Dashboard Feedback API Error", ex);
    }
});

api.MapGet("/online-count", async context =>
{
    try
    {
        if (!TryResolveAuthService(context, out var authService))
        {
            await WritePlainError(context, 503, "AuthService not available");
            return;
        }

        var heartbeat = context.Request.Query["heartbeat"] == "1";
        var username = context.Session.GetString("Username");
        if (heartbeat && !string.IsNullOrEmpty(username) &&
            await authService.TouchLastSeenIfDueAsync(username))
        {
            InvalidateDashboardCaches(context.RequestServices);
        }

        var onlineCount = await authService.GetOnlineUserCountAsync();
        var data = new { online = onlineCount };

        await WriteJson(context, data);
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Online Count API Error", ex);
    }
});

api.MapGet("/stats-data", async context =>
{
    if (!IsAuthenticated(context))
    {
        await WritePlainError(context, 401, "Unauthorized");
        return;
    }

    try
    {
        if (!TryResolveAuthService(context, out var authService))
        {
            await WritePlainError(context, 503, "AuthService not available");
            return;
        }

        var username = context.Session.GetString("Username")!;
        var user = await authService.GetUserAsync(username);
        if (user == null)
        {
            await WritePlainError(context, 401, "Unauthorized");
            return;
        }

        var progressService = context.RequestServices.GetService<UserProgressService>();
        var correct = user.CorrectAnswers;
        var total = user.TotalAnswered;
        var xp = user.Xp;
        var streak = context.Session.GetInt32("CurrentStreak") ?? 0;
        var level = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(xp);

        if (progressService != null)
        {
            var progress = progressService.Load(username);
            var (progTotal, progCorrect) = progressService.GetAnswerTotals(username);
            correct = Math.Max(correct, progCorrect);
            total = Math.Max(total, progTotal);
            xp = Math.Max(xp, progress.Xp);
        }

        var successRate = total > 0 ? (int)Math.Round((double)correct / total * 100) : 0;
        var xpProgressPercent = QuizGamification.XpProgressPercent(xp);
        var weeklyRank = 0;
        if (progressService != null)
        {
            var board = progressService.GetWeeklyLeaderboard(50);
            for (var i = 0; i < board.Count; i++)
            {
                if (string.Equals(board[i].Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    weeklyRank = i + 1;
                    break;
                }
            }
        }

        await WriteJson(context, new
        {
            correct,
            total,
            successRate,
            streak,
            level,
            xp,
            xpProgressPercent,
            xpToNextLevel = QuizGamification.XpToNextLevel(xp),
            weeklyRank
        });
    }
    catch (Exception ex)
    {
        await WriteServerError(context, "Stats Data API Error", ex);
    }
});

api.MapGet("/question-difficulty", (HttpContext context) =>
{
    try
    {
        var svc = context.RequestServices.GetService<QuestionStatsService>();
        if (svc == null) return Results.Problem("Stats service unavailable", statusCode: 503);
        var items = svc.GetAll();
        return Results.Json(new { items });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var url = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
        Console.WriteLine("Noodles Simulator is running!");
        Console.WriteLine($"Listening on: {url}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup Log Error] {ex}");
    }
});

var isProdStart = app.Environment.IsProduction();
var reportsDir = isProdStart ? "/data-keys/reports" : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
if (!Directory.Exists(reportsDir))
    Directory.CreateDirectory(reportsDir);

var reportsJson = Path.Combine(reportsDir, "reports.json");
if (!File.Exists(reportsJson))
    File.WriteAllText(reportsJson, "[]");

var questionReportsJson = Path.Combine(reportsDir, "question_reports.json");
if (!File.Exists(questionReportsJson))
    File.WriteAllText(questionReportsJson, "[]");

if (!Directory.Exists(progressDir))
    Directory.CreateDirectory(progressDir);

_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var storage = scope.ServiceProvider.GetService<SupabaseStorageService>();
        if (storage == null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await storage.ListFilesAsync("");
        Console.WriteLine($"[Startup] Storage file list warmed ({sw.ElapsedMilliseconds}ms)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Storage warmup failed: {ex.Message}");
    }
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
app.Urls.Add($"http://*:{port}");

app.Run();
