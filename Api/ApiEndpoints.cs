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

internal static class ApiEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapPost("/clear-session", async context =>
        {
            try
            {
                if (!await ApiHelpers.RequireAuthAsync(context)) return;

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
                if (!await ApiHelpers.RequireAdminAsync(context)) return;

                try
                {
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
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync($"debug error: {ex.Message}");
                }
            });

        }

        var api = app.MapGroup("/api");

        api.MapGet("/dashboard-data", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var dashboard = context.RequestServices.GetService<DashboardDataService>();
                if (dashboard == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Dashboard service not available");
                    return;
                }

                var forceRefresh = context.Request.Query.ContainsKey("fresh");
                var snapshot = await dashboard.GetSnapshotAsync(forceRefresh);
                await ApiHelpers.WriteJson(context, dashboard.ToApiPayload(snapshot));
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard API Error", ex);
            }
        });

        api.MapGet("/dashboard-widget", async context =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            if (!ApiHelpers.IsWidgetAuthorized(context, config))
            {
                await ApiHelpers.WritePlainError(context, 401, "Unauthorized");
                return;
            }

            try
            {
                var dashboard = context.RequestServices.GetService<DashboardDataService>();
                if (dashboard == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Dashboard service not available");
                    return;
                }

                var forceRefresh = context.Request.Query.ContainsKey("fresh");
                var snapshot = await dashboard.GetSnapshotAsync(forceRefresh);
                await ApiHelpers.WriteJson(context, dashboard.ToWidgetPayload(snapshot));
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard Widget API Error", ex);
            }
        });

        api.MapGet("/dashboard-activity", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var activity = context.RequestServices.GetService<ActivityEventService>();
                if (activity == null || !activity.IsEnabled)
                {
                    await ApiHelpers.WriteJson(context, new { items = Array.Empty<object>() });
                    return;
                }

                var limit = 50;
                if (int.TryParse(context.Request.Query["limit"], out var parsed) && parsed > 0 && parsed <= 100)
                    limit = parsed;

                var events = await activity.GetRecentAsync(limit);
                await ApiHelpers.WriteJson(context, new
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
                await ApiHelpers.WriteServerError(context, "Dashboard Activity API Error", ex);
            }
        });

        api.MapGet("/dashboard-user", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var username = context.Request.Query["username"].ToString();
                if (string.IsNullOrWhiteSpace(username))
                {
                    await ApiHelpers.WritePlainError(context, 400, "Missing username");
                    return;
                }

                var dashboard = context.RequestServices.GetService<DashboardDataService>();
                if (dashboard == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Dashboard service not available");
                    return;
                }

                var detail = await dashboard.GetUserDetailAsync(username);
                if (detail == null)
                {
                    await ApiHelpers.WritePlainError(context, 404, "User not found");
                    return;
                }

                await ApiHelpers.WriteJson(context, dashboard.ToApiUserDetail(detail));
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard User API Error", ex);
            }
        });

        api.MapPost("/dashboard-user-action", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                if (!ApiHelpers.TryResolveAuthService(context, out var authService))
                {
                    await ApiHelpers.WritePlainError(context, 503, "AuthService not available");
                    return;
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                var root = doc.RootElement;
                var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
                if (string.IsNullOrWhiteSpace(username))
                {
                    await ApiHelpers.WritePlainError(context, 400, "Missing username");
                    return;
                }

                var user = await authService.GetUserAsync(username);
                if (user == null)
                {
                    await ApiHelpers.WritePlainError(context, 404, "User not found");
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
                        activity?.Log(
                            user.Username,
                            ActivityEventCatalog.AdminAction,
                            new Dictionary<string, object>
                            {
                                ["action"] = user.IsCheater ? "cheater_mark" : "cheater_unmark",
                                ["admin"] = adminUsername ?? "Admin"
                            });
                    }
                    if (wasCheater && !user.IsCheater)
                        await ApiHelpers.RestoreUserStatsFromProgressAsync(context, user);
                }

                if (root.TryGetProperty("isBanned", out var bannedEl) &&
                    (bannedEl.ValueKind == System.Text.Json.JsonValueKind.True ||
                     bannedEl.ValueKind == System.Text.Json.JsonValueKind.False))
                {
                    var wasBanned = user.IsBanned;
                    user.IsBanned = bannedEl.GetBoolean();
                    if (wasBanned != user.IsBanned)
                    {
                        activity?.Log(
                            user.Username,
                            ActivityEventCatalog.AdminAction,
                            new Dictionary<string, object>
                            {
                                ["action"] = user.IsBanned ? "ban" : "unban",
                                ["admin"] = adminUsername ?? "Admin"
                            });
                    }
                }

                var ok = await authService.UpdateUserAsync(user);
                if (ok)
                    ApiHelpers.InvalidateDashboardCaches(context.RequestServices);

                await ApiHelpers.WriteJson(context, new { success = ok, username = user.Username, isCheater = user.IsCheater, isBanned = user.IsBanned });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard User Action API Error", ex);
            }
        });

        api.MapPost("/dashboard-user-delete", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var admin = context.RequestServices.GetService<AdminUserService>();
                if (admin == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Admin service not available");
                    return;
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                var root = doc.RootElement;
                var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
                if (string.IsNullOrWhiteSpace(username))
                {
                    await ApiHelpers.WritePlainError(context, 400, "Missing username");
                    return;
                }

                var (success, error) = await admin.DeleteUserCompletelyAsync(username);
                if (!success)
                {
                    var status = string.Equals(error, "User not found", StringComparison.Ordinal) ? 404 : 400;
                    await ApiHelpers.WritePlainError(context, status, error ?? "Delete failed");
                    return;
                }

                ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
                await ApiHelpers.WriteJson(context, new { success = true, username = username.Trim() });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard User Delete API Error", ex);
            }
        });

        api.MapPost("/dashboard-report-status", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var reports = context.RequestServices.GetService<QuestionReportService>();
                if (reports == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Report service not available");
                    return;
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                var root = doc.RootElement;
                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status))
                {
                    await ApiHelpers.WritePlainError(context, 400, "Missing id or status");
                    return;
                }

                var ok = false;
                if (string.Equals(status, QuestionReportService.StatusResolved, StringComparison.OrdinalIgnoreCase))
                    ok = reports.MarkResolved(id);
                else if (string.Equals(status, QuestionReportService.StatusOpen, StringComparison.OrdinalIgnoreCase))
                    ok = reports.Reopen(id);

                if (!ok)
                {
                    await ApiHelpers.WritePlainError(context, 404, "Report not found or invalid status");
                    return;
                }

                ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
                await ApiHelpers.WriteJson(context, new { success = true, id, status = status.ToLowerInvariant() });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard Report Status API Error", ex);
            }
        });

        api.MapPost("/dashboard-user-reset", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var admin = context.RequestServices.GetService<AdminUserService>();
                if (admin == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Admin service not available");
                    return;
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                var username = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                if (string.IsNullOrWhiteSpace(username))
                {
                    await ApiHelpers.WritePlainError(context, 400, "Missing username");
                    return;
                }

                var (success, error) = await admin.ResetUserProgressAsync(username);
                if (!success)
                {
                    var status = string.Equals(error, "User not found", StringComparison.Ordinal) ? 404 : 400;
                    await ApiHelpers.WritePlainError(context, status, error ?? "Reset failed");
                    return;
                }

                ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
                await ApiHelpers.WriteJson(context, new { success = true, username = username.Trim() });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard User Reset API Error", ex);
            }
        });

        api.MapPost("/dashboard-exam-expire", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var admin = context.RequestServices.GetService<AdminUserService>();
                if (admin == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Admin service not available");
                    return;
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    await ApiHelpers.WritePlainError(context, 400, "Missing token");
                    return;
                }

                var (success, error) = await admin.ExpireExamAsync(token);
                if (!success)
                {
                    var status = string.Equals(error, "Exam not found", StringComparison.Ordinal) ? 404 : 400;
                    await ApiHelpers.WritePlainError(context, status, error ?? "Expire failed");
                    return;
                }

                ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
                await ApiHelpers.WriteJson(context, new { success = true, token = token.Trim() });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dashboard Exam Expire API Error", ex);
            }
        });

        api.MapGet("/leaderboard-data", async context =>
        {
            try
            {
                var leaderboard = context.RequestServices.GetService<LeaderboardDataService>();
                if (leaderboard == null)
                {
                    await ApiHelpers.WritePlainError(context, 503, "Leaderboard service not available");
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

                await ApiHelpers.WriteJson(context, response);
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Leaderboard API Error", ex);
            }
        });

        api.MapPost("/notices/dismiss", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            try
            {
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                if (!doc.RootElement.TryGetProperty("noticeId", out var noticeIdEl))
                {
                    await ApiHelpers.WritePlainError(context, 400, "noticeId required");
                    return;
                }

                var noticeId = noticeIdEl.GetString();
                if (!AppNotices.IsValid(noticeId) && !GitHubStarPrompt.IsGitHubStarNotice(noticeId) && !WelcomePrompt.IsValidNoticeId(noticeId))
                {
                    await ApiHelpers.WritePlainError(context, 400, "Invalid noticeId");
                    return;
                }

                if (!ApiHelpers.TryResolveAuthService(context, out var authService))
                {
                    await ApiHelpers.WritePlainError(context, 503, "AuthService not available");
                    return;
                }

                var username = context.Session.GetString("Username")!;
                var ok = await authService.DismissNoticeAsync(username, noticeId);
                if (!ok)
                {
                    await ApiHelpers.WritePlainError(context, 500, "Failed to save");
                    return;
                }

                var activity = context.RequestServices.GetService<ActivityEventService>();
                if (AppNotices.IsValid(noticeId))
                    activity?.Log(username, ActivityEventCatalog.AppNoticeDismiss, new Dictionary<string, object>
                    {
                        ["noticeId"] = noticeId ?? ""
                    });
                else if (string.Equals(noticeId, GitHubStarPrompt.OptedInNoticeId, StringComparison.Ordinal))
                    activity?.Log(username, ActivityEventCatalog.GitHubStarAccept);
                else if (GitHubStarPrompt.IsGitHubStarNotice(noticeId))
                {
                    var suffix = noticeId!.Length > "github-star-".Length
                        ? noticeId["github-star-".Length..]
                        : "";
                    if (int.TryParse(suffix, out var milestone))
                        activity?.Log(username, ActivityEventCatalog.GitHubStarLater, new Dictionary<string, object>
                        {
                            ["milestone"] = milestone
                        });
                }
                else if (WelcomePrompt.IsValidNoticeId(noticeId))
                {
                    activity?.Log(username, ActivityEventCatalog.WelcomeCs24Dismiss);
                    WelcomePrompt.ClearPending(context);
                }

                await ApiHelpers.WriteJson(context, new { ok = true });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Dismiss Notice API Error", ex);
            }
        });

        api.MapPost("/welcome/cs24-click", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            try
            {
                if (!ApiHelpers.TryResolveAuthService(context, out var authService))
                {
                    await ApiHelpers.WritePlainError(context, 503, "AuthService not available");
                    return;
                }

                var username = context.Session.GetString("Username")!;
                var ok = await authService.DismissNoticeAsync(username, WelcomePrompt.NoticeId);
                if (!ok)
                {
                    await ApiHelpers.WritePlainError(context, 500, "Failed to save");
                    return;
                }

                var activity = context.RequestServices.GetService<ActivityEventService>();
                activity?.Log(username, ActivityEventCatalog.WelcomeCs24Click);
                WelcomePrompt.ClearPending(context);

                await ApiHelpers.WriteJson(context, new { ok = true });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Welcome CS24 Click API Error", ex);
            }
        });

        api.MapPost("/feedback/submit", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            try
            {
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                if (!doc.RootElement.TryGetProperty("campaignId", out var campaignIdEl) ||
                    !doc.RootElement.TryGetProperty("rating", out var ratingEl))
                {
                    await ApiHelpers.WritePlainError(context, 400, "campaignId and rating required");
                    return;
                }

                var campaignId = campaignIdEl.GetString();
                var resolved = await ApiHelpers.TryResolveActiveFeedbackCampaignAsync(context, campaignId);
                if (resolved == null) return;
                var (username, activeCampaignId) = resolved.Value;

                if (!ratingEl.TryGetInt32(out var rating) || rating < 1 || rating > 5)
                {
                    await ApiHelpers.WritePlainError(context, 400, "rating must be between 1 and 5");
                    return;
                }

                var message = doc.RootElement.TryGetProperty("message", out var messageEl)
                    ? messageEl.GetString() ?? ""
                    : "";

                var feedbackService = await ApiHelpers.RequireFeedbackServiceAsync(context);
                if (feedbackService == null) return;

                var (success, alreadyResponded) = await feedbackService.SubmitAsync(username, activeCampaignId, rating, message);
                if (!await ApiHelpers.HandleFeedbackWriteResultAsync(context, success, alreadyResponded, "Failed to save feedback"))
                    return;

                var activity = context.RequestServices.GetService<ActivityEventService>();
                activity?.Log(username, ActivityEventCatalog.FeedbackSubmit, new Dictionary<string, object>
                {
                    ["rating"] = rating,
                    ["campaignId"] = activeCampaignId
                });

                await ApiHelpers.WriteJson(context, new { ok = true });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Feedback Submit API Error", ex);
            }
        });

        api.MapPost("/feedback/later", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            try
            {
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                if (!doc.RootElement.TryGetProperty("campaignId", out var campaignIdEl))
                {
                    await ApiHelpers.WritePlainError(context, 400, "campaignId required");
                    return;
                }

                var campaignId = campaignIdEl.GetString();
                var resolved = await ApiHelpers.TryResolveActiveFeedbackCampaignAsync(context, campaignId);
                if (resolved == null) return;
                var (username, activeCampaignId) = resolved.Value;

                var feedbackService = await ApiHelpers.RequireFeedbackServiceAsync(context);
                if (feedbackService == null) return;

                var (success, alreadyResponded) = await feedbackService.RecordLaterAsync(username, activeCampaignId);
                if (!await ApiHelpers.HandleFeedbackWriteResultAsync(context, success, alreadyResponded, "Failed to save response"))
                    return;

                var activity = context.RequestServices.GetService<ActivityEventService>();
                var milestone = FeedbackCampaigns.ParseMilestoneFromCampaignId(activeCampaignId);
                activity?.Log(username, ActivityEventCatalog.FeedbackLater, new Dictionary<string, object>
                {
                    ["campaignId"] = activeCampaignId,
                    ["milestone"] = milestone
                });

                await ApiHelpers.WriteJson(context, new { ok = true });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Feedback Later API Error", ex);
            }
        });

        api.MapPost("/activity/prompt-shown", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            try
            {
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
                if (!doc.RootElement.TryGetProperty("prompt", out var promptEl))
                {
                    await ApiHelpers.WritePlainError(context, 400, "prompt required");
                    return;
                }

                var prompt = promptEl.GetString();
                var username = context.Session.GetString("Username")!;
                var activity = context.RequestServices.GetService<ActivityEventService>();
                if (activity == null || !activity.IsEnabled)
                {
                    await ApiHelpers.WriteJson(context, new { ok = true });
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
                        activity.Log(username, ActivityEventCatalog.FeedbackPrompt, new Dictionary<string, object>
                        {
                            ["milestone"] = milestone,
                            ["campaignId"] = campaignId
                        });
                        break;
                    }
                    case "github_star":
                    {
                        var milestone = doc.RootElement.TryGetProperty("milestone", out var mEl) &&
                                        mEl.TryGetInt32(out var m)
                            ? m
                            : 0;
                        activity.Log(username, ActivityEventCatalog.GitHubStarPrompt, new Dictionary<string, object>
                        {
                            ["milestone"] = milestone
                        });
                        break;
                    }
                    case "app_notice":
                    {
                        if (!doc.RootElement.TryGetProperty("noticeId", out var nEl))
                        {
                            await ApiHelpers.WritePlainError(context, 400, "noticeId required");
                            return;
                        }

                        var noticeId = nEl.GetString();
                        if (!AppNotices.IsValid(noticeId))
                        {
                            await ApiHelpers.WritePlainError(context, 400, "Invalid noticeId");
                            return;
                        }

                        activity.Log(username, ActivityEventCatalog.AppNoticePrompt, new Dictionary<string, object>
                        {
                            ["noticeId"] = noticeId!
                        });
                        break;
                    }
                    case "welcome_cs24":
                        activity.Log(username, ActivityEventCatalog.WelcomeCs24Prompt);
                        break;
                    default:
                        await ApiHelpers.WritePlainError(context, 400, "Invalid prompt");
                        return;
                }

                await ApiHelpers.WriteJson(context, new { ok = true });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Prompt Shown API Error", ex);
            }
        });
        api.MapGet("/dashboard-feedback", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            try
            {
                var feedbackService = await ApiHelpers.RequireFeedbackServiceAsync(context);
                if (feedbackService == null) return;

                var entries = await feedbackService.GetSubmittedFeedbackAsync();
                await ApiHelpers.WriteJson(context, new
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
                await ApiHelpers.WriteServerError(context, "Dashboard Feedback API Error", ex);
            }
        });

        api.MapGet("/online-count", async context =>
        {
            try
            {
                if (!ApiHelpers.TryResolveAuthService(context, out var authService))
                {
                    await ApiHelpers.WritePlainError(context, 503, "AuthService not available");
                    return;
                }

                var heartbeat = context.Request.Query["heartbeat"] == "1";
                var username = context.Session.GetString("Username");
                if (heartbeat && !string.IsNullOrEmpty(username) &&
                    await authService.TouchLastSeenIfDueAsync(username))
                {
                    ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
                }

                var onlineCount = await authService.GetOnlineUserCountAsync();
                var data = new { online = onlineCount };

                await ApiHelpers.WriteJson(context, data);
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Online Count API Error", ex);
            }
        });

        api.MapGet("/stats-data", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            try
            {
                var username = context.Session.GetString("Username")!;
                var streak = context.Session.GetInt32("CurrentStreak") ?? 0;
                var progressService = context.RequestServices.GetService<UserProgressService>();

                if (progressService != null)
                {
                    var snap = await progressService.GetQuizStatsSnapshotAsync(username);
                    var successRate = snap.TotalAnswered > 0
                        ? (int)Math.Round((double)snap.CorrectAnswers / snap.TotalAnswered * 100)
                        : 0;

                    await ApiHelpers.WriteJson(context, new
                    {
                        correct = snap.CorrectAnswers,
                        total = snap.TotalAnswered,
                        successRate,
                        streak,
                        level = snap.Level,
                        xp = snap.Xp,
                        xpProgressPercent = snap.XpProgressPercent,
                        xpToNextLevel = snap.XpToNextLevel
                    });
                    return;
                }

                if (!ApiHelpers.TryResolveAuthService(context, out var authService))
                {
                    await ApiHelpers.WritePlainError(context, 503, "AuthService not available");
                    return;
                }

                var user = await authService.GetUserAsync(username);
                if (user == null)
                {
                    await ApiHelpers.WritePlainError(context, 401, "Unauthorized");
                    return;
                }

                var xp = user.Xp;
                var level = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(xp);
                var total = user.TotalAnswered;
                var correct = user.CorrectAnswers;
                var successRateFallback = total > 0 ? (int)Math.Round((double)correct / total * 100) : 0;

                await ApiHelpers.WriteJson(context, new
                {
                    correct,
                    total,
                    successRate = successRateFallback,
                    streak,
                    level,
                    xp,
                    xpProgressPercent = QuizGamification.XpProgressPercent(xp),
                    xpToNextLevel = QuizGamification.XpToNextLevel(xp)
                });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Stats Data API Error", ex);
            }
        });

        api.MapGet("/question-difficulty", async (HttpContext context) =>
        {
            try
            {
                var svc = context.RequestServices.GetService<QuestionDifficultyService>();
                if (svc == null) return Results.Problem("Difficulty service unavailable", statusCode: 503);
                var items = await svc.GetAllQuestionsAsync();
                return Results.Json(new { items });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 500);
            }
        });

        api.MapGet("/question-explanation", async (HttpContext context) =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            var questionId = context.Request.Query["questionId"].ToString();
            if (string.IsNullOrWhiteSpace(questionId))
            {
                await ApiHelpers.WritePlainError(context, 400, "questionId required");
                return;
            }

            try
            {
                var svc = context.RequestServices.GetService<QuestionExplanationService>();
                if (svc == null || !svc.IsEnabled)
                {
                    await ApiHelpers.WriteJson(context, new { hasExplanation = false, videoUrl = (string?)null });
                    return;
                }

                var (hasExplanation, videoUrl) = await svc.GetVideoUrlAsync(questionId.Trim());
                await ApiHelpers.WriteJson(context, new { hasExplanation, videoUrl });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Question Explanation API Error", ex);
            }
        });

        api.MapGet("/question-explanations-status", async (HttpContext context) =>
        {
            if (!await ApiHelpers.RequireAuthAdminAsync(context)) return;

            try
            {
                var svc = context.RequestServices.GetService<QuestionExplanationService>();
                var groups = context.RequestServices.GetService<QuestionGroupLoader>();
                if (svc == null || !svc.IsEnabled)
                {
                    await ApiHelpers.WriteJson(context, new { enabled = false, summary = new { }, items = Array.Empty<object>() });
                    return;
                }

                var statusFilter = context.Request.Query["status"].ToString();
                var summary = await svc.GetStatusSummaryAsync();
                var items = await svc.ListAsync(string.IsNullOrWhiteSpace(statusFilter) ? null : statusFilter, 500);
                var totalQuestions = groups != null ? (await groups.ListAllGroupsAsync()).Count : 0;

                await ApiHelpers.WriteJson(context, new
                {
                    enabled = true,
                    totalQuestions,
                    summary = new
                    {
                        ready = summary.Ready,
                        pending = summary.Pending,
                        failed = summary.Failed,
                        needsReview = summary.NeedsReview,
                        recorded = summary.Total,
                        missing = Math.Max(0, totalQuestions - summary.Total)
                    },
                    items = items.Select(i => new
                    {
                        questionFile = i.QuestionFile,
                        questionLabel = QuestionLabel.Format(i.QuestionFile),
                        status = i.Status,
                        videoPath = i.VideoPath,
                        errorMessage = i.ErrorMessage,
                        generatedAt = i.GeneratedAt?.ToUniversalTime().ToString("o")
                    })
                });
            }
            catch (Exception ex)
            {
                await ApiHelpers.WriteServerError(context, "Question Explanations Status API Error", ex);
            }
        });
    }
}
