using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Api;

internal static partial class ApiEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapPost("/clear-session", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            context.Session.Clear();
            RememberMeService.Clear(context.Response);
            context.Response.StatusCode = 200;
            await context.Response.CompleteAsync();
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
                env = context.RequestServices.GetRequiredService<IHostEnvironment>().EnvironmentName,
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
            await ApiHelpers.WriteJson(context, payload);
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/signed", async (HttpContext ctx) =>
            {
                if (!await ApiHelpers.RequireAuthAsync(ctx)) return;

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
                if (!await ApiHelpers.RequireAdminAsync(context)) return;

                var (tracked, throttled) = PracticeQuestionPickerService.GetThrottleSnapshot();
                var hist = PracticeQuestionPickerService.GetGroupShownHistogramSnapshot();

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
            });
        }

        var api = app.MapGroup("/api");
        MapDashboard(api);
        MapApp(api);
        MapQuestions(api);
    }
}
