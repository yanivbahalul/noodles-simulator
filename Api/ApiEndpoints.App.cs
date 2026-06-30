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
    private static void MapApp(RouteGroupBuilder api)
    {
        api.MapGet("/leaderboard-data", async context =>
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
        });


        api.MapPost("/notices/dismiss", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

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
        });


        api.MapPost("/welcome/cs24-click", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

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
        });


        api.MapPost("/feedback/submit", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

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
        });


        api.MapPost("/feedback/later", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

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
        });


        api.MapPost("/activity/prompt-shown", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

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
        });


        api.MapGet("/online-count", async context =>
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
        });

        api.MapPost("/offline", async context =>
        {
            if (!ApiHelpers.IsAuthenticated(context))
            {
                context.Response.StatusCode = 204;
                return;
            }

            if (!ApiHelpers.TryResolveAuthService(context, out var authService))
            {
                context.Response.StatusCode = 204;
                return;
            }

            var username = context.Session.GetString("Username");
            if (!string.IsNullOrWhiteSpace(username))
            {
                await authService.MarkOfflineAsync(username);
                ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
            }

            context.Response.StatusCode = 204;
        });


        api.MapGet("/stats-data", async context =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

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
        });

    }
}
