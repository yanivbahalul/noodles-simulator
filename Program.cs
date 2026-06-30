
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
using NoodlesSimulator.Api;
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

if (args.Contains("--ponytail-check", StringComparer.OrdinalIgnoreCase))
{
    PonytailSelfCheck.Run();
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<UserStatsService>();
builder.Services.AddSingleton<UserQuestionStatsStore>();
builder.Services.AddSingleton<AuthService>(sp =>
    new AuthService(sp.GetRequiredService<IConfiguration>(), sp.GetService<UserStatsService>()));
builder.Services.AddSingleton<QuestionDifficultyService>();
builder.Services.AddSingleton<QuestionExplanationService>(sp =>
    new QuestionExplanationService(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetService<SupabaseStorageService>()));

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

var questionReportsPath = isProd
    ? Path.Combine("/data-keys", "question_reports.json")
    : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "question_reports.json");
builder.Services.AddSingleton(new QuestionReportService(questionReportsPath));

var progressDir = isProd
    ? Path.Combine("/data-keys", "progress")
    : Path.Combine(Directory.GetCurrentDirectory(), "progress");
builder.Services.AddSingleton<RememberMeService>();
builder.Services.AddSingleton<LoginThrottleService>();
builder.Services.AddSingleton<LoginPageService>();
builder.Services.AddSingleton<TestResultsPageService>();
builder.Services.AddSingleton<UserProgressStore>(sp =>
    new UserProgressStore(sp.GetRequiredService<IConfiguration>(), sp.GetService<UserStatsService>()));
builder.Services.AddSingleton<UserProgressService>(sp =>
    new UserProgressService(
        progressDir,
        sp.GetRequiredService<AuthService>(),
        sp.GetService<UserProgressStore>(),
        sp.GetService<UserStatsService>(),
        sp.GetService<UserQuestionStatsStore>(),
        sp.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
builder.Services.AddSingleton<AchievementService>();
builder.Services.AddSingleton<LeaderboardDataService>();
builder.Services.AddSingleton<ActivityEventService>();
builder.Services.AddSingleton<UserFeedbackService>();
builder.Services.AddSingleton<DashboardDataService>();
builder.Services.AddSingleton<QuestionGroupLoader>(sp =>
    new QuestionGroupLoader(sp.GetService<SupabaseStorageService>()));
builder.Services.AddSingleton<PracticeQuestionPickerService>(sp =>
    new PracticeQuestionPickerService(
        sp.GetService<QuestionGroupLoader>(),
        sp.GetService<QuestionDifficultyService>(),
        sp.GetService<UserProgressService>()));
builder.Services.AddSingleton<PracticeQuizService>(sp =>
    new PracticeQuizService(
        sp.GetRequiredService<PracticeQuestionPickerService>(),
        sp.GetService<SupabaseStorageService>()));
builder.Services.AddSingleton<PracticeAnswerService>(sp =>
    new PracticeAnswerService(
        sp.GetRequiredService<AuthService>(),
        sp.GetService<UserProgressService>(),
        sp.GetService<AchievementService>(),
        sp.GetService<ActivityEventService>(),
        sp.GetService<QuestionDifficultyService>(),
        sp.GetRequiredService<PracticeQuizService>()));
builder.Services.AddSingleton<PracticeIndexPageService>(sp =>
    new PracticeIndexPageService(
        sp.GetRequiredService<AuthService>(),
        sp.GetService<UserProgressService>(),
        sp.GetService<UserFeedbackService>(),
        sp.GetService<ActivityEventService>(),
        sp.GetRequiredService<PracticeQuizService>()));
builder.Services.AddSingleton<TestExamService>(sp =>
    new TestExamService(
        sp.GetService<SupabaseStorageService>(),
        sp.GetService<TestSessionService>(),
        sp.GetService<QuestionDifficultyService>(),
        sp.GetService<ActivityEventService>(),
        sp.GetService<QuestionGroupLoader>()));
builder.Services.AddSingleton<AdminUserService>(sp =>
    new AdminUserService(
        sp.GetRequiredService<AuthService>(),
        sp.GetService<UserProgressService>(),
        sp.GetService<TestSessionService>(),
        sp.GetService<ActivityEventService>(),
        sp.GetService<UserStatsService>()));
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

PonytailSelfCheck.RunStartup();

if (app.Environment.IsProduction())
{
    app.UseHsts();
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
        "default-src 'self'; img-src 'self' data: https:; media-src 'self' https:; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
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
app.UseMiddleware<ApiExceptionMiddleware>();

app.MapRazorPages();
ApiEndpoints.Map(app);

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
        var explanations = scope.ServiceProvider.GetService<QuestionExplanationService>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (storage != null)
            await storage.ListFilesAsync("");
        if (explanations != null)
            await explanations.WarmReadyFilesAsync();
        Console.WriteLine($"[Startup] Storage + explanation list warmed ({sw.ElapsedMilliseconds}ms)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Storage warmup failed: {ex.Message}");
    }
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
app.Urls.Add($"http://*:{port}");

app.Run();
