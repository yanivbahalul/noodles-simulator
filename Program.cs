
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuthService>();

builder.Services.AddSingleton<EmailService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<EmailService>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new EmailService(logger, configuration);
});

// Data protection - disabled to prevent key ring issues
// builder.Services.AddDataProtection();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Session configuration - simplified
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Noodles.Session.v2";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.MaxAge = TimeSpan.FromHours(1);
});

builder.Services.AddHttpClient();

// Question difficulty stats
var statsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "question_stats.json");
builder.Services.AddSingleton(new QuestionStatsService(statsPath));

// Cookie policy
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

// Antiforgery - disabled to prevent key ring issues
// builder.Services.AddAntiforgery();

// Environment variables
var sbUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
var sbAnon = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")
            ?? Environment.GetEnvironmentVariable("SUPABASE_KEY")
            ?? Environment.GetEnvironmentVariable("ANON_PUBLIC");
var sbService = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY")
               ?? Environment.GetEnvironmentVariable("SERVICE_ROLE_SECRET");
var sbBucket = Environment.GetEnvironmentVariable("SUPABASE_BUCKET") ?? "noodles-images";
var sbTtlStr = Environment.GetEnvironmentVariable("SUPABASE_SIGNED_URL_TTL") ?? "3600";
var sbTtl = int.TryParse(sbTtlStr, out var ttlVal) ? ttlVal : 3600;

// Supabase storage service
if (!string.IsNullOrWhiteSpace(sbUrl) && !string.IsNullOrWhiteSpace(sbService))
{
    builder.Services.AddSingleton(new SupabaseStorageService(sbUrl!, sbService!, sbBucket, sbTtl));
}
else
{
    Console.WriteLine("⚠️  Missing SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY (or SERVICE_ROLE_SECRET). Signed URLs won't work.");
}

// Email configuration debug
var emailTo = Environment.GetEnvironmentVariable("EMAIL_TO");
var emailUser = Environment.GetEnvironmentVariable("Email__SmtpUser");
var emailPass = Environment.GetEnvironmentVariable("Email__SmtpPass");

Console.WriteLine($"📧 Email configuration:");
Console.WriteLine($"   EMAIL_TO: {emailTo}");
Console.WriteLine($"   SmtpUser: {emailUser}");
Console.WriteLine($"   SmtpPass: {(string.IsNullOrEmpty(emailPass) ? "NOT SET" : "SET")}");

if (string.IsNullOrEmpty(emailTo) || string.IsNullOrEmpty(emailUser) || string.IsNullOrEmpty(emailPass))
{
    Console.WriteLine("⚠️  Email notifications will not work - missing configuration");
}
else
{
    Console.WriteLine("✅ Email configuration looks good");
}

var app = builder.Build();

// Middleware pipeline
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
// Drop legacy cookies so the server won't try to decrypt them
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
app.UseAuthorization();
app.MapRazorPages();

app.MapPost("/clear-session", async context =>
{
    try
    {
        context.Session.Clear();
        context.Response.Cookies.Delete("Username");
        context.Response.StatusCode = 200;
        await context.Response.CompleteAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ClearSession Error] {ex}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Server error: {ex.Message}");
    }
});

app.MapGet("/health", async context =>
{
    var url      = Environment.GetEnvironmentVariable("SUPABASE_URL");
    var anon     = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")
                   ?? Environment.GetEnvironmentVariable("SUPABASE_KEY")
                   ?? Environment.GetEnvironmentVariable("ANON_PUBLIC");
    var service  = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY")
                   ?? Environment.GetEnvironmentVariable("SERVICE_ROLE_SECRET");
    var bucket   = Environment.GetEnvironmentVariable("SUPABASE_BUCKET");
    var ttl      = Environment.GetEnvironmentVariable("SUPABASE_SIGNED_URL_TTL");

    var imagesDir   = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
    var reportsDir  = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
    var progressDir = Path.Combine(Directory.GetCurrentDirectory(), "progress");

    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"SUPABASE_URL: {(string.IsNullOrEmpty(url) ? "MISSING" : "OK")}");
    sb.AppendLine($"SUPABASE_ANON_KEY/ANON_PUBLIC: {(string.IsNullOrEmpty(anon) ? "MISSING" : "OK")}");
    sb.AppendLine($"SUPABASE_SERVICE_ROLE_KEY/SERVICE_ROLE_SECRET: {(string.IsNullOrEmpty(service) ? "MISSING" : "OK")}");
    sb.AppendLine($"SUPABASE_BUCKET: {(string.IsNullOrEmpty(bucket) ? "MISSING" : bucket)}");
    sb.AppendLine($"SUPABASE_SIGNED_URL_TTL: {(string.IsNullOrEmpty(ttl) ? "MISSING" : ttl)}");
    sb.AppendLine($"wwwroot/images: {(Directory.Exists(imagesDir) ? "OK" : "MISSING")}");
    sb.AppendLine($"wwwroot/reports: {(Directory.Exists(reportsDir) ? "OK" : "MISSING")}");
    sb.AppendLine($"progress: {(Directory.Exists(progressDir) ? "OK" : "MISSING")}");
    if (Directory.Exists(imagesDir))   sb.AppendLine($"images count: {Directory.GetFiles(imagesDir).Length}");
    if (Directory.Exists(reportsDir))  sb.AppendLine($"reports count: {Directory.GetFiles(reportsDir).Length}");
    if (Directory.Exists(progressDir)) sb.AppendLine($"progress count: {Directory.GetFiles(progressDir).Length}");

    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync(sb.ToString());
});

app.MapGet("/signed", async (HttpContext ctx) =>
{
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
    var signedUrl = await storage.GetSignedUrlAsync(path);
    await ctx.Response.WriteAsync(signedUrl);
});

app.MapGet("/debug-random", async context =>
{
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

        // Session recent list
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

// API endpoints
app.MapGet("/api/dashboard-data", async context =>
{
    try
    {
        var authService = context.RequestServices.GetService<AuthService>();
        if (authService == null)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("AuthService not available");
            return;
        }

        var allUsers = await authService.GetAllUsersLight();
        var onlineUsers = allUsers.Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).ToList();
        var cheaters = allUsers.Where(u => u.IsCheater).ToList();
        var bannedUsers = allUsers.Where(u => u.IsBanned).ToList();
        var topUsers = allUsers.OrderByDescending(u => u.CorrectAnswers).Take(5).ToList();
        var averageSuccessRate = allUsers.Where(u => u.TotalAnswered > 0)
            .Select(u => (double)u.CorrectAnswers / u.TotalAnswered)
            .DefaultIfEmpty(0).Average() * 100;

        var data = new
        {
            allUsersCount = allUsers.Count,
            onlineUsersCount = onlineUsers.Count,
            cheatersCount = cheaters.Count,
            bannedUsersCount = bannedUsers.Count,
            averageSuccessRate = Math.Round(averageSuccessRate, 1),
            onlineUsersList = onlineUsers.Select(u => new
            {
                username = u.Username,
                totalAnswered = u.TotalAnswered,
                correctAnswers = u.CorrectAnswers,
                successRate = u.TotalAnswered > 0 ? Math.Round((double)u.CorrectAnswers / u.TotalAnswered * 100, 1) : 0
            }).ToList(),
            topUsersList = topUsers.Select(u => new
            {
                username = u.Username,
                totalAnswered = u.TotalAnswered,
                correctAnswers = u.CorrectAnswers,
                successRate = u.TotalAnswered > 0 ? Math.Round((double)u.CorrectAnswers / u.TotalAnswered * 100, 1) : 0
            }).ToList()
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(data));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Dashboard API Error] {ex}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Server error: {ex.Message}");
    }
});

app.MapGet("/api/leaderboard-data", async context =>
{
    try
    {
        var authService = context.RequestServices.GetService<AuthService>();
        if (authService == null)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("AuthService not available");
            return;
        }

        // Get current username from session
        var currentUsername = context.Session.GetString("Username") ?? "";

        var topUsers = await authService.GetTopUsers(50);
        
        // Ensure we have valid data
        if (topUsers == null)
        {
            topUsers = new List<User>();
        }
        
        var data = topUsers.Select((u, index) => new
        {
            rank = index + 1,
            username = u.Username ?? "",
            totalAnswered = u.TotalAnswered,
            correctAnswers = u.CorrectAnswers,
            successRate = u.TotalAnswered > 0 ? Math.Round((double)u.CorrectAnswers / u.TotalAnswered * 100, 1) : 0,
            isOnline = u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5),
            isCurrentUser = u.Username == currentUsername
        }).ToList();

        var response = new
        {
            users = data,
            currentUsername = currentUsername,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Leaderboard API Error] {ex}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Server error: {ex.Message}");
    }
});

app.MapGet("/api/online-count", async context =>
{
    try
    {
        var authService = context.RequestServices.GetService<AuthService>();
        if (authService == null)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("AuthService not available");
            return;
        }

        var onlineCount = await authService.GetOnlineUserCount();
        var data = new { online = onlineCount };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(data));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Online Count API Error] {ex}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Server error: {ex.Message}");
    }
});

// Difficulty API
app.MapGet("/api/question-difficulty", (HttpContext context) =>
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

// Application startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var url = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
        Console.WriteLine("🍜 Noodles Simulator is running!");
        Console.WriteLine($"🔗 Listening on: {url}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup Log Error] {ex}");
    }
});

// Initialize directories
var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
if (!Directory.Exists(reportsDir))
    Directory.CreateDirectory(reportsDir);

var reportsJson = Path.Combine(reportsDir, "reports.json");
if (!File.Exists(reportsJson))
    File.WriteAllText(reportsJson, "[]");

var progressDir = Path.Combine(Directory.GetCurrentDirectory(), "progress");
if (!Directory.Exists(progressDir))
    Directory.CreateDirectory(progressDir);

// Cleanup old progress files
if (Directory.Exists(progressDir))
{
    var files = Directory.GetFiles(progressDir, "*.json");
    var threshold = DateTime.Now.AddDays(-7);

    foreach (var file in files)
    {
        var lastWrite = File.GetLastWriteTime(file);
        if (lastWrite < threshold)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Progress File Delete Error] {ex}");
            }
        }
    }
}

// Start application
var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
app.Urls.Add($"http://*:{port}");

app.Run();
