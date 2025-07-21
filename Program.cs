using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoodlesSimulator.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<EmailService>();

// Persist Data Protection keys to /etc/secrets/data-keys for Render Secret File or Persistent Disk
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/etc/secrets/data-keys"));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Noodles.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromHours(1);
});

builder.Services.AddHttpClient();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
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
    var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
    var key = Environment.GetEnvironmentVariable("SUPABASE_KEY");
    var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
    var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
    var progressDir = Path.Combine(Directory.GetCurrentDirectory(), "progress");
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"SUPABASE_URL: {(string.IsNullOrEmpty(url) ? "MISSING" : "OK")}");
    sb.AppendLine($"SUPABASE_KEY: {(string.IsNullOrEmpty(key) ? "MISSING" : "OK")}");
    sb.AppendLine($"wwwroot/images: {(Directory.Exists(imagesDir) ? "OK" : "MISSING")}");
    sb.AppendLine($"wwwroot/reports: {(Directory.Exists(reportsDir) ? "OK" : "MISSING")}");
    sb.AppendLine($"progress: {(Directory.Exists(progressDir) ? "OK" : "MISSING")}");
    if (Directory.Exists(imagesDir))
        sb.AppendLine($"images count: {Directory.GetFiles(imagesDir).Length}");
    if (Directory.Exists(reportsDir))
        sb.AppendLine($"reports count: {Directory.GetFiles(reportsDir).Length}");
    if (Directory.Exists(progressDir))
        sb.AppendLine($"progress count: {Directory.GetFiles(progressDir).Length}");
    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync(sb.ToString());
});

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

var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
if (!Directory.Exists(reportsDir))
    Directory.CreateDirectory(reportsDir);
var reportsJson = Path.Combine(reportsDir, "reports.json");
if (!File.Exists(reportsJson))
    File.WriteAllText(reportsJson, "[]");
var progressDir = Path.Combine(Directory.GetCurrentDirectory(), "progress");
if (!Directory.Exists(progressDir))
    Directory.CreateDirectory(progressDir);

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

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

app.Run();
